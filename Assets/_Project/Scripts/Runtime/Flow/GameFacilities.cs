using System.Collections.Generic;
using System.Text;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 시설 팝업(D-0·D-1·D-5). 런 데이터에 시설 상태·상점 창구를 붙이고, 건물에서 F로 시설 팝업을 열며
    /// Esc나 닫기 버튼으로 닫는다. 팝업이 열린 동안은 <see cref="GamePhaseView.IsPanelView"/>로 이동·시점을 멈춘다.
    /// 모든 시설은 팝업에서 업그레이드하고, 상점은 매물 구매와 보관함 판매를 한다. 병영(D-4)은 잡졸을 모집하고 넣을 부대를 고르며,
    /// 레벨에 따른 훈련 보너스를 보여 준다. 대장간(D-6)은 레벨의 고정 레시피로 장비를 만들고 보관함 장비를 판다.
    /// 다른 시설의 기능은 후속 작업이다.
    /// 시설 목록·거래 수치는 <see cref="RunConfig"/> 에셋, 레벨·매물 풀은 시설 정의 에셋에서 바꾼다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameFacilities : MonoBehaviour
    {
        const int MaxInventoryRows = 8;

        [SerializeField] RunConfig startConfig;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text body;
        [SerializeField] Button upgradeButton;
        [SerializeField] TMP_Text upgradeLabel;
        [SerializeField] GameObject tradeArea;
        [SerializeField] RectTransform offerList;
        [SerializeField] RectTransform inventoryList;
        [SerializeField] Button rowTemplate;
        readonly List<Button> rows = new List<Button>();
        GameBattleController battle;
        GameUnitSpawner spawner;
        TMP_Text offerHeader;
        TMP_Text inventoryHeader;
        string shopOfferHeader;
        string shopInventoryHeader;
        Squad<RunUnit> chosenSquad;
        GameFlow flow;

        /// <summary>지금 열린 시설. 닫혀 있으면 null.</summary>
        public FacilityType? OpenFacility { get; private set; }
        public bool IsOpen => OpenFacility.HasValue;
        /// <summary>마지막 거래·업그레이드 결과 안내.</summary>
        public string Message { get; private set; } = string.Empty;
        public string TitleText => title != null ? title.text : string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;
        public string UpgradeText => upgradeLabel != null ? upgradeLabel.text : string.Empty;
        /// <summary>지금 보이는 매물 줄 수(상점).</summary>
        public int OfferRows => offerList != null ? ActiveChildren(offerList) : 0;
        /// <summary>병영에서 고른 부대 순번. -1이면 자동 배치(자리가 남은 첫 부대).</summary>
        public int ChosenSquad => chosenSquad != null && State != null ? State.IndexOf(chosenSquad) : -1;
        /// <summary>매물 칸 제목(상점: 매물, 병영: 모집).</summary>
        public string OfferHeaderText => offerHeader != null ? offerHeader.text : string.Empty;
        /// <summary>지금 보이는 보관함 줄 수(상점) 또는 부대 줄 수(병영).</summary>
        public int InventoryRows => inventoryList != null ? ActiveChildren(inventoryList) : 0;

        public void Configure(RunConfig start, GamePhaseView view, GameObject popup, TMP_Text titleLabel, TMP_Text statusLabel,
            TMP_Text bodyLabel)
        {
            startConfig = start; phaseView = view; panel = popup; title = titleLabel; status = statusLabel; body = bodyLabel;
        }

        public void ConfigureActions(Button upgrade, TMP_Text upgradeText, GameObject trade, RectTransform offers,
            RectTransform inventory, Button template)
        {
            upgradeButton = upgrade; upgradeLabel = upgradeText; tradeArea = trade; offerList = offers;
            inventoryList = inventory; rowTemplate = template;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            spawner = GetComponent<GameUnitSpawner>();
            offerHeader = HeaderOf(offerList);
            inventoryHeader = HeaderOf(inventoryList);
            shopOfferHeader = offerHeader != null ? offerHeader.text : string.Empty;
            shopInventoryHeader = inventoryHeader != null ? inventoryHeader.text : string.Empty;
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>런 데이터에 설정의 시설·상점 창구를 붙인다(이미 있으면 그대로). 런이 없으면 null.</summary>
        RunState PreparedState()
        {
            RunState state = State;
            if (state == null || startConfig == null) return state;
            state.AddFacilities(startConfig.Facilities);
            state.SetupShop(startConfig.Economy);
            return state;
        }

        /// <summary>런 데이터의 시설 상태. 아직 없으면 설정의 시설을 시작 레벨로 붙인다. 런이 없으면 null.</summary>
        public FacilityState Facility(FacilityType type) => PreparedState()?.Facility(type);

        public string NameOf(FacilityType type)
        {
            if (startConfig != null)
                foreach (FacilityDefinition definition in startConfig.Facilities)
                    if (definition != null && definition.Type == type) return definition.DisplayName;
            return type.ToString();
        }

        /// <summary>준비 중 1인칭 탐색에서 시설 팝업을 연다. 전투 중·건설 중·시설이 없으면 false.</summary>
        public bool Open(FacilityType type)
        {
            if (flow.Phase != GamePhase.Preparation || phaseView == null || panel == null) return false;
            if (Facility(type) == null)
            {
                Debug.LogWarning("런 데이터에 이 시설이 없어 팝업을 열지 않는다: " + type);
                return false;
            }
            if (!phaseView.EnterPanel()) return false;
            if (type == FacilityType.Shop) State.RestockShopIfNeeded(item => GameSession.Meta.IsUnlocked(item));
            OpenFacility = type;
            Message = string.Empty;
            chosenSquad = null;
            panel.SetActive(true);
            Rebuild();
            return true;
        }

        /// <summary>팝업을 닫고 1인칭 탐색으로 돌아간다. 닫기 버튼도 이것을 부른다.</summary>
        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        /// <summary>열린 시설을 한 레벨 올린다(D-1). 업그레이드 버튼이 부른다.</summary>
        public bool TryUpgrade()
        {
            if (!IsOpen || flow.Phase != GamePhase.Preparation) return false;
            FacilityState facility = Facility(OpenFacility.Value);
            if (facility == null) return false;
            if (facility.IsMaxLevel) { Message = "이미 최대 레벨입니다."; Rebuild(); return false; }
            Cost cost = facility.NextUpgradeCost;
            bool upgraded = State.TryUpgradeFacility(OpenFacility.Value);
            if (upgraded && OpenFacility == FacilityType.TrainingGround) battle.RunLink.ApplyTraining();
            Message = upgraded ? $"레벨 {facility.Level + 1}(으)로 올렸습니다 · {Describe(cost)}" : "비용이 모자랍니다: " + Describe(cost);
            Rebuild();
            return upgraded;
        }

        public void TryUpgradeFromButton() => TryUpgrade();

        /// <summary>상점 매물의 index번째를 산다(D-5).</summary>
        public bool TryBuy(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || index < 0 || index >= state.Shop.Stock.Count) return false;
            ItemDefinition item = state.Shop.Stock[index];
            bool bought = state.TryBuy(item);
            Message = bought ? $"{item.DisplayName} 구매 · {Describe(item.Price)}" : $"{item.DisplayName}: 비용이 모자랍니다.";
            Rebuild();
            return bought;
        }

        /// <summary>보관함의 index번째 아이템을 판다(D-5).</summary>
        public bool TrySell(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || index < 0 || index >= state.Inventory.Items.Count) return false;
            ItemInstance item = state.Inventory.Items[index];
            int price = state.Shop.GetSellPrice(item.Definition);
            bool sold = state.TrySell(item);
            if (sold) Message = $"{item.Definition.DisplayName} 판매 · {price}골드";
            Rebuild();
            return sold;
        }

        static bool IsUnlocked(ItemDefinition item) => GameSession.Meta.IsUnlocked(item);

        /// <summary>대장간 레시피의 index번째 장비를 만든다(D-6). 비용을 내고 보관함에 넣는다.</summary>
        public bool TryCraft(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Smithy || state == null || flow.Phase != GamePhase.Preparation) return false;
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsUnlocked);
            if (index < 0 || index >= recipes.Count) return false;
            CraftingRecipe recipe = recipes[index];
            CraftCheck check = state.CheckCraft(recipe.item, IsUnlocked);
            bool crafted = check == CraftCheck.Ok && state.TryCraft(recipe.item, IsUnlocked) != null;
            Message = crafted ? $"{recipe.item.DisplayName} 제작 · {Describe(recipe.cost)}" : $"{recipe.item.DisplayName}: " + Reason(check);
            Rebuild();
            return crafted;
        }

        /// <summary>대장간에서 보관함 장비의 index번째(장비만 센 순번)를 판다(D-6).</summary>
        public bool TrySellEquipment(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Smithy || state?.Shop == null) return false;
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (index < 0 || index >= equipment.Count) return false;
            ItemInstance item = equipment[index];
            int price = state.Shop.GetSellPrice(item.Definition);
            bool sold = state.TrySellAtSmithy(item);
            if (sold) Message = $"{item.Definition.DisplayName} 판매 · {price}골드";
            Rebuild();
            return sold;
        }

        static List<ItemInstance> EquipmentIn(Inventory inventory)
        {
            var equipment = new List<ItemInstance>();
            foreach (ItemInstance item in inventory.Items)
                if (item.Definition.Kind == ItemKind.Equipment) equipment.Add(item);
            return equipment;
        }

        static string Reason(CraftCheck check) => check switch
        {
            CraftCheck.BattlePending => "전투 중에는 만들 수 없습니다.",
            CraftCheck.NotCraftable => "지금 대장간 레벨에서는 만들 수 없습니다.",
            CraftCheck.NotEnoughFunds => "비용이 모자랍니다.",
            _ => "만들지 못했습니다.",
        };

        /// <summary>병영에서 모집할 수 있는 잡졸의 index번째를 고른 부대(없으면 자동 배치)로 모집한다(D-4). 비용을 낸다.</summary>
        public bool TryRecruit(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.TrainingGround || state == null || flow.Phase != GamePhase.Preparation) return false;
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            if (index < 0 || index >= soldiers.Count || soldiers[index] == null) return false;
            SoldierDefinition soldier = soldiers[index];
            if (chosenSquad != null && state.IndexOf(chosenSquad) < 0) chosenSquad = null;
            RecruitCheck check = state.CheckBarracksRecruit(soldier, chosenSquad);
            UnitCombatAI unit = null;
            int squad = -1;
            if (check == RecruitCheck.Ok && spawner != null) unit = spawner.RecruitAtBarracks(soldier, chosenSquad, out squad);
            Message = unit != null ? $"{soldier.DisplayName} 모집 · {Describe(soldier.RecruitCost)} → 부대 {squad + 1}"
                : $"{soldier.DisplayName}: " + Reason(check);
            Rebuild();
            return unit != null;
        }

        /// <summary>병영에서 새 잡졸을 넣을 부대를 고른다. -1이면 자동 배치(자리가 남은 첫 부대, 모두 차면 새 부대).</summary>
        public bool ChooseSquad(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.TrainingGround || state == null || index >= state.Army.Squads.Count) return false;
            chosenSquad = index < 0 ? null : state.Army.Squads[index];
            Message = string.Empty;
            Rebuild();
            return true;
        }

        static string Reason(RecruitCheck check) => check switch
        {
            RecruitCheck.BattlePending => "전투 중에는 모집할 수 없습니다.",
            RecruitCheck.NotRecruitable => "지금 병영 레벨에서는 모집할 수 없습니다.",
            RecruitCheck.NoSquad => "넣을 부대가 없습니다(고른 부대가 꽉 찼거나 부대 수 상한).",
            RecruitCheck.NotEnoughFunds => "비용이 모자랍니다.",
            _ => "모집하지 못했습니다.",
        };

        void Hide()
        {
            OpenFacility = null;
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (!OpenFacility.HasValue) return;
            // Esc(PreparationInteraction)나 단계 전환으로 팝업 화면이 끝나면 함께 닫는다.
            if (phaseView == null || !phaseView.IsPanelView) Hide();
        }

        void Rebuild()
        {
            FacilityState facility = OpenFacility.HasValue ? Facility(OpenFacility.Value) : null;
            if (facility == null) return;
            RunState state = State;
            FacilityType type = facility.Definition.Type;
            if (title != null) title.text = facility.Definition.DisplayName;
            if (status != null)
                status.text = $"레벨 {facility.Level + 1} / {facility.Definition.Levels.Count}\n" + DescribeWallet(state.Wallet);
            if (upgradeLabel != null)
                upgradeLabel.text = facility.IsMaxLevel ? "최대 레벨" : "업그레이드 · " + Describe(facility.NextUpgradeCost);
            if (upgradeButton != null) upgradeButton.interactable = !facility.IsMaxLevel;
            bool shop = type == FacilityType.Shop && state.Shop != null;
            bool barracks = type == FacilityType.TrainingGround;
            bool smithy = type == FacilityType.Smithy && state.Shop != null;
            if (body != null)
            {
                string text = shop ? "매물은 준비 단계마다 새로 바뀝니다. 업그레이드는 다음 준비의 매물부터 반영됩니다."
                    : barracks ? DescribeTraining(facility)
                    : smithy ? DescribeSmithy(facility)
                    : Purpose(type) + "\n이 시설의 기능과 업그레이드 효과는 다음 작업에서 붙습니다.";
                body.text = string.IsNullOrEmpty(Message) ? text : Message + "\n" + text;
            }
            if (tradeArea != null) tradeArea.SetActive(shop || barracks || smithy);
            if (offerHeader != null)
                offerHeader.text = barracks ? "모집 (클릭해서 모집)" : smithy ? "제작 (클릭해서 제작)" : shopOfferHeader;
            if (inventoryHeader != null)
                inventoryHeader.text = barracks ? "넣을 부대 (클릭해서 선택)" : smithy ? "장비 판매 (클릭해서 판매)" : shopInventoryHeader;
            ClearRows();
            if (rowTemplate == null) return;
            if (barracks) { BuildBarracksRows(state); return; }
            if (smithy) { BuildSmithyRows(state); return; }
            if (!shop) return;
            IReadOnlyList<ItemDefinition> stock = state.Shop.Stock;
            if (stock.Count == 0) AddRow(offerList, "매물이 없습니다.", null);
            for (int i = 0; i < stock.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{stock[i].DisplayName} · {Describe(stock[i].Price)}", () => TryBuy(index));
            }
            IReadOnlyList<ItemInstance> items = state.Inventory.Items;
            if (items.Count == 0) AddRow(inventoryList, "보관함이 비었습니다.", null);
            for (int i = 0; i < items.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                AddRow(inventoryList, $"{items[i].Definition.DisplayName} 팔기 · {state.Shop.GetSellPrice(items[i].Definition)}골드",
                    () => TrySell(index));
            }
            if (items.Count > MaxInventoryRows) AddRow(inventoryList, $"외 {items.Count - MaxInventoryRows}개", null);
        }

        void BuildBarracksRows(RunState state)
        {
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            if (soldiers.Count == 0) AddRow(offerList, "모집할 수 있는 잡졸이 없습니다.", null);
            for (int i = 0; i < soldiers.Count; i++)
            {
                int index = i;
                if (soldiers[i] == null) continue;
                AddRow(offerList, $"{soldiers[i].DisplayName} · {Describe(soldiers[i].RecruitCost)}", () => TryRecruit(index));
            }
            if (chosenSquad != null && state.IndexOf(chosenSquad) < 0) chosenSquad = null;
            AddRow(inventoryList, (chosenSquad == null ? "[선택] " : string.Empty) + "자동 배치 (빈자리 있는 첫 부대)", () => ChooseSquad(-1));
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            for (int i = 0; i < squads.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                string mark = squads[i] == chosenSquad ? "[선택] " : string.Empty;
                AddRow(inventoryList, $"{mark}부대 {i + 1} · {squads[i].Members.Count}/{squads[i].Capacity}명", () => ChooseSquad(index));
            }
            if (state.Army.CanAddSquad) AddRow(inventoryList, "모두 차면 새 부대를 만듭니다", null);
        }

        void BuildSmithyRows(RunState state)
        {
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsUnlocked);
            if (recipes.Count == 0) AddRow(offerList, "만들 수 있는 장비가 없습니다.", null);
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{recipes[i].item.DisplayName} · {Describe(recipes[i].cost)}", () => TryCraft(index));
            }
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (equipment.Count == 0) AddRow(inventoryList, "팔 장비가 없습니다.", null);
            for (int i = 0; i < equipment.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                AddRow(inventoryList, $"{equipment[i].Definition.DisplayName} 팔기 · {state.Shop.GetSellPrice(equipment[i].Definition)}골드",
                    () => TrySellEquipment(index));
            }
            if (equipment.Count > MaxInventoryRows) AddRow(inventoryList, $"외 {equipment.Count - MaxInventoryRows}개", null);
        }

        static string DescribeSmithy(FacilityState facility)
        {
            var text = new StringBuilder("레시피의 장비는 비용만 있으면 언제든 만들 수 있습니다. 착용 중인 장비는 팔 수 없습니다.");
            if (!facility.IsMaxLevel)
            {
                List<CraftingRecipe> next = facility.Definition.Levels[facility.Level + 1].recipes;
                text.Append($"\n다음 레벨: 레시피 {(next != null ? next.Count : 0)}종");
            }
            return text.ToString();
        }

        static string DescribeTraining(FacilityState facility)
        {
            var text = new StringBuilder("훈련 보너스(모든 잡졸): " + DescribeModifiers(SoldierTraining.ModifiersOf(facility)));
            if (!facility.IsMaxLevel)
                text.Append("\n다음 레벨: " + DescribeModifiers(facility.Definition.Levels[facility.Level + 1].soldierModifiers));
            text.Append("\n업그레이드하면 지금 있는 잡졸에게도 바로 붙습니다.");
            return text.ToString();
        }

        static string DescribeModifiers(IEnumerable<StatModifier> modifiers)
        {
            var text = new StringBuilder();
            if (modifiers != null)
                foreach (StatModifier modifier in modifiers)
                {
                    if (text.Length > 0) text.Append(" · ");
                    string amount = modifier.kind == ModifierKind.Percent ? $"{modifier.value * 100f:+0;-0}%" : $"{modifier.value:+0.#;-0.#}";
                    text.Append(StatName(modifier.stat) + " " + amount);
                }
            return text.Length > 0 ? text.ToString() : "없음";
        }

        static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "체력",
            StatType.Attack => "공격력",
            StatType.Defense => "방어력",
            StatType.AttackSpeed => "공격 속도",
            StatType.MoveSpeed => "이동 속도",
            StatType.AttackRange => "사거리",
            _ => stat.ToString(),
        };

        // 매물·보관함 칸 위의 제목. 칸의 부모(열) 아래 Header 텍스트를 쓴다. 없으면 null.
        static TMP_Text HeaderOf(RectTransform list)
        {
            Transform header = list != null && list.parent != null ? list.parent.Find("Header") : null;
            return header != null ? header.GetComponent<TMP_Text>() : null;
        }

        void AddRow(RectTransform list, string text, UnityEngine.Events.UnityAction action)
        {
            if (list == null) return;
            Button row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>(true).text = text;
            row.interactable = action != null;
            if (action != null) row.onClick.AddListener(action);
            rows.Add(row);
        }

        void ClearRows()
        {
            foreach (Button row in rows)
            {
                if (row == null) continue;
                // 같은 프레임에 다시 그리므로 줄 수가 바로 맞도록 먼저 끈다.
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
        }

        static int ActiveChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent) if (child.gameObject.activeSelf) count++;
            return count;
        }

        static string Describe(Cost cost)
        {
            var text = new StringBuilder($"{Mathf.Max(0, cost.gold)}골드");
            if (cost.materials != null)
                foreach (MaterialAmount entry in cost.materials)
                    if (entry.material != null && entry.amount > 0) text.Append($" · {entry.material.DisplayName} {entry.amount}");
            return text.ToString();
        }

        static string DescribeWallet(Wallet wallet)
        {
            var text = new StringBuilder($"골드 {wallet.Gold}");
            foreach (KeyValuePair<MaterialDefinition, int> material in wallet.Materials)
                if (material.Key != null && material.Value > 0) text.Append($" · {material.Key.DisplayName} {material.Value}");
            return text.ToString();
        }

        static string Purpose(FacilityType type) => type switch
        {
            FacilityType.Tavern => "영웅 고용",
            FacilityType.Shop => "아이템 구매·판매",
            FacilityType.TrainingGround => "잡졸 모집·강화",
            FacilityType.Smithy => "장비 생산·강화·판매",
            FacilityType.Defense => "성벽·무기 준비",
            _ => string.Empty,
        };
    }
}
