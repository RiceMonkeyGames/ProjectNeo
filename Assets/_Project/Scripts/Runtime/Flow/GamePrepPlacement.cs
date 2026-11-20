using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 준비 단계 부대 배치(PREP-05). 1인칭 탐색에서는 전투와 같은 지휘 키를 쓰고, 따로 여는 부대 배치 화면(위에서 보는 시점)
    /// 에서는 부대 선택 키(F1~F10)·`나 유닛 클릭으로 부대를 고른 뒤 좌클릭한 곳으로 걸어가게 한다. 준비 중 지시는 성벽·성문이 둘러싼 범위 안만
    /// 받고, 부대마다 마지막 배치 자리를 기억했다가 다음 준비 단계가 시작되면 그 자리로 걸어서 돌아가게 한다.
    /// 전투 중 지시는 제한하지 않고 기억하지도 않는다.
    /// </summary>
    public sealed class GamePrepPlacement : MonoBehaviour
    {
        public const string OutsideMessage = "성 밖에는 부대를 배치할 수 없습니다.";

        GamePhaseView phaseView;
        SquadCommandInput commandInput;
        PlacementController placement;
        Camera topDownCamera;
        GameFlow flow;
        SquadCommandCenter center;
        ConstructionWorld world;
        readonly SquadPlacementMemory memory = new SquadPlacementMemory();
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        InputAction[] selectSquad;
        InputAction selectAll;
        bool subscribed, wasPlacing;

        /// <summary>부대 배치 화면이 열려 있다.</summary>
        public bool PlacementMode => phaseView != null && phaseView.IsPlacementView;
        public string Message { get; private set; } = "";
        public SquadPlacementMemory Memory => memory;
        public SquadCommandCenter Center => center;

        public void Configure(GamePhaseView view, SquadCommandInput input, PlacementController placementController, Camera camera)
        {
            phaseView = view; commandInput = input; placement = placementController; topDownCamera = camera;
        }

        void Awake() => flow = GameSession.Flow;

        void Start()
        {
            center = commandInput != null ? commandInput.Center : null;
            world = placement != null ? placement.World : null;
            if (commandInput != null) commandInput.DestinationCheck = CheckDestination;
            if (center != null && center.Commander != null)
            {
                center.Commander.OrderChanged += OnOrderChanged;
                center.Commander.SquadRemoved += memory.RemoveSquad;
                subscribed = true;
                SquadCommandSettings settings = center.Settings;
                selectSquad = new InputAction[settings.SelectSquad.Length];
                for (int i = 0; i < selectSquad.Length; i++) selectSquad[i] = settings.SelectSquad[i].Clone();
                selectAll = settings.SelectAll.Clone();
                foreach (InputAction action in Actions()) action.Enable();
            }
            flow.PhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            if (commandInput != null && commandInput.DestinationCheck == (System.Func<Vector3, string>)CheckDestination)
                commandInput.DestinationCheck = null;
            if (subscribed && center != null && center.Commander != null)
            {
                center.Commander.OrderChanged -= OnOrderChanged;
                center.Commander.SquadRemoved -= memory.RemoveSquad;
            }
            foreach (InputAction action in Actions()) action.Dispose();
        }

        IEnumerable<InputAction> Actions()
        {
            if (selectSquad == null) yield break;
            foreach (InputAction action in selectSquad) yield return action;
            yield return selectAll;
        }

        // ── 성 안 판정 ─────────────────────────────────────────

        /// <summary>준비 단계에서만 성 밖 목적지를 거절한다. 허용하면 null.</summary>
        public string CheckDestination(Vector3 point)
        {
            if (flow == null || flow.Phase != GamePhase.Preparation) return null;
            return IsInsideCastle(point) ? null : OutsideMessage;
        }

        /// <summary>성벽·성문 조각이 차지하는 범위 안인지. 조각이 없으면 막지 않는다.</summary>
        public bool IsInsideCastle(Vector3 point)
        {
            if (world == null) return true;
            var corners = new List<Vector2>();
            foreach (PlacedStructure piece in world.Structures)
            {
                if (piece == null || piece.Placement == null) continue;
                BuildPieceKind kind = piece.Placement.Definition.Kind;
                if (kind != BuildPieceKind.Wall && kind != BuildPieceKind.Gate) continue;
                bool found = false;
                Bounds bounds = default;
                foreach (Collider body in piece.GetComponentsInChildren<Collider>())
                {
                    if (body.isTrigger) continue;
                    if (!found) { bounds = body.bounds; found = true; } else bounds.Encapsulate(body.bounds);
                }
                if (!found) bounds = new Bounds(piece.transform.position, Vector3.zero);
                Vector3 min = world.LocalPoint(bounds.min), max = world.LocalPoint(bounds.max);
                corners.Add(new Vector2(min.x, min.z));
                corners.Add(new Vector2(max.x, max.z));
            }
            if (!CastleInteriorRules.TryGetBounds(corners, 0f, out Rect rect)) return true;
            Vector3 local = world.LocalPoint(point);
            return CastleInteriorRules.Contains(rect, new Vector2(local.x, local.z));
        }

        // ── 기억과 복귀 ───────────────────────────────────────

        void OnOrderChanged(int squad, SquadOrder order)
        {
            if (flow.Phase != GamePhase.Preparation) return;
            if (order.HasDestination) { memory.Remember(squad, order.Destination, order.Facing); return; }
            // 목적지 없는 중지(제자리 지키기)는 지금 서 있는 곳을 자리로 삼는다.
            if (order.Kind == SquadOrderKind.Hold && center.TryGetGatherPoint(squad, out Vector3 here))
            {
                float facing = memory.TryGet(squad, out SquadPlacementMemory.Entry previous) ? previous.Facing : 0f;
                memory.Remember(squad, here, facing);
            }
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Preparation) return;
            StopAllCoroutines();
            StartCoroutine(ReturnToPlacements());
        }

        /// <summary>길이 다시 만들어지고 전투 정지가 풀린 뒤, 기억한 자리로 부대를 걸어서 보낸다.</summary>
        IEnumerator ReturnToPlacements()
        {
            yield return null;
            while (world != null && (world.IsRebuilding || world.RebuildPending)) yield return null;
            yield return null;
            if (flow.Phase != GamePhase.Preparation || center == null) yield break;
            ReturnToPlacementsNow();
        }

        /// <summary>기억한 자리가 있는 부대를 그곳으로 보낸다. 성 밖이 되었거나 걸을 수 없는 자리는 잊는다. 보낸 부대 수.</summary>
        public int ReturnToPlacementsNow()
        {
            if (center == null || flow.Phase != GamePhase.Preparation) return 0;
            SquadCommander commander = center.Commander;
            var previous = new bool[commander.SquadCount];
            for (int i = 0; i < previous.Length; i++) previous[i] = commander.IsSelected(i);
            int sent = 0;
            for (int i = 0; i < commander.SquadCount; i++)
            {
                if (!memory.TryGet(i, out SquadPlacementMemory.Entry entry)) continue;
                if (!IsInsideCastle(entry.Position) || !NavMesh.SamplePosition(entry.Position, out NavMeshHit hit,
                    center.Settings.NavigationSnapDistance, NavMesh.AllAreas))
                { memory.Forget(i); continue; }
                commander.Select(i);
                sent += commander.Issue(new SquadOrder(SquadOrderKind.Move, hit.position, entry.Facing));
            }
            // 선택은 하나 또는 전체만 있으므로 그 상태로 되돌린다.
            int previouslySelected = 0, last = -1;
            for (int i = 0; i < previous.Length; i++) if (previous[i]) { previouslySelected++; last = i; }
            if (previouslySelected == 0 || commander.SquadCount == 0) commander.ClearSelection();
            else if (previouslySelected == 1 && last < commander.SquadCount) commander.Select(last);
            else commander.SelectAll();
            return sent;
        }

        // ── 부대 배치 화면 ─────────────────────────────────────

        void Update()
        {
            bool placing = PlacementMode && center != null;
            if (placing != wasPlacing)
            {
                wasPlacing = placing;
                Message = "";
                // 화면을 닫으면 고른 부대도 놓는다. 1인칭 지휘 선택과 섞이지 않게 한다.
                if (!placing && center != null) center.Commander.ClearSelection();
            }
            if (!placing) return;
            for (int i = 0; i < selectSquad.Length; i++)
                if (selectSquad[i].WasPressedThisFrame()) SelectSquadForPlacement(i);
            if (selectAll.WasPressedThisFrame()) { center.Commander.SelectAll(); Message = ""; }
            if (Mouse.current == null || topDownCamera == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (Mouse.current.rightButton.wasPressedThisFrame) { center.Commander.ClearSelection(); Message = "선택을 해제했습니다."; }
            else if (Mouse.current.leftButton.wasPressedThisFrame && !OverUI(pointer))
                ClickAt(topDownCamera.ScreenPointToRay(pointer));
        }

        public bool SelectSquadForPlacement(int squad)
        {
            bool selected = center.Commander.Select(squad);
            Message = selected ? "" : $"{squad + 1}부대가 없습니다.";
            return selected;
        }

        /// <summary>선택한 부대 번호를 "1번, 3번"처럼 적는다. 없으면 "없음".</summary>
        public string SelectionText()
        {
            if (center == null) return "없음";
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < center.Commander.SquadCount; i++)
                if (center.Commander.IsSelected(i)) builder.Append(builder.Length == 0 ? "" : ", ").Append(i + 1);
            return builder.Length == 0 ? "없음" : builder.ToString();
        }

        /// <summary>
        /// 부대 배치 화면에서 클릭한 곳을 처리한다. 아군 유닛이면 그 부대를 고르고, 걸을 수 있는 땅이나 성벽 위면
        /// 고른 부대를 그곳으로 보낸다.
        /// </summary>
        public bool ClickAt(Ray ray)
        {
            if (center == null) return false;
            SquadCommandSettings settings = center.Settings;
            int count = Physics.RaycastNonAlloc(ray, rayHits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit? first = null;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].distance >= nearest || rayHits[i].collider.gameObject.scene != gameObject.scene) continue;
                nearest = rayHits[i].distance; first = rayHits[i];
            }
            if (!first.HasValue) { Message = "그곳으로 이동할 수 없습니다."; return false; }
            RaycastHit hit = first.Value;
            var unit = hit.collider.GetComponentInParent<UnitCombatAI>();
            if (unit != null)
            {
                int squad = SquadOf(unit);
                if (squad < 0) { Message = "이 아군은 부대에 속해 있지 않습니다."; return false; }
                return SelectSquadForPlacement(squad);
            }
            if (hit.collider.GetComponentInParent<CharacterActor>() != null) { Message = "이 아군은 부대에 속해 있지 않습니다."; return false; }
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            if (hit.normal.y < settings.MinimumSurfaceUp
                || !NavMesh.SamplePosition(hit.point, out NavMeshHit navigation, settings.NavigationSnapDistance, NavMesh.AllAreas)
                || Mathf.Abs(navigation.position.y - hit.point.y) > settings.VerticalSnapTolerance)
            { Message = "그곳으로 이동할 수 없습니다."; return false; }
            string rejected = CheckDestination(navigation.position);
            if (rejected != null) { Message = rejected; return false; }
            float facing = topDownCamera != null ? topDownCamera.transform.eulerAngles.y : 0f;
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, navigation.position, facing));
            Message = "";
            return true;
        }

        int SquadOf(UnitCombatAI unit)
        {
            for (int i = 0; i < center.Squads.Count; i++)
            {
                if (center.Squads[i].Leader == unit) return i;
                foreach (UnitCombatAI member in center.Squads[i].Members) if (member == unit) return i;
            }
            return -1;
        }

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
