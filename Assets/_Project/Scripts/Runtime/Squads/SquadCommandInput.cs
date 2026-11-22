using ProjectNeo.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ProjectNeo.Squads
{
    /// <summary>부대 선택·지시 입력. 키는 SquadCommandSettings 에셋에서 읽는다.</summary>
    public sealed class SquadCommandInput : MonoBehaviour
    {
        [SerializeField] SquadCommandCenter center;
        [SerializeField] Camera view;
        readonly RaycastHit[] hits = new RaycastHit[32];
        InputAction[] selectSquad;
        InputAction selectAll, move, attack, hold, cycleFormation, dragFormation;
        bool dragging, hasDragEnd;
        Vector3 dragStart, dragEnd;
        public string Message { get; private set; } = "";
        /// <summary>전열을 끄는 중이다(COMBAT-08).</summary>
        public bool IsDragging => dragging;
        public SquadCommandCenter Center => center;
        /// <summary>조준 광선을 쏘는 카메라(COMBAT-06 스킬 조준도 같은 화면 가운데를 쓴다).</summary>
        public Camera View => view;
        /// <summary>
        /// 목적지 있는 지시를 내리기 전에 묻는다. 거절 문구를 돌려주면 지시하지 않는다(PREP-05 준비 단계 성 안 제한). null이면 허용.
        /// </summary>
        public System.Func<Vector3, string> DestinationCheck;

        public void Configure(SquadCommandCenter commandCenter, Camera cameraView) { center = commandCenter; view = cameraView; }

        void Awake()
        {
            // 공유 에셋의 액션 상태를 바꾸지 않도록 복제해서 쓴다.
            SquadCommandSettings settings = center.Settings;
            selectSquad = new InputAction[settings.SelectSquad.Length];
            for (int i = 0; i < selectSquad.Length; i++) selectSquad[i] = settings.SelectSquad[i].Clone();
            selectAll = settings.SelectAll.Clone();
            move = settings.Move.Clone();
            attack = settings.Attack.Clone();
            hold = settings.Hold.Clone();
            // 예전 설정 에셋에는 이 키가 비어 있을 수 있어 기본 키를 쓴다.
            cycleFormation = OrDefault(settings.CycleFormation, "CycleFormation", SquadCommandSettings.DefaultCycleFormationBinding);
            dragFormation = OrDefault(settings.DragFormation, "DragFormation", SquadCommandSettings.DefaultDragFormationBinding);
        }

        static InputAction OrDefault(InputAction source, string name, string binding) =>
            source != null && source.bindings.Count > 0 ? source.Clone() : new InputAction(name, InputActionType.Button, binding);

        void OnEnable() { foreach (InputAction action in All()) action.Enable(); }
        void OnDisable()
        {
            foreach (InputAction action in All()) action.Disable();
            CancelDrag();
        }
        void OnDestroy() { foreach (InputAction action in All()) action.Dispose(); }

        System.Collections.Generic.IEnumerable<InputAction> All()
        {
            if (selectSquad == null) yield break;
            foreach (InputAction action in selectSquad) yield return action;
            yield return selectAll; yield return move; yield return attack; yield return hold;
            yield return cycleFormation; yield return dragFormation;
        }

        void Update()
        {
            for (int i = 0; i < selectSquad.Length; i++)
                if (selectSquad[i].WasPressedThisFrame()) SelectSquad(i);
            if (selectAll.WasPressedThisFrame()) SelectAll();
            if (move.WasPressedThisFrame()) OrderMove();
            if (attack.WasPressedThisFrame()) Order(SquadOrderKind.Attack);
            if (hold.WasPressedThisFrame()) Order(SquadOrderKind.Hold);
            if (cycleFormation.WasPressedThisFrame()) CycleFormation();
            if (dragFormation.WasPressedThisFrame()) BeginDrag();
            if (dragging)
            {
                if (dragFormation.IsPressed()) UpdateDrag();
                else EndDrag();
            }
        }

        // ── 진형(COMBAT-08) ─────────────────────────────────────

        static readonly string[] FormationNames = { "횡대", "밀집", "산개", "종대" };

        public static string FormationName(FormationKind kind) =>
            (int)kind >= 0 && (int)kind < FormationNames.Length ? FormationNames[(int)kind] : kind.ToString();

        /// <summary>선택한 부대의 진형을 다음으로 바꾼다. 바꾼 부대가 있으면 true.</summary>
        public bool CycleFormation()
        {
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            center.CycleSelectedFormation();
            for (int i = 0; i < center.Commander.SquadCount; i++)
                if (center.Commander.IsSelected(i)) { Message = "진형: " + FormationName(center.Commander.FormationOf(i)); break; }
            return true;
        }

        /// <summary>화면 가운데가 가리키는 곳에서 전열 긋기를 시작한다. 시작했으면 true.</summary>
        public bool BeginDrag()
        {
            CancelDrag();
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            if (!TryGetAimPoint(out Vector3 point)) { Message = "그곳으로 이동할 수 없습니다."; return false; }
            dragging = true; hasDragEnd = false; dragStart = point;
            Message = "";
            return true;
        }

        /// <summary>지금 가리키는 곳까지 선을 늘리고 자리 미리보기를 갱신한다.</summary>
        public void UpdateDrag()
        {
            if (!dragging) return;
            if (TryGetAimPoint(out Vector3 point)) { dragEnd = point; hasDragEnd = true; }
            if (IsLine()) center.PreviewLine(dragStart, dragEnd, view.transform.forward);
            else center.Preview.Clear();
        }

        /// <summary>
        /// 긋기를 끝내고 지시한다. 충분히 끌었으면 그은 선에 서고, 아니면 시작점으로 지금 폭 그대로 이동한다. 지시했으면 true.
        /// </summary>
        public bool EndDrag()
        {
            if (!dragging) return false;
            dragging = false;
            center.Preview.Clear();
            if (!IsLine()) return MoveTo(dragStart);
            int issued = center.IssueLine(dragStart, dragEnd, view.transform.forward, SquadOrderKind.Move, DestinationCheck,
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

        bool MoveTo(Vector3 point)
        {
            string rejected = DestinationCheck?.Invoke(point);
            if (!string.IsNullOrEmpty(rejected)) { Message = rejected; return false; }
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, point, view.transform.eulerAngles.y));
            Message = "";
            return true;
        }

        /// <summary>
        /// 부대 하나를 고른다. 그 부대만 골라 둔 상태에서 같은 키를 다시 누르면 선택을 푼다(UI-12, 사용자 결정 2026-11-22).
        /// 골라졌으면 true, 풀었거나 없는 부대면 false.
        /// </summary>
        public bool SelectSquad(int index)
        {
            SquadCommander commander = center.Commander;
            if (commander.IsSelected(index) && commander.SelectedCount == 1)
            {
                commander.ClearSelection();
                Message = "";
                return false;
            }
            bool selected = commander.Select(index);
            Message = selected ? "" : $"{index + 1}부대가 없습니다.";
            return selected;
        }

        /// <summary>모든 부대를 고른다. 이미 모두 골라져 있으면 선택을 푼다(UI-12, 사용자 결정 2026-11-22).</summary>
        public void SelectAll()
        {
            SquadCommander commander = center.Commander;
            if (commander.SquadCount > 0 && commander.SelectedCount == commander.SquadCount) commander.ClearSelection();
            else commander.SelectAll();
            Message = "";
        }

        public bool OrderMove()
        {
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            if (!TryGetAimPoint(out Vector3 point)) { Message = "그곳으로 이동할 수 없습니다."; return false; }
            return MoveTo(point);
        }

        public bool Order(SquadOrderKind kind)
        {
            if (kind == SquadOrderKind.Move) return OrderMove();
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            Message = "";
            // 공격은 가리킨 지점으로 가면서 싸운다. 가리킨 곳이 없으면 지금 자리에서 싸운다.
            if (kind == SquadOrderKind.Attack && TryGetAimPoint(out Vector3 point))
            {
                string rejected = DestinationCheck?.Invoke(point);
                if (!string.IsNullOrEmpty(rejected)) { Message = rejected; return false; }
                center.Commander.Issue(new SquadOrder(kind, point, view.transform.eulerAngles.y));
            }
            else
            {
                center.Commander.Issue(new SquadOrder(kind));
                if (kind == SquadOrderKind.Attack) Message = "목표 지점이 없어 제자리에서 교전합니다.";
            }
            return true;
        }

        /// <summary>화면 가운데가 가리키는 땅이나 성벽 위에서 걸을 수 있는 지점. 유닛은 건너뛴다.</summary>
        public bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            SquadCommandSettings settings = center.Settings;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, hits, settings.MaxAimDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool found = false;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= nearest || hits[i].collider.gameObject.scene != gameObject.scene) continue;
                if (hits[i].collider.GetComponentInParent<CharacterActor>() != null) continue;
                nearest = hits[i].distance; point = hits[i].point; normal = hits[i].normal; found = true;
            }
            // 벽 옆면을 가리켰을 때 지면이나 위층으로 붙이지 않는다. 처음 맞은 면을 기준으로 고른다.
            if (!found || normal.y < settings.MinimumSurfaceUp
                || !NavMesh.SamplePosition(point, out NavMeshHit hit, settings.NavigationSnapDistance, NavMesh.AllAreas)
                || Mathf.Abs(hit.position.y - point.y) > settings.VerticalSnapTolerance)
                return false;
            point = hit.position;
            return true;
        }
    }
}
