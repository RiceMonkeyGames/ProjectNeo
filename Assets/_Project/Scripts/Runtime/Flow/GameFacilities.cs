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
            if (facility.IsMaxLevel) { Message = "Already at maximum level."; Rebuild(); return false; }
            Cost cost = facility.NextUpgradeCost;
            bool upgraded = State.TryUpgradeFacility(OpenFacility.Value);
            if (upgraded && OpenFacility == FacilityType.TrainingGround) battle.RunLink.ApplyTraining();
            Message = upgraded ? $"Upgraded to level {facility.Level + 1} · {Describe(cost)}" : "Not enough funds: " + Describe(cost);
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
            Message = bought ? $"{item.DisplayName} purchased · {Describe(price)}" : $"{item.DisplayName}: Not enough funds.";
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
            if (sold) Message = $"{item.DisplayName} sold · {price} gold";
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
                ? $"{hero.DisplayName} hired · {Describe(hero.HireCost)} → " + (captainSquad < 0 ? "Reserve hero" : $"Squad {captainSquad + 1} captain")
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
            HireCheck.BattlePending => "Cannot hire during battle.",
            HireCheck.NoSquad => "No squad available for a captain.",
            HireCheck.NotEnoughFunds => "Not enough funds.",
            _ => "Could not hire.",
        };

        void BuildTavernRows(RunState state)
        {
            IReadOnlyList<HeroDefinition> offers = state.TavernOffers;
            if (offers.Count == 0) AddRow(offerList, "No heroes available to hire.", null);
            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{offers[i].DisplayName} · {offers[i].Tier} tier · {Describe(offers[i].HireCost)}", () => TryHire(index));
            }
            if (captainSquad >= state.Army.Squads.Count) captainSquad = -1;
            AddRow(inventoryList, (captainSquad < 0 ? "[Selected] " : string.Empty) + "Reserve hero (out of battle)", () => ChooseCaptainSquad(-1));
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            for (int i = 0; i < squads.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                string mark = i == captainSquad ? "[Selected] " : string.Empty;
                string leader = squads[i].Leader is RunHero hero ? hero.Definition.DisplayName : "None";
                AddRow(inventoryList, $"{mark}Captain of squad {i + 1} (current: {leader})", () => ChooseCaptainSquad(index));
            }
        }

        static string DescribeTavern(FacilityState facility)
        {
            var text = new StringBuilder("Hero offers refresh each preparation. Replacing a captain sends the old captain to reserve. Reserve heroes do not enter battle.");
            if (!facility.IsMaxLevel) text.Append("\nUpgrades unlock higher tier heroes from the next preparation.");
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
            Message = rerolled ? $"Offers rerolled · {cost} gold" : "Reroll: " + Reason(check, cost);
            Rebuild();
            return rerolled;
        }

        public void TryRerollFromButton() => TryReroll();

        static string Reason(ShopRerollCheck check, int cost) => check switch
        {
            ShopRerollCheck.BattlePending => "Cannot reroll during battle.",
            ShopRerollCheck.NotEnoughFunds => $"Not enough funds: {cost} gold",
            _ => "Cannot reroll now.",
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
                ? (wasLocked ? $"{item.DisplayName} unlocked" : $"{item.DisplayName} locked (limit {state.ShopMaxLocked})")
                : Reason(check, state.ShopMaxLocked);
            Rebuild();
            return toggled;
        }

        static string Reason(ShopLockCheck check, int maxLocked) => check switch
        {
            ShopLockCheck.BattlePending => "Cannot change locks during battle.",
            ShopLockCheck.LimitReached => $"Lock limit: {maxLocked} offers.",
            _ => "Cannot change locks now.",
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

            if (UnlockTracks.ImportLegacyUnlocks(GameSession.Meta, catalog) > 0) GameSession.SaveMeta();
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
            Message = crafted ? $"{recipe.item.DisplayName} crafted · {Describe(recipe.cost)}" : $"{recipe.item.DisplayName}: " + Reason(check);
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
            if (sold) Message = $"{item.DisplayName} sold · {price} gold";
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
            Message = upgraded ? $"{item.DisplayName} upgraded" : $"{item.DisplayName}: " + UpgradeReason(check);
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
            EquipmentUpgradeCheck.MaximumLevel => "Maximum upgrade level for this smithy.",
            EquipmentUpgradeCheck.NotEnoughFunds => "Not enough funds.",
            EquipmentUpgradeCheck.NotOwned => "Unequip the item into the stash first.",
            EquipmentUpgradeCheck.MissingCostData => "Upgrade material data is missing for this level.",
            EquipmentUpgradeCheck.NotConfigured => "Upgrade settings are missing.",
            EquipmentUpgradeCheck.BattlePending => "Cannot upgrade during battle.",
            EquipmentUpgradeCheck.NotEnoughTime => "Not enough time.",
            _ => "This equipment cannot be upgraded.",
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
            CraftCheck.BattlePending => "Cannot craft during battle.",
            CraftCheck.NotCraftable => "Cannot craft at this smithy level.",
            CraftCheck.NotEnoughFunds => "Not enough funds.",
            CraftCheck.NotEnoughTime => "Not enough time.",
            _ => "Could not craft.",
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
            Message = promoted ? $"{hero.Definition.DisplayName} promoted to grade {hero.Grade} · {Describe(step.cost)}"
                : $"{hero.Definition.DisplayName}: " + Reason(check);
            Rebuild();
            return promoted;
        }

        static string Reason(HeroPromoteCheck check) => check switch
        {
            HeroPromoteCheck.BattlePending => "Cannot promote during battle.",
            HeroPromoteCheck.MaxGrade => "Already at the highest grade.",
            HeroPromoteCheck.NotEnoughExp => "Not enough EXP. Heroes earn EXP by winning battles as squad captains.",
            HeroPromoteCheck.NotEnoughFunds => "Not enough funds.",
            HeroPromoteCheck.NotEnoughTime => "Not enough time.",
            HeroPromoteCheck.NotConfigured => "Promotion settings are missing.",
            _ => "Cannot promote this hero.",
        };

        void BuildHeroHallRows(RunState state)
        {
            IReadOnlyList<RunHero> heroes = state.GradedHeroes;
            if (heroes.Count == 0) AddRow(offerList, "No heroes yet. Hire heroes at the tavern.", null);
            for (int i = 0; i < heroes.Count; i++)
            {
                int index = i;
                RunHero hero = heroes[i];
                string role = state.LeadingSquadOf(hero) is Squad<RunUnit> squad ? $"Squad {state.IndexOf(squad) + 1} captain" : "Reserve";
                bool canGrow = state.TryGetNextGradeStep(hero, out HeroGradeStep step);
                string detail = canGrow
                    ? $"EXP {hero.Exp}/{step.expRequired} · Promote - {Describe(step.cost)}{TimeTag(state, state.PromotionHours)}"
                    : "Highest grade";
                // 최고 등급이 아니면 눌러서 모자란 이유(경험치·골드·시간)를 볼 수 있게 둔다.
                Button row = AddRow(offerList, $"{hero.Definition.DisplayName} · Grade {hero.Grade} · {role}\n{detail}",
                    canGrow ? () => TryPromote(index) : null);
                if (row != null) row.GetComponentInChildren<TMP_Text>(true).fontSize = 12f;
            }
            HeroPromotionSettings settings = state.HeroPromotion;
            if (settings == null) { AddRow(inventoryList, "Promotion settings are missing.", null); return; }
            AddRow(inventoryList, "Per grade above 1: " + DescribeModifiers(settings.BonusPerGrade), null);
            AddRow(inventoryList, $"Each grade opens one more skill slot (max {settings.MaxGrade}).", null);
            AddRow(inventoryList, $"Victory: +{settings.ExpPerVictory} EXP to surviving captains", null);
        }

        static string DescribeHeroHall(RunState state) =>
            "Heroes gain EXP by winning battles as squad captains. Reserve heroes gain none.\n"
            + "Promote a hero with full EXP for gold and preparation time. A hero starts at its hire tier grade.";

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
            Message = unit != null ? $"{soldier.DisplayName} recruited · {Describe(soldier.RecruitCost)} -> Squad {squad + 1}"
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
            RecruitCheck.BattlePending => "Cannot recruit during battle.",
            RecruitCheck.NotRecruitable => "Cannot recruit at this barracks level.",
            RecruitCheck.NoSquad => "No squad available (squad full or squad limit reached).",
            RecruitCheck.NotEnoughFunds => "Not enough funds.",
            _ => "Could not recruit.",
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
                status.text = $"Level {facility.Level + 1} / {facility.Definition.Levels.Count}\n" + DescribeWallet(state.Wallet);
            if (upgradeLabel != null)
                upgradeLabel.text = facility.IsMaxLevel ? "Max level" : "Upgrade - " + Describe(facility.NextUpgradeCost);
            if (upgradeButton != null) upgradeButton.interactable = !facility.IsMaxLevel;
            bool shop = type == FacilityType.Shop && state.Shop != null;
            bool barracks = type == FacilityType.TrainingGround;
            bool smithy = type == FacilityType.Smithy && state.Shop != null;
            bool tavern = type == FacilityType.Tavern;
            bool heroHall = type == FacilityType.HeroHall;
            if (body != null)
            {
                string text = shop ? $"Offers refresh each preparation; upgrades apply to the next preparation's offers.\nReroll costs increase until the next preparation. Locking is free (limit {state.ShopMaxLocked})."
                    : barracks ? DescribeTraining(facility)
                    : smithy ? DescribeSmithy(facility)
                    : tavern ? DescribeTavern(facility)
                    : heroHall ? DescribeHeroHall(state)
                    : Purpose(type) + "\nFacility functions and upgrade effects will be added later.";
                body.text = string.IsNullOrEmpty(Message) ? text : Message + "\n" + text;
            }
            if (tradeArea != null) tradeArea.SetActive(shop || barracks || smithy || tavern || heroHall);
            if (rerollButton != null) rerollButton.gameObject.SetActive(shop);
            if (shop && rerollLabel != null) rerollLabel.text = $"Reroll - {state.ShopRerollCost} gold";
            if (offerHeader != null)
                offerHeader.text = barracks ? "Recruit (click to recruit)" : smithy ? "Craft (click to craft)"
                    : tavern ? "Heroes (click to hire)" : heroHall ? "Heroes (click to promote)" : shopOfferHeader;
            if (inventoryHeader != null)
                inventoryHeader.text = barracks ? "Squad (click to select)" : smithy ? "Upgrade / Sell equipment"
                    : tavern ? "Assignment (click to select)" : heroHall ? "Grade bonus" : shopInventoryHeader;
            ClearRows();
            if (rowTemplate == null) return;
            if (barracks) { BuildBarracksRows(state); return; }
            if (tavern) { BuildTavernRows(state); return; }
            if (smithy) { BuildSmithyRows(state); return; }
            if (heroHall) { BuildHeroHallRows(state); return; }
            if (!shop) return;
            IReadOnlyList<ItemDefinition> stock = state.Shop.Stock;
            if (stock.Count == 0) AddRow(offerList, "No offers available.", null);
            for (int i = 0; i < stock.Count; i++)
            {
                int index = i;
                bool isLocked = state.Shop.IsLocked(index);
                string prefix = isLocked ? "[Locked] " : string.Empty;
                AddShopOfferRow(offerList, $"{prefix}{stock[i].DisplayName} · {Describe(state.Shop.GetBuyPrice(stock[i]))}", isLocked,
                    () => TryBuy(index), () => TryToggleLock(index));
            }
            IReadOnlyList<ItemInstance> items = state.Inventory.Items;
            if (items.Count == 0) AddRow(inventoryList, "The stash is empty.", null);
            for (int i = 0; i < items.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                AddRow(inventoryList, $"{items[i].DisplayName} sell - {state.Shop.GetInstanceSellPrice(items[i])} gold",
                    () => TrySell(index));
            }
            if (items.Count > MaxInventoryRows) AddRow(inventoryList, $"{items.Count - MaxInventoryRows} more", null);
        }

        void BuildBarracksRows(RunState state)
        {
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            if (soldiers.Count == 0) AddRow(offerList, "No soldiers available to recruit.", null);
            for (int i = 0; i < soldiers.Count; i++)
            {
                int index = i;
                if (soldiers[i] == null) continue;
                AddRow(offerList, $"{soldiers[i].DisplayName} · {Describe(soldiers[i].RecruitCost)}", () => TryRecruit(index));
            }
            if (chosenSquad != null && state.IndexOf(chosenSquad) < 0) chosenSquad = null;
            AddRow(inventoryList, (chosenSquad == null ? "[Selected] " : string.Empty) + "Auto assign (first squad with room)", () => ChooseSquad(-1));
            IReadOnlyList<Squad<RunUnit>> squads = state.Army.Squads;
            for (int i = 0; i < squads.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                string mark = squads[i] == chosenSquad ? "[Selected] " : string.Empty;
                AddRow(inventoryList, $"{mark}Squad {i + 1} - {squads[i].Members.Count}/{squads[i].Capacity} units", () => ChooseSquad(index));
            }
            if (state.Army.CanAddSquad) AddRow(inventoryList, "A new squad is created if all squads are full", null);
        }

        void BuildSmithyRows(RunState state)
        {
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsRecipeAvailable);
            if (recipes.Count == 0) AddRow(offerList, "No equipment available to craft.", null);
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                AddRow(offerList, $"{recipes[i].item.DisplayName} · {Describe(recipes[i].cost)}{TimeTag(state, state.PrepTime?.CraftHours ?? 0f)}",
                    () => TryCraft(index));
            }
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            if (equipment.Count == 0) AddRow(inventoryList, "No equipment to sell.", null);
            for (int i = 0; i < equipment.Count && i < MaxInventoryRows; i++)
            {
                int index = i;
                ItemInstance item = equipment[i];
                EquipmentUpgradeCheck check = state.CheckUpgradeEquipment(item);
                string detail = check == EquipmentUpgradeCheck.MaximumLevel ? $"Max +{state.MaxEquipmentUpgrade}"
                    : state.EquipmentUpgrades != null && state.EquipmentUpgrades.TryGetNextCost(item, out Cost cost)
                        ? $"+{item.UpgradeLevel + 1} upgrade - {Describe(cost)}{TimeTag(state, state.PrepTime?.UpgradeHours ?? 0f)}"
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
            if (equipment.Count > MaxInventoryRows) AddRow(inventoryList, $"{equipment.Count - MaxInventoryRows} more", null);
        }

        /// <summary>준비 시간을 쓰는 행동의 시간 표시(PREP-06). 시간 설정이 없거나 0시간이면 비운다.</summary>
        static string TimeTag(RunState state, float hours) =>
            state?.PrepTime != null && hours > 0f ? $" · {hours:0.#}h" : string.Empty;

        string DescribeSmithy(FacilityState facility)
        {
            var text = new StringBuilder($"Craft from recipes / click stash: upgrade (max +{State.MaxEquipmentUpgrade}) / button: sell\nUnequip equipped items first.");
            if (!facility.IsMaxLevel)
            {
                List<CraftingRecipe> next = facility.Definition.Levels[facility.Level + 1].recipes;
                text.Append($" Next level: {(next != null ? next.Count : 0)} recipes");
            }
            return text.ToString();
        }

        static string DescribeTraining(FacilityState facility)
        {
            var text = new StringBuilder("Training bonus (all soldiers): " + DescribeModifiers(SoldierTraining.ModifiersOf(facility)));
            if (!facility.IsMaxLevel)
                text.Append("\nNext level: " + DescribeModifiers(facility.Definition.Levels[facility.Level + 1].soldierModifiers));
            text.Append("\nUpgrades apply to existing soldiers immediately.");
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
            return text.Length > 0 ? text.ToString() : "None";
        }

        static string StatName(StatType stat) => stat switch
        {
            StatType.MaxHealth => "Health",
            StatType.Attack => "Attack",
            StatType.Defense => "Defense",
            StatType.AttackSpeed => "Attack speed",
            StatType.MoveSpeed => "Move speed",
            StatType.AttackRange => "Range",
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
            label.text = $"Sell\n{price} gold";
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
            lockLabel.text = locked ? "Unlock" : "Lock";
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
            var text = new StringBuilder($"{Mathf.Max(0, cost.gold)} gold");
            if (cost.materials != null)
                foreach (MaterialAmount entry in cost.materials)
                    if (entry.material != null && entry.amount > 0) text.Append($" · {entry.material.DisplayName} {entry.amount}");
            return text.ToString();
        }

        static string DescribeWallet(Wallet wallet)
        {
            var text = new StringBuilder($"Gold {wallet.Gold}");
            foreach (KeyValuePair<MaterialDefinition, int> material in wallet.Materials)
                if (material.Key != null && material.Value > 0) text.Append($" · {material.Key.DisplayName} {material.Value}");
            return text.ToString();
        }

        static string Purpose(FacilityType type) => type switch
        {
            FacilityType.Tavern => "Hire heroes",
            FacilityType.Shop => "Buy / sell items",
            FacilityType.TrainingGround => "Recruit / train soldiers",
            FacilityType.Smithy => "Craft / upgrade / sell equipment",
            FacilityType.Defense => "Prepare walls / weapons",
            FacilityType.HeroHall => "Promote heroes",
            _ => string.Empty,
        };
    }
}
