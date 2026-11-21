using System.Collections.Generic;
using System.Text;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
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
    /// 주점(D-3)은 준비마다 뽑은 영웅을 고용해 고른 부대의 대장이나 대기 영웅으로 넣는다.
    /// 다른 시설의 기능은 후속 작업이다.
    /// 시설 목록·거래 수치는 <see cref="RunConfig"/> 에셋, 레벨·매물 풀은 시설 정의 에셋에서 바꾼다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameFacilities : MonoBehaviour
    {
        const int MaxInventoryRows = 8;

        [SerializeField] RunConfig startConfig;
        /// <summary>새 런의 시작 설정(시설·거래·강화 수치). 저장된 런을 되살릴 때도 같은 수치를 쓴다(FLOW-02).</summary>
        public RunConfig StartConfig => startConfig;
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
        [SerializeField] Button rerollButton;
        [SerializeField] TMP_Text rerollLabel;
        readonly List<Button> rows = new List<Button>();
        GameBattleController battle;
        GameUnitSpawner spawner;
        TMP_Text offerHeader;
        TMP_Text inventoryHeader;
        string shopOfferHeader;
        string shopInventoryHeader;
        Squad<RunUnit> chosenSquad;
        int captainSquad = -1;
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
        /// <summary>리롤 버튼 글자(상점). 버튼이 없으면 빈 글자.</summary>
        public string RerollText => rerollLabel != null ? rerollLabel.text : string.Empty;
        /// <summary>리롤 버튼이 지금 보이는지(상점 팝업에서만 보인다).</summary>
        public bool IsRerollVisible => rerollButton != null && rerollButton.gameObject.activeSelf;
        /// <summary>지금 보이는 매물 줄 수(상점).</summary>
        public int OfferRows => offerList != null ? ActiveChildren(offerList) : 0;
        /// <summary>병영에서 고른 부대 순번. -1이면 자동 배치(자리가 남은 첫 부대).</summary>
        public int ChosenSquad => chosenSquad != null && State != null ? State.IndexOf(chosenSquad) : -1;
        /// <summary>매물 칸 제목(상점: 매물, 병영: 모집).</summary>
        public string OfferHeaderText => offerHeader != null ? offerHeader.text : string.Empty;
        /// <summary>지금 보이는 보관함 줄 수(상점) 또는 부대 줄 수(병영).</summary>
        public int InventoryRows => inventoryList != null ? ActiveChildren(inventoryList) : 0;
        /// <summary>index번째 상점 매물이 잠겨 있는지(PREP-03).</summary>
        public bool IsOfferLocked(int index) => State?.Shop != null && State.Shop.IsLocked(index);
        public int ShopMaxLocked => State != null ? State.ShopMaxLocked : 0;
        public int ShopLockedCount => State?.Shop != null ? State.Shop.LockedCount : 0;
        public Button OfferLockButton(int index)
        {
            if (offerList == null || index < 0) return null;
            int found = 0;
            foreach (Button button in offerList.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name != "Button_Lock" || !button.gameObject.activeInHierarchy) continue;
                if (found == index) return button;
                found++;
            }
            return null;
        }

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

        public void ConfigureReroll(Button reroll, TMP_Text rerollText)
        {
            rerollButton = reroll; rerollLabel = rerollText;
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
            state.SetupEquipmentUpgrades(startConfig.EquipmentUpgrades);
            state.SetupPrepTime(startConfig.PrepTime);
            state.SetupHeroPromotion(startConfig.HeroPromotion);
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
            if (type == FacilityType.Shop) State.RestockShopIfNeeded(IsShopItemAvailable);
            if (type == FacilityType.Tavern) State.RestockTavernIfNeeded(IsHeroAvailable);
            OpenFacility = type;
            Message = string.Empty;
            chosenSquad = null;
            captainSquad = -1;
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
            if (facility.IsMaxLevel) { Message = "이미 최고 레벨입니다."; Rebuild(); return false; }
            Cost cost = facility.NextUpgradeCost;
            bool upgraded = State.TryUpgradeFacility(OpenFacility.Value);
            if (upgraded && OpenFacility == FacilityType.TrainingGround) battle.RunLink.ApplyTraining();
            Message = upgraded ? $"레벨 {facility.Level + 1}(으)로 업그레이드 · {Describe(cost)}" : "자금 부족: " + Describe(cost);
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
            Cost price = state.Shop.GetBuyPrice(item);
            bool bought = state.TryBuyAt(index);
            Message = bought ? $"{item.DisplayName} 구매 · {Describe(price)}" : $"{item.DisplayName}: 자금이 부족합니다.";
            Rebuild();
            return bought;
        }

        /// <summary>보관함의 index번째 아이템을 판다(D-5).</summary>
        public bool TrySell(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || index < 0 || index >= state.Inventory.Items.Count) return false;
            ItemInstance item = state.Inventory.Items[index];
            int price = state.Shop.GetInstanceSellPrice(item);
            bool sold = state.TrySell(item);
            if (sold) Message = $"{item.DisplayName} 판매 · {price}골드";
            Rebuild();
            return sold;
        }

        /// <summary>주점에서 고용할 영웅에게 대장을 맡길 부대 순번. -1이면 대기 영웅으로 고용한다.</summary>
        public int CaptainSquad => captainSquad;

        /// <summary>주점 매물의 index번째 영웅을 고른 부대 대장(없으면 대기)으로 고용한다(D-3). 비용을 내고 주점 앞에 나타난다.</summary>
        public bool TryHire(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Tavern || state == null || flow.Phase != GamePhase.Preparation) return false;
            if (index < 0 || index >= state.TavernOffers.Count) return false;
            HeroDefinition hero = state.TavernOffers[index];
            if (captainSquad >= state.Army.Squads.Count) captainSquad = -1;
            UnitCombatAI unit = null;
            HireCheck check = HireCheck.NotOffered;
            if (spawner != null) unit = spawner.HireAtTavern(hero, captainSquad, out check);
            Message = unit != null
                ? $"{hero.DisplayName} 고용 · {Describe(hero.HireCost)} → " + (captainSquad < 0 ? "예비 영웅" : $"{captainSquad + 1}부대 대장")
                : $"{hero.DisplayName}: " + Reason(check);
            Rebuild();
            return unit != null;
        }

        /// <summary>주점에서 고용할 영웅에게 대장을 맡길 부대를 고른다. -1이면 대기 영웅.</summary>
        public bool ChooseCaptainSquad(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Tavern || state == null || index < -1 || index >= state.Army.Squads.Count) return false;
            captainSquad = index;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        static string Reason(HireCheck check) => check switch
        {
            HireCheck.BattlePending => "전투 중에는 고용할 수 없습니다.",
            HireCheck.NoSquad => "대장을 맡길 부대가 없습니다.",
            HireCheck.NotEnoughFunds => "자금이 부족합니다.",
            _ => "고용하지 못했습니다.",
        };

        void BuildTavernRows(RunState state)
        {
            IReadOnlyList<HeroDefinition> offers = state.TavernOffers;
            if (offers.Count == 0) AddRow(offerList, "고용할 수 있는 영웅이 없습니다.", null);
            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{offers[i].DisplayName} · {offers[i].Tier}티어 · {Describe(offers[i].HireCost)}", () => TryHire(index));
            }
            if (captainSquad >= state.Army.Squads.Count) captainSquad = -1;
            AddRow(inventoryList, (captainSquad < 0 ? "[선택] " : string.Empty) + "예비 영웅 (전투 불참)", () => ChooseCaptainSquad(-1));
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            for (int i = 0; i < squads.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                string mark = i == captainSquad ? "[선택] " : string.Empty;
                string leader = squads[i].Leader is RunHero hero ? hero.Definition.DisplayName : "없음";
                AddRow(inventoryList, $"{mark}{i + 1}부대 대장 (현재: {leader})", () => ChooseCaptainSquad(index));
            }
        }

        /// <summary>방어 시설: 지금 성벽 위 성가퀴와 다음 레벨에서 바뀌는 모양(SIEGE-10).</summary>
        static string DescribeDefense(FacilityState facility)
        {
            var text = new StringBuilder("성벽 위: " + DescribeBattlement(facility.Current.battlementStyle));
            if (!facility.IsMaxLevel)
            {
                BattlementStyle next = facility.Definition.Levels[facility.Level + 1].battlementStyle;
                if (next != facility.Current.battlementStyle)
                    text.Append("\n다음 레벨: " + DescribeBattlement(next) + ". 모든 성벽에 한꺼번에 적용됩니다.");
            }
            text.Append("\n흉벽은 맨 위층 바깥 가장자리에 섭니다. 아군은 자기 흉벽 너머로 쏠 수 있습니다.");
            return text.ToString();
        }

        static string DescribeBattlement(BattlementStyle style) => style switch
        {
            BattlementStyle.Parapet => "낮은 난간 (다리를 가림)",
            BattlementStyle.Crenellated => "총안 흉벽 (돌출부가 화살과 시야를 막음)",
            _ => "none",
        };

        static string DescribeTavern(FacilityState facility)
        {
            var text = new StringBuilder("영웅 목록은 준비마다 바뀝니다. 대장을 바꾸면 이전 대장은 예비로 갑니다. 예비 영웅은 전투에 나가지 않습니다.");
            if (!facility.IsMaxLevel) text.Append("\n업그레이드하면 다음 준비부터 더 높은 티어의 영웅이 나옵니다.");
            return text.ToString();
        }

        /// <summary>골드를 내고 상점 매물을 새로 뽑는다(PREP-02). 리롤 버튼이 부른다.</summary>
        public bool TryReroll()
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || flow.Phase != GamePhase.Preparation) return false;
            int cost = state.ShopRerollCost;
            ShopRerollCheck check = state.CheckShopReroll();
            bool rerolled = check == ShopRerollCheck.Ok && state.TryRerollShop(IsShopItemAvailable);
            Message = rerolled ? $"목록 리롤 · {cost}골드" : "리롤: " + Reason(check, cost);
            Rebuild();
            return rerolled;
        }

        public void TryRerollFromButton() => TryReroll();

        static string Reason(ShopRerollCheck check, int cost) => check switch
        {
            ShopRerollCheck.BattlePending => "전투 중에는 리롤할 수 없습니다.",
            ShopRerollCheck.NotEnoughFunds => $"자금 부족: {cost}골드",
            _ => "지금은 리롤할 수 없습니다.",
        };

        /// <summary>상점 매물의 index번째 잠금을 토글한다(PREP-03). 각 매물 줄의 잠금 버튼이 부른다.</summary>
        public bool TryToggleLock(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || flow.Phase != GamePhase.Preparation) return false;
            if (index < 0 || index >= state.Shop.Stock.Count) return false;
            bool wasLocked = state.Shop.IsLocked(index);
            ShopLockCheck check = state.CheckShopLock(index);
            bool toggled = check == ShopLockCheck.Ok && state.TryToggleShopLock(index);
            ItemDefinition item = state.Shop.Stock[index];
            Message = toggled
                ? (wasLocked ? $"{item.DisplayName} 잠금 해제" : $"{item.DisplayName} 잠금 (최대 {state.ShopMaxLocked}개)")
                : Reason(check, state.ShopMaxLocked);
            Rebuild();
            return toggled;
        }

        static string Reason(ShopLockCheck check, int maxLocked) => check switch
        {
            ShopLockCheck.BattlePending => "전투 중에는 잠금을 바꿀 수 없습니다.",
            ShopLockCheck.LimitReached => $"잠금 한도: {maxLocked}개.",
            _ => "지금은 잠금을 바꿀 수 없습니다.",
        };

        UnlockCatalog Unlocks => startConfig != null ? startConfig.UnlockCatalog : null;

        // 해금 트랙(META-07)이 있으면 트랙을 보고, 없으면 예전 해금 플래그를 본다. 시설 레벨 조건은 풀이 따로 맡는다.
        bool IsAvailable(IUnlockable content, UnlockPayloadKind kind)
        {
            if (content == null) return false;
            UnlockCatalog catalog = Unlocks;
            return catalog != null
                ? UnlockTracks.IsContentAvailable(GameSession.Meta, catalog, kind, content.Id)
                : GameSession.Meta.IsUnlocked(content);
        }

        bool IsShopItemAvailable(ItemDefinition item) => IsAvailable(item, UnlockPayloadKind.ShopItem);
        bool IsRecipeAvailable(ItemDefinition item) => IsAvailable(item, UnlockPayloadKind.EquipmentRecipe);
        bool IsHeroAvailable(HeroDefinition hero) => IsAvailable(hero, UnlockPayloadKind.Hero);

        bool runReadyHandled;

        /// <summary>해금 항목 이름 바꾸기·환불(META-10). 고친 곳의 수. 무엇을 했는지는 개발자 로그에만 남긴다.</summary>
        public static int MigrateUnlocks(UnlockCatalog catalog)
        {
            if (catalog == null || GameSession.Meta == null) return 0;
            var log = new List<string>();
            int changes = UnlockTracks.Migrate(GameSession.Meta, catalog, catalog.Renames, log);
            if (log.Count > 0) Debug.Log("Meta save migrated (META-10):\n- " + string.Join("\n- ", log));
            return changes;
        }

        /// <summary>
        /// 런 데이터가 씬에 붙은 직후 한 번 부른다(META-07). 예전 해금 목록을 해금 트랙으로 옮기고,
        /// 새 런이면 열린 시작 보너스(골드·아이템·일반병)를 준다. 되살린 런에는 다시 주지 않는다.
        /// </summary>
        public void OnRunReady(bool restored)
        {
            if (runReadyHandled) return;
            runReadyHandled = true;
            UnlockCatalog catalog = Unlocks;
            RunState state = PreparedState();
            if (catalog == null || state == null) return;

            // 업데이트로 바뀌거나 없어진 해금 항목을 먼저 고친다(META-10).
            bool migrated = MigrateUnlocks(catalog) > 0;
            if (UnlockTracks.ImportLegacyUnlocks(GameSession.Meta, catalog) > 0 || migrated) GameSession.SaveMeta();
            if (restored) return;

            UnlockStartingBonus bonus = UnlockTracks.StartingBonus(GameSession.Meta, catalog);
            if (bonus.IsEmpty) return;
            if (bonus.Gold > 0) state.Wallet.AddGold(bonus.Gold);
            IRunContentLookup content = battle.RunLink.Content;
            foreach (KeyValuePair<string, int> entry in bonus.Items)
            {
                ItemDefinition item = content != null ? content.Item(entry.Key) : null;
                if (item == null) { Debug.LogWarning("Starting bonus item not found: " + entry.Key); continue; }
                for (int i = 0; i < entry.Value; i++) state.Inventory.Add(item);
            }
            SoldierDefinition grunt = StartingGrunt(state);
            for (int i = 0; i < bonus.Grunts; i++)
            {
                if (grunt == null || spawner == null || spawner.Recruit(grunt, FacilityType.TrainingGround) == null)
                {
                    Debug.LogWarning($"Starting bonus grunts: {bonus.Grunts - i} could not be added.");
                    break;
                }
            }
        }

        // 시작 보너스 일반병은 병영 시작 레벨에서 모집할 수 있는 첫 종류다.
        static SoldierDefinition StartingGrunt(RunState state)
        {
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            return soldiers != null && soldiers.Count > 0 ? soldiers[0] : null;
        }

        /// <summary>대장간 레시피의 index번째 장비를 만든다(D-6). 비용을 내고 보관함에 넣는다.</summary>
        public bool TryCraft(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Smithy || state == null || flow.Phase != GamePhase.Preparation) return false;
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsRecipeAvailable);
            if (index < 0 || index >= recipes.Count) return false;
            CraftingRecipe recipe = recipes[index];
            CraftCheck check = state.CheckCraft(recipe.item, IsRecipeAvailable);
            bool crafted = check == CraftCheck.Ok && state.TryCraft(recipe.item, IsRecipeAvailable) != null;
            Message = crafted ? $"{recipe.item.DisplayName} 제작 · {Describe(recipe.cost)}" : $"{recipe.item.DisplayName}: " + Reason(check);
            Rebuild();
            return crafted;
        }

        /// <summary>대장간에서 보관함 장비의 index번째(장비만 센 순번)를 판다(D-6).</summary>
        public bool TrySellEquipment(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Smithy || state?.Shop == null || flow.Phase != GamePhase.Preparation) return false;
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (index < 0 || index >= equipment.Count) return false;
            ItemInstance item = equipment[index];
            int price = state.Shop.GetInstanceSellPrice(item);
            bool sold = state.TrySellAtSmithy(item);
            if (sold) Message = $"{item.DisplayName} 판매 · {price}골드";
            Rebuild();
            return sold;
        }

        public bool TryUpgradeEquipment(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Smithy || state == null || flow.Phase != GamePhase.Preparation) return false;
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (index < 0 || index >= equipment.Count) return false;
            ItemInstance item = equipment[index];
            EquipmentUpgradeCheck check = state.CheckUpgradeEquipment(item);
            bool upgraded = check == EquipmentUpgradeCheck.Allowed && state.TryUpgradeEquipment(item);
            Message = upgraded ? $"{item.DisplayName} 강화" : $"{item.DisplayName}: " + UpgradeReason(check);
            Rebuild();
            return upgraded;
        }

        public Button EquipmentUpgradeButton(int index)
        {
            if (OpenFacility != FacilityType.Smithy || inventoryList == null || index < 0) return null;
            int found = 0;
            foreach (Transform child in inventoryList)
                if (child.gameObject.activeSelf && child.name == "Button_UpgradeEquipment")
                {
                    if (found++ == index) return child.GetComponent<Button>();
                }
            return null;
        }

        static string UpgradeReason(EquipmentUpgradeCheck check) => check switch
        {
            EquipmentUpgradeCheck.MaximumLevel => "이 대장간에서 할 수 있는 최고 강화 단계입니다.",
            EquipmentUpgradeCheck.NotEnoughFunds => "자금이 부족합니다.",
            EquipmentUpgradeCheck.NotOwned => "먼저 장비를 벗어 보관함에 넣으세요.",
            EquipmentUpgradeCheck.MissingCostData => "이 단계의 강화 재료 데이터가 없습니다.",
            EquipmentUpgradeCheck.NotConfigured => "강화 설정이 없습니다.",
            EquipmentUpgradeCheck.BattlePending => "전투 중에는 강화할 수 없습니다.",
            EquipmentUpgradeCheck.NotEnoughTime => "시간이 부족합니다.",
            _ => "이 장비는 강화할 수 없습니다.",
        };

        static List<ItemInstance> EquipmentIn(Inventory inventory)
        {
            var equipment = new List<ItemInstance>();
            foreach (ItemInstance item in inventory.Items)
                if (item.Definition.Kind == ItemKind.Equipment) equipment.Add(item);
            return equipment;
        }

        static string Reason(CraftCheck check) => check switch
        {
            CraftCheck.BattlePending => "전투 중에는 제작할 수 없습니다.",
            CraftCheck.NotCraftable => "이 대장간 레벨에서는 제작할 수 없습니다.",
            CraftCheck.NotEnoughFunds => "자금이 부족합니다.",
            CraftCheck.NotEnoughTime => "시간이 부족합니다.",
            _ => "제작하지 못했습니다.",
        };

        /// <summary>영웅의 전당에서 승급 대상 영웅(플레이어 제외)의 index번째를 한 등급 올린다(PREP-10). 비용과 준비 시간을 낸다.</summary>
        public bool TryPromote(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.HeroHall || state == null || flow.Phase != GamePhase.Preparation) return false;
            IReadOnlyList<RunHero> heroes = state.GradedHeroes;
            if (index < 0 || index >= heroes.Count) return false;
            RunHero hero = heroes[index];
            state.TryGetNextGradeStep(hero, out HeroGradeStep step);
            HeroPromoteCheck check = battle != null && battle.RunLink != null ? battle.RunLink.TryPromote(hero) : HeroPromoteCheck.NotConfigured;
            bool promoted = check == HeroPromoteCheck.Ok;
            Message = promoted ? $"{hero.Definition.DisplayName} {hero.Grade}등급으로 승급 · {Describe(step.cost)}"
                : $"{hero.Definition.DisplayName}: " + Reason(check);
            Rebuild();
            return promoted;
        }

        static string Reason(HeroPromoteCheck check) => check switch
        {
            HeroPromoteCheck.BattlePending => "전투 중에는 승급할 수 없습니다.",
            HeroPromoteCheck.MaxGrade => "이미 최고 등급입니다.",
            HeroPromoteCheck.NotEnoughExp => "EXP가 부족합니다. 영웅은 부대 대장으로 전투에서 이기면 EXP를 얻습니다.",
            HeroPromoteCheck.NotEnoughFunds => "자금이 부족합니다.",
            HeroPromoteCheck.NotEnoughTime => "시간이 부족합니다.",
            HeroPromoteCheck.NotConfigured => "승급 설정이 없습니다.",
            _ => "이 영웅은 승급할 수 없습니다.",
        };

        void BuildHeroHallRows(RunState state)
        {
            IReadOnlyList<RunHero> heroes = state.GradedHeroes;
            if (heroes.Count == 0) AddRow(offerList, "아직 영웅이 없습니다. 주점에서 고용하세요.", null);
            for (int i = 0; i < heroes.Count; i++)
            {
                int index = i;
                RunHero hero = heroes[i];
                string role = state.LeadingSquadOf(hero) is Squad<RunUnit> squad ? $"{state.IndexOf(squad) + 1}부대 대장" : "예비";
                bool canGrow = state.TryGetNextGradeStep(hero, out HeroGradeStep step);
                string detail = canGrow
                    ? $"EXP {hero.Exp}/{step.expRequired} · 승급 - {Describe(step.cost)}{TimeTag(state, state.PromotionHours)}"
                    : "최고 등급";
                // 최고 등급이 아니면 눌러서 모자란 이유(경험치·골드·시간)를 볼 수 있게 둔다.
                Button row = AddRow(offerList, $"{hero.Definition.DisplayName} · {hero.Grade}등급 · {role}\n{detail}",
                    canGrow ? () => TryPromote(index) : null);
                if (row != null) row.GetComponentInChildren<TMP_Text>(true).fontSize = 12f;
            }
            HeroPromotionSettings settings = state.HeroPromotion;
            if (settings == null) { AddRow(inventoryList, "승급 설정이 없습니다.", null); return; }
            AddRow(inventoryList, "1등급 위로 등급마다: " + DescribeModifiers(settings.BonusPerGrade), null);
            AddRow(inventoryList, $"등급이 오를 때마다 스킬 칸이 하나씩 열립니다 (최대 {settings.MaxGrade}).", null);
            AddRow(inventoryList, $"승리: 살아남은 대장에게 EXP +{settings.ExpPerVictory}", null);
        }

        static string DescribeHeroHall(RunState state) =>
            "영웅은 부대 대장으로 전투에서 이기면 EXP를 얻습니다. 예비 영웅은 얻지 못합니다.\n"
            + "EXP가 다 찬 영웅은 골드와 준비 시간을 써서 승급합니다. 영웅은 고용 티어와 같은 등급으로 시작합니다.";

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
            Message = unit != null ? $"{soldier.DisplayName} 모집 · {Describe(soldier.RecruitCost)} -> {squad + 1}부대"
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
            RecruitCheck.NotRecruitable => "이 훈련장 레벨에서는 모집할 수 없습니다.",
            RecruitCheck.NoSquad => "들어갈 부대가 없습니다 (부대가 가득 찼거나 부대 수 한도).",
            RecruitCheck.NotEnoughFunds => "자금이 부족합니다.",
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
                upgradeLabel.text = facility.IsMaxLevel ? "최고 레벨" : "업그레이드 - " + Describe(facility.NextUpgradeCost);
            if (upgradeButton != null) upgradeButton.interactable = !facility.IsMaxLevel;
            bool shop = type == FacilityType.Shop && state.Shop != null;
            bool barracks = type == FacilityType.TrainingGround;
            bool smithy = type == FacilityType.Smithy && state.Shop != null;
            bool tavern = type == FacilityType.Tavern;
            bool heroHall = type == FacilityType.HeroHall;
            if (body != null)
            {
                string text = shop ? $"목록은 준비마다 바뀌고, 업그레이드는 다음 준비의 목록부터 적용됩니다.\n리롤 비용은 다음 준비까지 계속 오릅니다. 잠금은 무료입니다 (최대 {state.ShopMaxLocked}개)."
                    : barracks ? DescribeTraining(facility)
                    : smithy ? DescribeSmithy(facility)
                    : tavern ? DescribeTavern(facility)
                    : heroHall ? DescribeHeroHall(state)
                    : type == FacilityType.Defense ? DescribeDefense(facility)
                    : Purpose(type) + "\n시설 기능과 업그레이드 효과는 나중에 추가됩니다.";
                body.text = string.IsNullOrEmpty(Message) ? text : Message + "\n" + text;
            }
            if (tradeArea != null) tradeArea.SetActive(shop || barracks || smithy || tavern || heroHall);
            if (rerollButton != null) rerollButton.gameObject.SetActive(shop);
            if (shop && rerollLabel != null) rerollLabel.text = $"리롤 - {state.ShopRerollCost}골드";
            if (offerHeader != null)
                offerHeader.text = barracks ? "모집 (클릭해서 모집)" : smithy ? "제작 (클릭해서 제작)"
                    : tavern ? "영웅 (클릭해서 고용)" : heroHall ? "영웅 (클릭해서 승급)" : shopOfferHeader;
            if (inventoryHeader != null)
                inventoryHeader.text = barracks ? "부대 (클릭해서 선택)" : smithy ? "장비 강화 / 판매"
                    : tavern ? "배치 (클릭해서 선택)" : heroHall ? "등급 보너스" : shopInventoryHeader;
            ClearRows();
            if (rowTemplate == null) return;
            if (barracks) { BuildBarracksRows(state); return; }
            if (tavern) { BuildTavernRows(state); return; }
            if (smithy) { BuildSmithyRows(state); return; }
            if (heroHall) { BuildHeroHallRows(state); return; }
            if (!shop) return;
            IReadOnlyList<ItemDefinition> stock = state.Shop.Stock;
            if (stock.Count == 0) AddRow(offerList, "살 수 있는 물건이 없습니다.", null);
            for (int i = 0; i < stock.Count; i++)
            {
                int index = i;
                bool isLocked = state.Shop.IsLocked(index);
                string prefix = isLocked ? "[잠금] " : string.Empty;
                AddShopOfferRow(offerList, $"{prefix}{stock[i].DisplayName} · {Describe(state.Shop.GetBuyPrice(stock[i]))}", isLocked,
                    () => TryBuy(index), () => TryToggleLock(index));
            }
            IReadOnlyList<ItemInstance> items = state.Inventory.Items;
            if (items.Count == 0) AddRow(inventoryList, "보관함이 비어 있습니다.", null);
            for (int i = 0; i < items.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                AddRow(inventoryList, $"{items[i].DisplayName} 판매 - {state.Shop.GetInstanceSellPrice(items[i])}골드",
                    () => TrySell(index));
            }
            if (items.Count > MaxInventoryRows) AddRow(inventoryList, $"그 밖에 {items.Count - MaxInventoryRows}개", null);
        }

        void BuildBarracksRows(RunState state)
        {
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            if (soldiers.Count == 0) AddRow(offerList, "모집할 수 있는 병사가 없습니다.", null);
            for (int i = 0; i < soldiers.Count; i++)
            {
                int index = i;
                if (soldiers[i] == null) continue;
                AddRow(offerList, $"{soldiers[i].DisplayName} · {Describe(soldiers[i].RecruitCost)}", () => TryRecruit(index));
            }
            if (chosenSquad != null && state.IndexOf(chosenSquad) < 0) chosenSquad = null;
            AddRow(inventoryList, (chosenSquad == null ? "[선택] " : string.Empty) + "자동 배치 (자리가 있는 첫 부대)", () => ChooseSquad(-1));
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            for (int i = 0; i < squads.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                string mark = squads[i] == chosenSquad ? "[선택] " : string.Empty;
                AddRow(inventoryList, $"{mark}{i + 1}부대 - {squads[i].Members.Count}/{squads[i].Capacity}명", () => ChooseSquad(index));
            }
            if (state.Army.CanAddSquad) AddRow(inventoryList, "모든 부대가 가득 차면 새 부대를 만듭니다", null);
        }

        void BuildSmithyRows(RunState state)
        {
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsRecipeAvailable);
            if (recipes.Count == 0) AddRow(offerList, "제작할 수 있는 장비가 없습니다.", null);
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{recipes[i].item.DisplayName} · {Describe(recipes[i].cost)}{TimeTag(state, state.PrepTime?.CraftHours ?? 0f)}",
                    () => TryCraft(index));
            }
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (equipment.Count == 0) AddRow(inventoryList, "판매할 장비가 없습니다.", null);
            for (int i = 0; i < equipment.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                ItemInstance item = equipment[i];
                EquipmentUpgradeCheck check = state.CheckUpgradeEquipment(item);
                string detail = check == EquipmentUpgradeCheck.MaximumLevel ? $"최대 +{state.MaxEquipmentUpgrade}"
                    : state.EquipmentUpgrades != null && state.EquipmentUpgrades.TryGetNextCost(item, out Cost cost)
                        ? $"+{item.UpgradeLevel + 1} 강화 - {Describe(cost)}{TimeTag(state, state.PrepTime?.UpgradeHours ?? 0f)}"
                        : UpgradeReason(check);
                Button row = AddRow(inventoryList, $"{item.DisplayName}\n{detail}",
                    check == EquipmentUpgradeCheck.Allowed || check == EquipmentUpgradeCheck.NotEnoughFunds
                        || check == EquipmentUpgradeCheck.NotEnoughTime
                        ? () => TryUpgradeEquipment(index) : null);
                if (row == null) continue;
                row.name = "Button_UpgradeEquipment";
                TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
                label.fontSize = 12f;
                label.rectTransform.offsetMax = new Vector2(-83f, 0f);
                AddEquipmentSellButton(row, state.Shop.GetInstanceSellPrice(item), () => TrySellEquipment(index));
            }
            if (equipment.Count > MaxInventoryRows) AddRow(inventoryList, $"그 밖에 {equipment.Count - MaxInventoryRows}개", null);
        }

        /// <summary>준비 시간을 쓰는 행동의 시간 표시(PREP-06). 시간 설정이 없거나 0시간이면 비운다.</summary>
        static string TimeTag(RunState state, float hours) =>
            state?.PrepTime != null && hours > 0f ? $" · {hours:0.#}시간" : string.Empty;

        string DescribeSmithy(FacilityState facility)
        {
            var text = new StringBuilder($"제작법으로 제작 / 보관함 클릭: 강화 (최대 +{State.MaxEquipmentUpgrade}) / 버튼: 판매\n착용 중인 장비는 먼저 벗으세요.");
            if (!facility.IsMaxLevel)
            {
                List<CraftingRecipe> next = facility.Definition.Levels[facility.Level + 1].recipes;
                text.Append($" 다음 레벨: 제작법 {(next != null ? next.Count : 0)}개");
            }
            return text.ToString();
        }

        static string DescribeTraining(FacilityState facility)
        {
            var text = new StringBuilder("훈련 보너스 (모든 병사): " + DescribeModifiers(SoldierTraining.ModifiersOf(facility)));
            if (!facility.IsMaxLevel)
                text.Append("\n다음 레벨: " + DescribeModifiers(facility.Definition.Levels[facility.Level + 1].soldierModifiers));
            text.Append("\n업그레이드는 지금 있는 병사에게도 바로 적용됩니다.");
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
            StatType.AimSpread => "탄퍼짐",
            _ => stat.ToString(),
        };

        // 매물·보관함 칸 위의 제목. 칸의 부모(열) 아래 Header 텍스트를 쓴다. 없으면 null.
        static TMP_Text HeaderOf(RectTransform list)
        {
            Transform header = list != null && list.parent != null ? list.parent.Find("Header") : null;
            return header != null ? header.GetComponent<TMP_Text>() : null;
        }

        Button AddRow(RectTransform list, string text, UnityEngine.Events.UnityAction action)
        {
            if (list == null) return null;
            Button row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>(true).text = text;
            row.interactable = action != null;
            if (action != null) row.onClick.AddListener(action);
            rows.Add(row);
            return row;
        }

        void AddEquipmentSellButton(Button row, int price, UnityEngine.Events.UnityAction onSell)
        {
            var buttonObject = new GameObject("Button_SellEquipment", typeof(RectTransform),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            buttonObject.transform.SetParent(row.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-4f, 0f);
            rect.sizeDelta = new Vector2(74f, 28f);
            var background = buttonObject.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.3f, 0.24f, 0.2f);
            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onSell);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rect, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = row.GetComponentInChildren<TMP_Text>().font;
            label.fontSize = 12f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = $"판매\n{price}골드";
        }

        void AddShopOfferRow(RectTransform list, string text, bool locked,
            UnityEngine.Events.UnityAction onBuy, UnityEngine.Events.UnityAction onToggleLock)
        {
            Button row = AddRow(list, text, onBuy);
            if (row == null) return;
            TMP_Text rowLabel = row.GetComponentInChildren<TMP_Text>(true);
            if (rowLabel != null) rowLabel.rectTransform.offsetMax = new Vector2(-78f, 0f);

            var lockObject = new GameObject("Button_Lock", typeof(RectTransform), typeof(Image), typeof(Button));
            lockObject.transform.SetParent(row.transform, false);
            var lockRect = lockObject.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(1f, 0.5f);
            lockRect.anchorMax = new Vector2(1f, 0.5f);
            lockRect.pivot = new Vector2(1f, 0.5f);
            lockRect.anchoredPosition = new Vector2(-4f, 0f);
            lockRect.sizeDelta = new Vector2(68f, 26f);

            var lockImage = lockObject.GetComponent<Image>();
            lockImage.color = locked ? new Color(0.55f, 0.42f, 0.14f) : new Color(0.22f, 0.32f, 0.38f);
            var lockButton = lockObject.GetComponent<Button>();
            lockButton.targetGraphic = lockImage;
            if (onToggleLock != null) lockButton.onClick.AddListener(onToggleLock);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(lockObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var lockLabel = labelObject.GetComponent<TextMeshProUGUI>();
            lockLabel.text = locked ? "잠금 해제" : "잠금";
            lockLabel.fontSize = 14f;
            lockLabel.alignment = TextAlignmentOptions.Center;
            lockLabel.raycastTarget = false;
            lockLabel.color = new Color(0.95f, 0.97f, 1f);
            if (rowLabel != null) lockLabel.font = rowLabel.font;
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
            FacilityType.Shop => "아이템 사기 / 팔기",
            FacilityType.TrainingGround => "병사 모집 / 훈련",
            FacilityType.Smithy => "장비 제작 / 강화 / 판매",
            FacilityType.Defense => "성벽 / 무기 준비",
            FacilityType.HeroHall => "영웅 승급",
            _ => string.Empty,
        };
    }
}
