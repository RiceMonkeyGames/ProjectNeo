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
        GameConstructionWork work;
        bool startFundsGranted;

        public bool Dismantling { get; private set; }
        public string Message { get; private set; } = string.Empty;
        /// <summary>건설 막대 상태 띠 오른쪽에 보일 결과 안내를 바꾼다(PREP-11 작업 탭: 고용·취소 결과).</summary>
        public void Notify(string message) => Message = message ?? string.Empty;
        public BuildEconomy Economy => EnsureEconomy();
        public BuildCostTable Table => table;
        public RunConfig StartConfig => startConfig;
        /// <summary>건설·수리 작업 주문(SIEGE-08). 준비 시간이 꺼져 있으면 주문 없이 예전처럼 바로 완성된다.</summary>
        public GameConstructionWork Work => work;

        public void Configure(BuildCostTable costs, RunConfig start, ConstructionWorld castle, PlacementController controller,
            GamePhaseView view, Camera buildCamera, TMP_Text economyLabel)
        {
            table = costs; startConfig = start; world = castle; placement = controller;
            phaseView = view; constructionCamera = buildCamera; label = economyLabel;
            if (work != null) work.Configure(world, placement, battle, this, label != null ? label.font : null);
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            work = GetComponent<GameConstructionWork>();
            if (work == null) work = gameObject.AddComponent<GameConstructionWork>();
            work.Configure(world, placement, battle, this, label != null ? label.font : null);
        }

        void OnEnable()
        {
            flow.PhaseChanged += OnPhaseChanged;
            if (world != null) world.PieceReplacing += OnPieceReplacing;
            if (placement == null) return;
            placement.ConfirmGate = CanConfirm;
            placement.PiecePlaced += OnPiecePlaced;
            placement.WeaponMounted += OnWeaponMounted;
        }

        void OnDisable()
        {
            hover.Clear();
            flow.PhaseChanged -= OnPhaseChanged;
            if (world != null) world.PieceReplacing -= OnPieceReplacing;
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
                    // 승천의 시작 골드 감소(FLOW-03). 해금 트랙의 시작 보너스 골드(META-07)는 따로 주므로 줄지 않는다.
                    state.Wallet.AddGold(state.Ascension.StartingGold(startConfig.StartingGold));
                    foreach (MaterialAmount entry in startConfig.StartingMaterials)
                        if (entry.material != null && entry.amount > 0) state.Wallet.AddMaterial(entry.material, entry.amount);
                }
                economy.BeginPreparation();
            }
            return economy;
        }

        /// <summary>
        /// 런 데이터가 씬에 붙은 직후 한 번 부른다(FLOW-02). 새 런이면 시작 재화를 바로 주어 첫 저장에 담기게 하고,
        /// 저장에서 되살린 런이면 시작 재화를 다시 주지 않는다.
        /// </summary>
        public void OnRunReady(bool restored)
        {
            if (startFundsGranted) return;
            if (!restored) { EnsureEconomy(); return; }
            startFundsGranted = true;
            EnsureEconomy()?.BeginPreparation();
        }

        void OnPhaseChanged(GamePhase phase)
        {
            Dismantling = false;
            if (phase == GamePhase.Preparation) { EnsureEconomy()?.BeginPreparation(); Message = string.Empty; }
        }

        bool CanConfirm()
        {
            // 이번 놓기에서 바꿔 끼운 성벽 환불 안내는 놓기마다 새로 센다.
            replacedRefund = replacedCount = 0;
            BuildEconomy current = EnsureEconomy();
            if (current == null || placement == null) return true;
            // 한 번에 여러 조각이 쌓이면(망루 2층 등) 그 수만큼 낸다(SIEGE-24, 층마다 비용).
            bool affordable = placement.SelectedWeapon != null ? current.CanAfford(placement.SelectedWeapon)
                : placement.Selected == null || current.CanAfford(placement.Selected, placement.PreviewCount);
            if (!affordable) Message = "골드가 부족합니다.";
            return affordable;
        }

        void OnPiecePlaced(PlacedStructure body)
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || body == null) return;
            if (!current.PayForPiece(body.Placement.Definition, body.Placement.Id))
                Debug.LogWarning("설치 확인 뒤 골드가 모자라 비용을 내지 못했다: " + body.Placement.Definition.DisplayName);
            // 준비 시간이 켜져 있으면 바로 완성하지 않고 작업 주문으로 짓는다(SIEGE-08).
            if (Preparing) work?.OrderPiece(body);
        }

        // 망루가 성벽 줄 칸을 바꿔 끼우면(SIEGE-24) 없어지는 성벽 층마다 철거처럼 환불하고 걸린 작업 주문을 지운다.
        void OnPieceReplacing(PlacedStructure wall)
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || wall == null || wall.Placement == null) return;
            int gold = current.RefundPiece(wall.Placement.Definition, wall.Placement.Id) + (work != null ? work.Cancel(wall) : 0);
            replacedRefund += gold;
            replacedCount++;
            Message = $"성벽 {replacedCount}층을 망루로 바꿈 - {replacedRefund}골드 환불";
        }

        int replacedRefund, replacedCount;

        void OnWeaponMounted(PlacedSiegeWeapon weapon)
        {
            BuildEconomy current = EnsureEconomy();
            if (current == null || weapon == null) return;
            if (!current.PayForWeapon(weapon.Definition, weapon.Mount.Id))
                Debug.LogWarning("설치 확인 뒤 골드가 모자라 비용을 내지 못했다: " + weapon.Definition.DisplayName);
            if (Preparing) work?.OrderWeapon(weapon);
        }

        bool Preparing => flow.Phase == GamePhase.Preparation;

        /// <summary>철거 도구를 켜고 끈다. 켜진 동안 건설 화면에서 조각을 클릭하면 철거한다.</summary>
        public void ToggleDismantle()
        {
            if (!Preparing || EnsureEconomy() == null) return;
            placement.Cancel();
            Dismantling = !Dismantling;
            if (!Dismantling) hover.Clear();
            Message = Dismantling ? "해체할 조각이나 대포를 클릭하세요. 우클릭으로 마칩니다." : string.Empty;
        }

        /// <summary>
        /// 조각 위 무기가 있으면 무기를, 없으면 조각을 철거하고 환불한다. 성문·위에 다른 조각이 얹힌 조각·유닛이 겹친 조각은
        /// 철거하지 않는다.
        /// </summary>
        public bool Dismantle(PlacedStructure structure)
        {
            BuildEconomy current = EnsureEconomy();
            if (!Preparing || current == null || structure == null) return false;
            // 망루는 맨 위층부터 해체한다. 처음 층 수만 남았으면 그 층 모두를 한 번에(SIEGE-24).
            List<PlacedStructure> group = DismantleGroup(structure);
            if (group.Count > 0) structure = group[0];
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                if (weapon.Support != structure) continue;
                SiegeWeaponDefinition definition = weapon.Definition;
                int mountId = weapon.Mount.Id;
                // 걸린 작업 주문을 지운다. 수리 주문이었으면 다 못 한 만큼 돌려준다(SIEGE-08).
                int repairRefund = work != null ? work.Cancel(weapon) : 0;
                if (!world.RemoveWeapon(weapon)) return false;
                int refund = current.RefundWeapon(definition, mountId) + repairRefund;
                Message = $"{definition.DisplayName} 해체 - {refund}골드 환불";
                return true;
            }
            BuildPlacement placed = structure.Placement;
            if (placed.IsFixed) { Message = "성문은 해체할 수 없습니다."; return false; }
            if (group.Count > 1) return DismantleTiers(current, group);
            BuildPieceDefinition piece = placed.Definition;
            int id = placed.Id;
            if (!world.TryDismantle(structure))
            {
                Message = "해체할 수 없습니다: 위에 조각이나 유닛이 있습니다.";
                return false;
            }
            // 짓는 중인 조각은 이번 준비에 지은 것이라 환불이 전액이다. 수리 주문이었으면 남은 몫을 더 돌려준다.
            int gold = current.RefundPiece(piece, id) + (work != null ? work.Cancel(structure) : 0);
            Message = $"{piece.DisplayName} 해체 - {gold}골드 환불";
            return true;
        }

        // 망루의 처음 층들을 위층부터 한 번에 해체하고 층마다 환불한다(SIEGE-24). 맨 위층을 뺄 수 없으면 아무것도 하지 않는다.
        bool DismantleTiers(BuildEconomy current, List<PlacedStructure> tiers)
        {
            string name = tiers[0].Placement.Definition.DisplayName;
            int gold = 0, removed = 0;
            foreach (PlacedStructure tier in tiers)
            {
                BuildPieceDefinition piece = tier.Placement.Definition;
                int id = tier.Placement.Id;
                if (!world.TryDismantle(tier)) break;
                gold += current.RefundPiece(piece, id) + (work != null ? work.Cancel(tier) : 0);
                removed++;
            }
            if (removed == 0) { Message = "해체할 수 없습니다: 위에 조각이나 유닛이 있습니다."; return false; }
            Message = removed == tiers.Count ? $"{name} {removed}층 해체 - {gold}골드 환불"
                : $"{name} {removed}층만 해체 - {gold}골드 환불 (남은 층 위나 안에 유닛이 있습니다)";
            return true;
        }

        // 성벽 조각과 수성 무기(SIEGE-02)를 함께 모은다. 둘의 번호 체계가 달라 수리 목록 안에서만 쓰는 순번을 붙인다.
        List<RepairNeed> RepairNeeds(Dictionary<int, Component> lookup)
        {
            var needs = new List<RepairNeed>();
            if (world == null) return needs;
            foreach (PlacedStructure piece in world.Structures)
                if (piece != null && !piece.IsUnderConstruction && (work == null || !work.HasOrder(piece)))
                    AddNeed(needs, lookup, piece, piece.Health, piece.Placement != null && piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null && !weapon.IsRemoved && (work == null || !work.HasOrder(weapon)))
                    AddNeed(needs, lookup, weapon, weapon.Health, false);
            return needs;
        }

        static void AddNeed(List<RepairNeed> needs, Dictionary<int, Component> lookup, Component target, Health health, bool allowDead)
        {
            if (health == null || (health.IsDead && !allowDead)) return;
            float missing = health.Maximum - health.Current;
            if (missing <= 0f) return;
            int key = needs.Count;
            needs.Add(new RepairNeed(key, missing));
            lookup?.Add(key, target);
        }

        static Health HealthOf(Component target) =>
            target is PlacedStructure piece ? piece.Health : target is PlacedSiegeWeapon weapon ? weapon.Health : null;

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
            var lookup = new Dictionary<int, Component>();
            List<RepairNeed> needs = RepairNeeds(lookup);
            if (needs.Count == 0) { Message = "수리할 곳이 없습니다."; return 0; }
            List<RepairNeed> chosen = current.PlanRepair(needs, out int cost);
            if (chosen.Count == 0 || !current.Wallet.TrySpend(new Cost(cost)))
            {
                Message = "수리할 골드가 부족합니다.";
                return 0;
            }
            bool ordered = work != null && work.IsActive;
            foreach (RepairNeed need in chosen)
            {
                Component target = lookup[need.Id];
                // 준비 시간이 켜져 있으면 수리도 작업 주문이다. 골드는 지금 다 내고 전투 때 못 한 만큼 돌려받는다(SIEGE-08).
                if (ordered && work.OrderRepair(target, current.RepairCost(need.Missing))) continue;
                Health health = HealthOf(target);
                if (health.IsDead) health.ReviveFull();
                else health.RestoreFull();
            }
            string verb = ordered ? "주문" : "완료";
            Message = chosen.Count == needs.Count ? $"전체 수리 {verb} · {cost}골드"
                : $"수리 {chosen.Count}/{needs.Count}곳 {verb} · {cost}골드 (골드 부족)";
            return chosen.Count;
        }

        /// <summary>버튼용. 결과는 <see cref="Message"/>로 보여준다.</summary>
        public void RepairAllFromButton() => RepairAll();

        void Update()
        {
            BuildEconomy current = EnsureEconomy();
            if (label != null) label.text = Describe(current);
            if (!Dismantling || current == null || Mouse.current == null) { hover.Clear(); return; }
            if (!Preparing || phaseView == null || !phaseView.IsBuildView || placement.HasSelection) { Dismantling = false; hover.Clear(); return; }
            if (Mouse.current.rightButton.wasPressedThisFrame) { Dismantling = false; Message = string.Empty; hover.Clear(); return; }
            if (constructionCamera == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            // UI-13: 해체 중에는 가리킨 조각(위에 무기가 있으면 무기)을 지을 수 없는 자리의 미리보기처럼 붉게 보여 준다.
            PlacedStructure hovered = OverUI(pointer) ? null : PickStructure(pointer);
            // 망루(SIEGE-24)는 해체될 층들(더 쌓은 층은 맨 위 한 층, 처음 층 수만 남았으면 그 층 모두)을 함께 보인다.
            List<PlacedStructure> group = DismantleGroup(hovered);
            PlacedStructure top = group.Count > 0 ? group[0] : null;
            GameObject target = DismantleTarget(top, out GameObject prefab);
            if (target != hover.Target)
            {
                // 조각이면 그 칸들의 성 킷 모델을 잠깐 숨기고 같은 칸 다른 층은 임시 외형으로 대신 보인다(킷이 칸 전체를 그린다).
                bool piece = top != null && target == top.gameObject;
                var stack = new List<GameObject>();
                if (piece) for (int i = group.Count - 1; i >= 0; i--) stack.Add(group[i].gameObject);
                hover.Show(target, prefab, placement.ViewSettings, piece ? KitCells(top) : null, piece ? StandIns(top, group) : null,
                    piece ? stack : null);
            }
            if (!Mouse.current.leftButton.wasPressedThisFrame || OverUI(pointer)) return;
            if (hovered == null) Message = "해체할 조각을 클릭하세요.";
            else { hover.Clear(); Dismantle(hovered); }
        }

        DismantleHighlight hoverView;
        // 처음 쓸 때 만든다(씬에서 불러온 컴포넌트에서도 늘 있게).
        DismantleHighlight hover => hoverView ??= new DismantleHighlight();

        CastleKitDresser kit;

        // 조각이 차지한 모든 칸의 성 킷 모델(2×2 망루는 네 칸, SIEGE-24).
        IEnumerable<GameObject> KitCells(PlacedStructure structure)
        {
            if (kit == null) kit = FindAnyObjectByType<CastleKitDresser>();
            if (kit == null) yield break;
            BuildPlacement placed = structure.Placement;
            for (int x = 0; x < placed.Size.x; x++)
                for (int z = 0; z < placed.Size.y; z++)
                {
                    GameObject root = kit.CellRoot(placed.Cell + new Vector2Int(x, z));
                    if (root != null) yield return root;
                }
        }

        IEnumerable<Renderer> StandIns(PlacedStructure structure, List<PlacedStructure> group)
        {
            foreach (PlacedStructure other in world.Structures)
            {
                if (other == null || other == structure || group.Contains(other) || other.Placement == null
                    || other.Placement.Cell != structure.Placement.Cell) continue;
                Transform visual = other.transform.Find("Visual");
                if (visual == null) continue;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) yield return renderer;
            }
        }

        /// <summary>
        /// 그 조각을 가리켜 해체하면 함께 없어질 조각들(위층부터). 보통은 그 조각 하나다. 망루(SIEGE-24, 사용자 결정 2026-11-27)는
        /// 어느 층을 가리켜도 맨 위층부터: 처음 쌓은 층 수(2층)보다 높으면 맨 위 한 층, 처음 층 수만 남았으면 그 층 모두를 한 번에 해체한다.
        /// </summary>
        public List<PlacedStructure> DismantleGroup(PlacedStructure structure)
        {
            var group = new List<PlacedStructure>();
            if (structure == null || structure.Placement == null) return group;
            BuildPlacement placed = structure.Placement;
            if (placed.Definition.Kind != BuildPieceKind.Tower) { group.Add(structure); return group; }
            foreach (PlacedStructure tier in world.Structures)
                if (tier != null && tier.Placement != null && tier.Placement.Definition.Kind == BuildPieceKind.Tower
                    && tier.Placement.Cell == placed.Cell) group.Add(tier);
            group.Sort((a, b) => b.Placement.Level.CompareTo(a.Placement.Level));
            if (group.Count > placed.Definition.InitialTiers) group.RemoveRange(1, group.Count - 1);
            return group;
        }

        // 해체하면 먼저 사라지는 것: 조각 위 무기, 없으면 조각. 미리보기를 만들 프리팹도 함께 돌려준다.
        GameObject DismantleTarget(PlacedStructure structure, out GameObject prefab)
        {
            prefab = null;
            if (structure == null) return null;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon.Support == structure)
                {
                    prefab = weapon.Definition != null ? weapon.Definition.Prefab : null;
                    return weapon.gameObject;
                }
            prefab = structure.Placement.Definition != null ? structure.Placement.Definition.Prefab : null;
            return structure.gameObject;
        }

        PlacedStructure PickStructure(Vector2 pointer)
        {
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
            return picked;
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
            // UI-13 건설 막대: 골드는 위 자원 바, 고른 조각 값은 카드에 있으므로 여기에는 수리·해체·작업·결과만 남긴다.
            var text = new StringBuilder();
            int repair = FullRepairCost;
            if (repair > 0) text.Append($"전체 수리 {repair}골드");
            if (Dismantling) Line(text, "해체 중: 조각 클릭 · 우클릭으로 마침");
            string queued = work != null ? work.Summary() : string.Empty;
            if (queued.Length > 0) Line(text, queued);
            if (!string.IsNullOrEmpty(Message)) Line(text, Message);
            return text.ToString();
        }

        static void Line(StringBuilder text, string line)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(line);
        }
    }
}
