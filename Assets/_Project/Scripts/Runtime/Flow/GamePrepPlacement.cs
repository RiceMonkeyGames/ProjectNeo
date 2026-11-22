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
        CastleBattlements battlements;
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
            // 진형 줄 수 상한으로 성벽 위 통로 폭을 쓴다(COMBAT-08).
            // 원거리 분대 앞줄은 성가퀴 빈틈 사격 자리에 선다(COMBAT-10).
            if (center != null && world != null && world.TryGetComponent(out battlements))
            {
                center.WalkwayWidth = battlements.WalkwayWidthAt;
                center.CrenelPoints = battlements.CollectCrenelStandPoints;
            }
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
            if (center != null && battlements != null
                && center.WalkwayWidth == (System.Func<Vector3, Vector3, float>)battlements.WalkwayWidthAt)
                center.WalkwayWidth = null;
            if (center != null && battlements != null
                && center.CrenelPoints == (System.Action<Vector3, float, List<Vector3>>)battlements.CollectCrenelStandPoints)
                center.CrenelPoints = null;
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
            if (order.HasDestination) { memory.Remember(squad, order.Destination, order.Facing, order.FrontAnchored); return; }
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
                sent += commander.Issue(new SquadOrder(SquadOrderKind.Move, hit.position, entry.Facing, entry.FrontAnchored));
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
                if (!placing && center != null) { CancelDrag(); center.Commander.ClearSelection(); }
            }
            if (!placing) return;
            for (int i = 0; i < selectSquad.Length; i++)
                if (selectSquad[i].WasPressedThisFrame()) SelectSquadForPlacement(i);
            if (selectAll.WasPressedThisFrame())
            {
                // 모두 골라져 있으면 전체 선택 키로 푼다(UI-12, 1인칭 지휘와 같다).
                SquadCommander commander = center.Commander;
                if (commander.SquadCount > 0 && commander.SelectedCount == commander.SquadCount) commander.ClearSelection();
                else commander.SelectAll();
                Message = "";
            }
            if (Mouse.current == null || topDownCamera == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            Ray ray = topDownCamera.ScreenPointToRay(pointer);
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelDrag();
                center.Commander.ClearSelection(); Message = "선택을 해제했습니다.";
            }
            else if (Mouse.current.leftButton.wasPressedThisFrame && !OverUI(pointer)) BeginDragAt(ray);
            // 좌클릭을 누른 채 끌면 전열을 긋고(COMBAT-08), 그냥 떼면 누른 곳으로 이동한다.
            if (dragging)
            {
                if (Mouse.current.leftButton.isPressed) DragTo(ray);
                else EndDrag();
            }
        }

        // ── 전열 긋기(COMBAT-08) ───────────────────────────────

        bool dragging, hasDragEnd;
        Vector3 dragStart, dragEnd;

        /// <summary>배치 화면에서 전열을 끄는 중이다.</summary>
        public bool IsDragging => dragging;

        /// <summary>
        /// 누른 곳을 처리한다. 아군 유닛이면 그 부대를 고르고(<see cref="ClickAt"/>와 같다), 걸을 수 있는 곳이면 전열 긋기를 시작한다.
        /// </summary>
        public bool BeginDragAt(Ray ray)
        {
            CancelDrag();
            if (!TryPickGround(ray, out Vector3 point, out bool result)) return result;
            dragging = true; hasDragEnd = false; dragStart = point;
            Message = "";
            return true;
        }

        /// <summary>가리키는 곳까지 선을 늘리고 자리 미리보기를 갱신한다. 유닛은 건너뛴다.</summary>
        public void DragTo(Ray ray)
        {
            if (!dragging) return;
            if (TryGroundPoint(ray, out Vector3 point)) { dragEnd = point; hasDragEnd = true; }
            if (IsLine()) center.PreviewLine(dragStart, dragEnd, ViewForward());
            else center.Preview.Clear();
        }

        /// <summary>긋기를 끝낸다. 충분히 끌었으면 그은 선에 세우고, 아니면 누른 곳으로 지금 폭 그대로 보낸다.</summary>
        public bool EndDrag()
        {
            if (!dragging) return false;
            dragging = false;
            center.Preview.Clear();
            if (!IsLine()) return MoveSelectedTo(dragStart);
            int issued = center.IssueLine(dragStart, dragEnd, ViewForward(), SquadOrderKind.Move, CheckDestination,
                out string rejected);
            Message = !string.IsNullOrEmpty(rejected) ? rejected : "";
            return issued > 0;
        }

        public void CancelDrag()
        {
            dragging = false; hasDragEnd = false;
            if (center != null) center.Preview.Clear();
        }

        bool IsLine()
        {
            if (!hasDragEnd) return false;
            Vector3 along = dragEnd - dragStart; along.y = 0f;
            return along.magnitude >= center.Settings.MinDragLength;
        }

        // 위에서 보는 화면의 "앞"은 화면 위쪽이다. 카메라가 거의 수직이어도 위쪽 벡터로 방향을 잡는다.
        Vector3 ViewForward()
        {
            if (topDownCamera == null) return Vector3.forward;
            Vector3 forward = topDownCamera.transform.forward + topDownCamera.transform.up;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward : Vector3.forward;
        }

        /// <summary>
        /// 배치할 부대를 고른다. 1인칭 지휘(SquadCommandInput)와 같이, 그 부대만 골라 둔 채 같은 키를 다시 누르면 선택을 푼다(UI-12).
        /// </summary>
        public bool SelectSquadForPlacement(int squad)
        {
            if (center.Commander.IsSelected(squad) && center.Commander.SelectedCount == 1)
            {
                center.Commander.ClearSelection(); Message = "선택을 해제했습니다.";
                return false;
            }
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
            if (!TryPickGround(ray, out Vector3 point, out bool result)) return result;
            return MoveSelectedTo(point);
        }

        // 맨 처음 맞은 것을 본다. 유닛이면 부대를 고르고 false(결과는 result), 걸을 수 있는 곳이면 그 지점과 true.
        bool TryPickGround(Ray ray, out Vector3 point, out bool result)
        {
            point = default; result = false;
            if (center == null) return false;
            if (!TryFirstHit(ray, false, out RaycastHit hit)) { Message = "그곳으로 이동할 수 없습니다."; return false; }
            var unit = hit.collider.GetComponentInParent<UnitCombatAI>();
            if (unit != null)
            {
                int squad = SquadOf(unit);
                if (squad < 0) { Message = "이 아군은 부대에 속해 있지 않습니다."; return false; }
                result = SelectSquadForPlacement(squad);
                return false;
            }
            if (hit.collider.GetComponentInParent<CharacterActor>() != null) { Message = "이 아군은 부대에 속해 있지 않습니다."; return false; }
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            if (!TrySnap(hit, out point)) { Message = "그곳으로 이동할 수 없습니다."; return false; }
            return true;
        }

        // 유닛을 건너뛰고 맨 처음 맞은 걸을 수 있는 지점(끄는 동안 쓴다).
        bool TryGroundPoint(Ray ray, out Vector3 point)
        {
            point = default;
            return TryFirstHit(ray, true, out RaycastHit hit) && TrySnap(hit, out point);
        }

        bool TryFirstHit(Ray ray, bool skipActors, out RaycastHit first)
        {
            first = default;
            int count = Physics.RaycastNonAlloc(ray, rayHits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].distance >= nearest || rayHits[i].collider.gameObject.scene != gameObject.scene) continue;
                if (skipActors && rayHits[i].collider.GetComponentInParent<CharacterActor>() != null) continue;
                nearest = rayHits[i].distance; first = rayHits[i];
            }
            return !float.IsPositiveInfinity(nearest);
        }

        bool TrySnap(RaycastHit hit, out Vector3 point)
        {
            point = default;
            SquadCommandSettings settings = center.Settings;
            if (hit.normal.y < settings.MinimumSurfaceUp
                || !NavMesh.SamplePosition(hit.point, out NavMeshHit navigation, settings.NavigationSnapDistance, NavMesh.AllAreas)
                || Mathf.Abs(navigation.position.y - hit.point.y) > settings.VerticalSnapTolerance)
                return false;
            point = navigation.position;
            return true;
        }

        bool MoveSelectedTo(Vector3 point)
        {
            string rejected = CheckDestination(point);
            if (rejected != null) { Message = rejected; return false; }
            float facing = topDownCamera != null ? topDownCamera.transform.eulerAngles.y : 0f;
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, point, facing));
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
