using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 전투 준비 단계의 건설 화면 도구. 임시 코스트 차감·환불, 내가 지은 조각의 철거,
    /// 부대를 골라 클릭한 자리에 바로 배치하는 기능을 맡는다. 전투가 시작되면 잠긴다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class SiegePrepController : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] Camera view;
        [SerializeField] PlacementController placement;
        [SerializeField] SquadCommandCenter center;
        [SerializeField] SiegeSettings settings;
        // 배치한 부대가 바라보는 방향(도). 성 바깥쪽이다.
        [SerializeField] float squadFacing;
        readonly Dictionary<int, int> pieceCosts = new Dictionary<int, int>();
        readonly Dictionary<int, int> weaponCosts = new Dictionary<int, int>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        public BuildBudget Budget { get; private set; }
        public int SelectedSquad { get; private set; } = -1;
        public bool Dismantling { get; private set; }
        public bool Locked { get; private set; }
        public string Message { get; private set; } = "성벽을 고치고 부대를 배치한 뒤 전투를 시작하세요.";

        public void Configure(ConstructionWorld owner, Camera buildCamera, PlacementController placementController,
            SquadCommandCenter commandCenter, SiegeSettings siegeSettings, float facing)
        {
            world = owner; view = buildCamera; placement = placementController; center = commandCenter;
            settings = siegeSettings; squadFacing = facing;
        }

        void Awake()
        {
            Budget = new BuildBudget(settings.StartingBudget);
            placement.ConfirmGate = CanAffordSelection;
            placement.PiecePlaced += OnPiecePlaced;
            placement.WeaponMounted += OnWeaponMounted;
        }

        void OnDestroy()
        {
            if (placement == null) return;
            placement.ConfirmGate = null;
            placement.PiecePlaced -= OnPiecePlaced;
            placement.WeaponMounted -= OnWeaponMounted;
        }

        int SelectionCost => placement.SelectedWeapon != null ? settings.CostOf(placement.SelectedWeapon)
            : placement.Selected != null ? settings.CostOf(placement.Selected) : 0;

        bool CanAffordSelection()
        {
            if (Locked) return false;
            if (Budget.CanAfford(SelectionCost)) return true;
            Message = "코스트가 부족합니다.";
            return false;
        }

        void OnPiecePlaced(PlacedStructure structure)
        {
            int cost = settings.CostOf(structure.Placement.Definition);
            Budget.TrySpend(cost);
            pieceCosts[structure.Placement.Id] = cost;
            Message = structure.Placement.Definition.DisplayName + "을(를) 지었습니다.";
        }

        void OnWeaponMounted(PlacedSiegeWeapon weapon)
        {
            int cost = settings.CostOf(weapon.Definition);
            Budget.TrySpend(cost);
            weaponCosts[weapon.Mount.Id] = cost;
            Message = weapon.Definition.DisplayName + "을(를) 설치했습니다.";
        }

        public void SelectSquad(int squad)
        {
            if (Locked || squad < 0 || squad >= center.Squads.Count) return;
            placement.Cancel();
            Dismantling = false;
            SelectedSquad = squad;
            center.Commander.Select(squad);
            Message = ProjectNeo.UI.SquadCommandHud.SquadName(center, squad) + ": 배치할 땅이나 성벽 위를 클릭하세요.";
        }

        public void BeginDismantle()
        {
            if (Locked) return;
            placement.Cancel();
            SelectedSquad = -1;
            Dismantling = true;
            Message = "철거: 이번에 지은 조각을 클릭하세요. 코스트를 전부 돌려받습니다.";
        }

        public void CancelTool()
        {
            SelectedSquad = -1;
            Dismantling = false;
        }

        /// <summary>전투가 시작되면 건설·철거·즉시 배치를 모두 막는다.</summary>
        public void Lock()
        {
            CancelTool();
            placement.Cancel();
            Locked = true;
        }

        /// <summary>부대의 살아 있는 인원을 가리킨 자리에 대형으로 바로 놓는다.</summary>
        public bool PlaceSquad(int squad, Vector3 point)
        {
            if (Locked || squad < 0 || squad >= center.Squads.Count) return false;
            if (world.IsRebuilding || world.RebuildPending)
            {
                Message = "지형을 반영하고 있습니다. 잠시 뒤 다시 클릭하세요.";
                return false;
            }
            SquadCommandSettings command = center.Settings;
            if (!NavMesh.SamplePosition(point, out NavMeshHit origin, command.NavigationSnapDistance, NavMesh.AllAreas)
                || Mathf.Abs(origin.position.y - point.y) > command.VerticalSnapTolerance)
            {
                Message = "부대를 놓을 수 없는 곳입니다.";
                return false;
            }
            var members = new List<UnitCombatAI>();
            foreach (UnitCombatAI member in center.Squads[squad].Members)
            {
                if (member == null || !member.isActiveAndEnabled || member.Actor.Health == null
                    || member.Actor.Health.IsDead) continue;
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) members.Add(member);
            }
            List<Vector3> slots = SquadFormation.Slots(origin.position, squadFacing, members.Count,
                command.FormationColumns, command.FormationSpacing);
            for (int i = 0; i < members.Count; i++)
            {
                // 대형 자리가 걸을 수 없거나 다른 층이면 가리킨 자리에 놓는다.
                Vector3 slot = NavMesh.SamplePosition(slots[i], out NavMeshHit hit, command.NavigationSnapDistance,
                    NavMesh.AllAreas) && Mathf.Abs(hit.position.y - origin.position.y) <= command.VerticalSnapTolerance
                    ? hit.position : origin.position;
                members[i].GetComponent<NavMeshAgent>().Warp(slot);
                members[i].transform.rotation = Quaternion.Euler(0f, squadFacing, 0f);
            }
            // 놓인 자리를 지키도록 중지 지시를 내린다.
            center.Commander.Select(squad);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Hold));
            foreach (UnitCombatAI member in members) member.SetDirective(UnitDirective.Hold);
            Message = (squad + 1) + "번 부대를 배치했습니다.";
            return true;
        }

        /// <summary>이번 준비 단계에 지은 조각이나 그 위의 무기를 없애고 코스트를 전부 돌려준다.</summary>
        public bool Dismantle(PlacedStructure structure)
        {
            if (Locked || structure == null) return false;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                if (weapon.Support != structure) continue;
                int id = weapon.Mount.Id;
                string name = weapon.Definition.DisplayName;
                if (!world.RemoveWeapon(weapon)) return false;
                if (weaponCosts.TryGetValue(id, out int refund)) { Budget.Refund(refund); weaponCosts.Remove(id); }
                Message = name + "을(를) 철거했습니다.";
                return true;
            }
            int pieceId = structure.Placement.Id;
            if (!pieceCosts.TryGetValue(pieceId, out int cost))
            {
                Message = "처음부터 있던 조각은 철거할 수 없습니다.";
                return false;
            }
            string pieceName = structure.Placement.Definition.DisplayName;
            if (!world.TryDismantle(structure))
            {
                Message = "유닛이 위에 있거나 겹쳐 있어 철거할 수 없습니다.";
                return false;
            }
            Budget.Refund(cost);
            pieceCosts.Remove(pieceId);
            Message = pieceName + "을(를) 철거했습니다.";
            return true;
        }

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        void Update()
        {
            if (Locked || Mouse.current == null) return;
            if (Mouse.current.rightButton.wasPressedThisFrame) CancelTool();
            // 조각이나 무기를 고르면 배치·철거 도구는 끝난다.
            if (placement.HasSelection) { CancelTool(); return; }
            if (SelectedSquad < 0 && !Dismantling) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (OverUI(pointer)) return;
            Ray ray = view.ScreenPointToRay(pointer);
            int count = Physics.RaycastNonAlloc(ray, rayHits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            int picked = -1;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].distance >= nearest || rayHits[i].collider.gameObject.scene != gameObject.scene) continue;
                if (rayHits[i].collider.GetComponentInParent<CharacterActor>() != null) continue;
                nearest = rayHits[i].distance; picked = i;
            }
            if (picked < 0) return;
            if (Dismantling)
            {
                var structure = rayHits[picked].collider.GetComponentInParent<PlacedStructure>();
                if (structure == null) Message = "철거할 조각을 클릭하세요.";
                else Dismantle(structure);
            }
            else if (rayHits[picked].normal.y < center.Settings.MinimumSurfaceUp) Message = "땅이나 성벽 윗면을 클릭하세요.";
            else PlaceSquad(SelectedSquad, rayHits[picked].point);
        }
    }
}
