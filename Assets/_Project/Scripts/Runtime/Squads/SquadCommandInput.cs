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
        InputAction selectAll, move, attack, hold;
        public string Message { get; private set; } = "";

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
        }

        void OnEnable() { foreach (InputAction action in All()) action.Enable(); }
        void OnDisable() { foreach (InputAction action in All()) action.Disable(); }
        void OnDestroy() { foreach (InputAction action in All()) action.Dispose(); }

        System.Collections.Generic.IEnumerable<InputAction> All()
        {
            if (selectSquad == null) yield break;
            foreach (InputAction action in selectSquad) yield return action;
            yield return selectAll; yield return move; yield return attack; yield return hold;
        }

        void Update()
        {
            for (int i = 0; i < selectSquad.Length; i++)
                if (selectSquad[i].WasPressedThisFrame()) SelectSquad(i);
            if (selectAll.WasPressedThisFrame()) SelectAll();
            if (move.WasPressedThisFrame()) OrderMove();
            if (attack.WasPressedThisFrame()) Order(SquadOrderKind.Attack);
            if (hold.WasPressedThisFrame()) Order(SquadOrderKind.Hold);
        }

        public bool SelectSquad(int index)
        {
            bool selected = center.Commander.Select(index);
            Message = selected ? "" : (index + 1) + "번 부대가 없습니다.";
            return selected;
        }

        public void SelectAll() { center.Commander.SelectAll(); Message = ""; }

        public bool OrderMove()
        {
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            if (!TryGetAimPoint(out Vector3 point)) { Message = "갈 수 없는 곳입니다."; return false; }
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, point, view.transform.eulerAngles.y));
            Message = "";
            return true;
        }

        public bool Order(SquadOrderKind kind)
        {
            if (kind == SquadOrderKind.Move) return OrderMove();
            if (center.Commander.SelectedCount == 0) { Message = "먼저 부대를 선택하세요."; return false; }
            Message = "";
            // 공격은 가리킨 지점으로 가면서 싸운다. 가리킨 곳이 없으면 지금 자리에서 싸운다.
            if (kind == SquadOrderKind.Attack && TryGetAimPoint(out Vector3 point))
                center.Commander.Issue(new SquadOrder(kind, point, view.transform.eulerAngles.y));
            else
            {
                center.Commander.Issue(new SquadOrder(kind));
                if (kind == SquadOrderKind.Attack) Message = "가리킨 곳이 없어 제자리에서 싸웁니다.";
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
