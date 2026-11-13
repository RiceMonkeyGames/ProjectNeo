using System.Collections.Generic;
using System.Text;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 건설·철거·수리를 런 골드와 잇는다(D-2). 규칙은 <see cref="BuildEconomy"/>, 수치는
    /// <see cref="BuildCostTable"/>, 시작 골드는 <see cref="RunConfig"/> 에셋에서 바꾼다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다. 부서진 성문 재건은 다루지 않는다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameBuildEconomy : MonoBehaviour
    {
        [SerializeField] BuildCostTable table;
        [SerializeField] RunConfig startConfig;
        [SerializeField] ConstructionWorld world;
        [SerializeField] PlacementController placement;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] Camera constructionCamera;
        [SerializeField] TMP_Text label;
        readonly RaycastHit[] rayHits = new RaycastHit[16];
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        GameBattleController battle;
        GameFlow flow;
        BuildEconomy economy;
        bool startFundsGranted;

        public bool Dismantling { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public BuildEconomy Economy => EnsureEconomy();
        public BuildCostTable Table => table;

        public void Configure(BuildCostTable costs, RunConfig start, ConstructionWorld castle, PlacementController controller,
            GamePhaseView view, Camera buildCamera, TMP_Text economyLabel)
        {
            table = costs; startConfig = start; world = castle; placement = controller;
            phaseView = view; constructionCamera = buildCamera; label = economyLabel;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
        }

        void OnEnable()
        {
            flow.PhaseChanged += OnPhaseChanged;
            if (placement == null) return;
            placement.ConfirmGate = CanConfirm;
            placement.PiecePlaced += OnPiecePlaced;
            placement.WeaponMounted += OnWeaponMounted;
        }

        void OnDisable()
        {
            flow.PhaseChanged -= OnPhaseChanged;
            if (placement == null) return;
            if (placement.ConfirmGate == (System.Func<bool>)CanConfirm) placement.ConfirmGate = null;
            placement.PiecePlaced -= OnPiecePlaced;
            placement.WeaponMounted -= OnWeaponMounted;
        }

        RunState State => battle != null ? battle.RunLink.State : null;

        BuildEconomy EnsureEconomy()
        {
            RunState state = State;
            if (state == null || table == null) return null;
            if (economy == null || economy.Wallet != state.Wallet) economy = new BuildEconomy(table, state.Wallet);
            if (!startFundsGranted)
            {
                // 첫 준비의 시작 재화. 게임 씬은 런마다 새로 열리므로 씬에서 한 번만 준다.
                startFundsGranted = true;
                if (startConfig != null)
                {
                    state.Wallet.AddGold(startConfig.StartingGold);
                    foreach (MaterialAmount entry in startConfig.StartingMaterials)
                        if (entry.material != null && entry.amount > 0) state.Wallet.AddMaterial(entry.material, entry.amount);
                }
                economy.BeginPreparation();
            }
            return economy;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            Dismantling = false;
            if (phase == GamePhase.Preparation) { EnsureEconomy()?.BeginPreparation(); Message = string.Empty; }
        }

        bool CanConfirm()
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || placement == null) return true;
            bool affordable = placement.SelectedWeapon != null ? current.CanAfford(placement.SelectedWeapon)
                : placement.Selected == null || current.CanAfford(placement.Selected);
            if (!affordable) Message = "골드가 모자랍니다.";
            return affordable;
        }

        void OnPiecePlaced(PlacedStructure body)
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || body == null) return;
            if (!current.PayForPiece(body.Placement.Definition, body.Placement.Id))
                Debug.LogWarning("설치 확인 뒤 골드가 모자라 비용을 내지 못했다: " + body.Placement.Definition.DisplayName);
        }

        void OnWeaponMounted(PlacedSiegeWeapon weapon)
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || weapon == null) return;
            if (!current.PayForWeapon(weapon.Definition, weapon.Mount.Id))
                Debug.LogWarning("설치 확인 뒤 골드가 모자라 비용을 내지 못했다: " + weapon.Definition.DisplayName);
        }

        bool Preparing => flow.Phase == GamePhase.Preparation;

        /// <summary>철거 도구를 켜고 끈다. 켜진 동안 건설 화면에서 조각을 클릭하면 철거한다.</summary>
        public void ToggleDismantle()
        {
            if (!Preparing || EnsureEconomy() == null) return;
            placement.Cancel();
            Dismantling = !Dismantling;
            Message = Dismantling ? "철거할 조각이나 대포를 클릭하세요. 우클릭으로 끝냅니다." : string.Empty;
        }

        /// <summary>
        /// 조각 위 무기가 있으면 무기를, 없으면 조각을 철거하고 환불한다. 성문·위에 다른 조각이 얹힌 조각·유닛이 겹친 조각은
        /// 철거하지 않는다.
        /// </summary>
        public bool Dismantle(PlacedStructure structure)
        {
            BuildEconomy current = EnsureEconomy();
            if (!Preparing || current == null || structure == null) return false;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                if (weapon.Support != structure) continue;
                SiegeWeaponDefinition definition = weapon.Definition;
                int mountId = weapon.Mount.Id;
                if (!world.RemoveWeapon(weapon)) return false;
                int refund = current.RefundWeapon(definition, mountId);
                Message = $"{definition.DisplayName} 철거 · {refund}골드 환불";
                return true;
            }
            BuildPlacement placed = structure.Placement;
            if (placed.IsFixed) { Message = "성문은 철거할 수 없습니다."; return false; }
            BuildPieceDefinition piece = placed.Definition;
            int id = placed.Id;
            if (!world.TryDismantle(structure))
            {
                Message = "위에 다른 조각이 있거나 유닛이 겹쳐 철거할 수 없습니다.";
                return false;
            }
            int gold = current.RefundPiece(piece, id);
            Message = $"{piece.DisplayName} 철거 · {gold}골드 환불";
            return true;
        }

        // 성벽 조각과 수성 무기(SIEGE-02)를 함께 모은다. 둘의 번호 체계가 달라 수리 목록 안에서만 쓰는 순번을 붙인다.
        List<RepairNeed> RepairNeeds(Dictionary<int, Health> lookup)
        {
            var needs = new List<RepairNeed>();
            if (world == null) return needs;
            foreach (PlacedStructure piece in world.Structures)
                if (piece != null) AddNeed(needs, lookup, piece.Health);
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null && !weapon.IsRemoved) AddNeed(needs, lookup, weapon.Health);
            return needs;
        }

        static void AddNeed(List<RepairNeed> needs, Dictionary<int, Health> lookup, Health health)
        {
            if (health == null || health.IsDead) return;
            float missing = health.Maximum - health.Current;
            if (missing <= 0f) return;
            int key = needs.Count;
            needs.Add(new RepairNeed(key, missing));
            lookup?.Add(key, health);
        }

        /// <summary>모두 수리하는 데 드는 골드.</summary>
        public int FullRepairCost => EnsureEconomy()?.FullRepairCost(RepairNeeds(null)) ?? 0;

        /// <summary>
        /// 가진 골드로 가장 많이 깎인 조각·무기부터 완전히 수리한다(2026-11-12 사용자 결정, 무기는 SIEGE-02).
        /// 수리한 개수를 돌려준다.
        /// </summary>
        public int RepairAll()
        {
            BuildEconomy current = EnsureEconomy();
            if (!Preparing || current == null) return 0;
            var lookup = new Dictionary<int, Health>();
            List<RepairNeed> needs = RepairNeeds(lookup);
            if (needs.Count == 0) { Message = "수리할 곳이 없습니다."; return 0; }
            List<RepairNeed> chosen = current.PlanRepair(needs, out int cost);
            if (chosen.Count == 0 || !current.Wallet.TrySpend(new Cost(cost)))
            {
                Message = "골드가 모자라 수리하지 못했습니다.";
                return 0;
            }
            foreach (RepairNeed need in chosen) lookup[need.Id].RestoreFull();
            Message = chosen.Count == needs.Count ? $"모두 수리 · {cost}골드" : $"{chosen.Count}/{needs.Count}곳 수리 · {cost}골드 (골드 부족)";
            return chosen.Count;
        }

        /// <summary>버튼용. 결과는 <see cref="Message"/>로 보여준다.</summary>
        public void RepairAllFromButton() => RepairAll();

        void Update()
        {
            BuildEconomy current = EnsureEconomy();
            if (label != null) label.text = Describe(current);
            if (!Dismantling || current == null || Mouse.current == null) return;
            if (!Preparing || phaseView == null || !phaseView.IsBuildView || placement.HasSelection) { Dismantling = false; return; }
            if (Mouse.current.rightButton.wasPressedThisFrame) { Dismantling = false; Message = string.Empty; return; }
            if (!Mouse.current.leftButton.wasPressedThisFrame || constructionCamera == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (OverUI(pointer)) return;
            Ray ray = constructionCamera.ScreenPointToRay(pointer);
            int count = Physics.RaycastNonAlloc(ray, rayHits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            PlacedStructure picked = null;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].distance >= nearest || rayHits[i].collider.gameObject.scene != gameObject.scene) continue;
                if (rayHits[i].collider.GetComponentInParent<CharacterActor>() != null) continue;
                nearest = rayHits[i].distance;
                picked = rayHits[i].collider.GetComponentInParent<PlacedStructure>();
            }
            if (picked == null) Message = "철거할 조각을 클릭하세요.";
            else Dismantle(picked);
        }

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        string Describe(BuildEconomy current)
        {
            if (current == null) return string.Empty;
            var text = new StringBuilder();
            text.Append($"골드 {current.Wallet.Gold}");
            if (placement != null && placement.SelectedWeapon != null)
                text.Append($" · {placement.SelectedWeapon.DisplayName} {table.CostOf(placement.SelectedWeapon)}골드");
            else if (placement != null && placement.Selected != null)
                text.Append($" · {placement.Selected.DisplayName} {table.CostOf(placement.Selected)}골드");
            int repair = FullRepairCost;
            if (repair > 0) text.Append($"\n모두 수리 {repair}골드");
            if (Dismantling) text.Append("\n철거 중: 조각 클릭 · 우클릭 끝");
            if (!string.IsNullOrEmpty(Message)) text.Append('\n').Append(Message);
            return text.ToString();
        }
    }
}
