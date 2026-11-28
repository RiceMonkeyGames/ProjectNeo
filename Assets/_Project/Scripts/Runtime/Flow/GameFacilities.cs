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
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 시설 팝업(D-0·D-1·D-5, UI-13 C안). 런 데이터에 시설 상태·상점 창구를 붙이고, 건물에서 F로 시설 팝업을 열며
    /// Esc나 닫기 버튼으로 닫는다. 팝업이 열린 동안은 <see cref="GamePhaseView.IsPanelView"/>로 이동·시점을 멈춘다.
    /// 화면은 I·O·K 창과 같은 금테 판이다(<see cref="FacilityWindowView"/>): 왼쪽 타일 격자에서 하나를 고르면 오른쪽 상세에 보이고,
    /// 상세의 단추가 행동(구매·고용·모집·제작·강화·승급·판매)을 한다. 아무것도 고르지 않으면 상세는 시설 설명을 보여 준다.
    /// 상점과 대장간은 격자 위 탭으로 매물과 보관함을 바꿔 본다. 모든 시설은 위 줄 단추로 업그레이드한다.
    /// 주점(D-3)에서 고용한 영웅은 모두 예비 영웅이 되고, 대장은 편성 창(O)에서 정한다(UI-13, 사용자 2026-11-24).
    /// 시설 목록·거래 수치는 <see cref="RunConfig"/> 에셋, 레벨·매물 풀은 시설 정의 에셋에서 바꾼다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameFacilities : MonoBehaviour
    {
        [SerializeField] RunConfig startConfig;
        /// <summary>새 런의 시작 설정(시설·거래·강화 수치). 저장된 런을 되살릴 때도 같은 수치를 쓴다(FLOW-02).</summary>
        public RunConfig StartConfig => startConfig;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] FacilityWindowView window;
        readonly List<FacilityTileView> tiles = new List<FacilityTileView>();
        readonly List<Entry> offerEntries = new List<Entry>();
        readonly List<Entry> storageEntries = new List<Entry>();
        GameBattleController battle;
        GameUnitSpawner spawner;
        GameFlow flow;
        PreparationInteraction interaction;
        int selected = -1;

        // 타일 하나와 그 상세. 행동이 없으면 단추를 숨긴다.
        sealed class Entry
        {
            public string Name, Sub, Tag, Price, Detail;
            public Sprite Icon;
            /// <summary>썸네일 옆 체력·공격력·방어력 줄에 보일 값(영웅·병사). null이면 줄을 숨긴다.</summary>
            public IReadOnlyList<StatValue> Stats;
            public bool Dim;
            public string Action;
            public UnityAction OnAction;
            public bool CanAct = true;
            public string Second;
            public UnityAction OnSecond;
        }

        /// <summary>지금 열린 시설. 닫혀 있으면 null.</summary>
        public FacilityType? OpenFacility { get; private set; }
        public bool IsOpen => OpenFacility.HasValue;
        /// <summary>마지막 거래·업그레이드 결과 안내.</summary>
        public string Message { get; private set; } = string.Empty;
        public string TitleText => window != null && window.title != null ? window.title.text : string.Empty;
        public string StatusText => window != null && window.status != null ? window.status.text : string.Empty;
        /// <summary>안내 한 줄과 상세 본문을 이어 읽는다. 고른 타일이 없으면 본문은 시설 설명이다.</summary>
        public string BodyText
        {
            get
            {
                string detail = window != null && window.detailBody != null ? window.detailBody.text : string.Empty;
                return string.IsNullOrEmpty(Message) ? detail : Message + "\n" + detail;
            }
        }
        /// <summary>상세 칸 제목(고른 타일의 이름이나 시설 용도).</summary>
        public string DetailTitleText => window != null && window.detailTitle != null ? window.detailTitle.text : string.Empty;
        public string UpgradeText => window != null && window.upgradeLabel != null ? window.upgradeLabel.text : string.Empty;
        /// <summary>리롤 버튼 글자(상점·주점). 버튼이 없으면 빈 글자.</summary>
        public string RerollText => window != null && window.rerollLabel != null ? window.rerollLabel.text : string.Empty;
        /// <summary>리롤 버튼이 지금 보이는지(상점·주점 팝업에서만 보인다).</summary>
        public bool IsRerollVisible => window != null && window.rerollButton != null && window.rerollButton.gameObject.activeSelf;
        /// <summary>리롤 버튼을 누를 수 있는지(주점은 골드가 모자라거나 모든 칸이 잠기면 꺼진다, PREP-12).</summary>
        public bool IsRerollInteractable => IsRerollVisible && window.rerollButton.interactable;
        public int TavernMaxLocked => State != null ? State.TavernMaxLocked : 0;
        public int TavernLockedCount => State != null ? State.TavernLockedCount : 0;
        /// <summary>매물 쪽 타일 수(상점 매물, 주점 영웅, 병영 병사, 대장간 제작법, 영웅의 전당 영웅).</summary>
        public int OfferRows => offerEntries.Count;
        /// <summary>보관함 쪽 타일 수(상점: 보관함 전체, 대장간: 장비). 다른 시설은 0.</summary>
        public int InventoryRows => storageEntries.Count;
        /// <summary>매물 쪽 제목(상점: 매물, 병영: 모집, 대장간: 제작, 주점: 고용, 영웅의 전당: 승급).</summary>
        public string OfferHeaderText => OpenFacility.HasValue ? OfferCaption(OpenFacility.Value) : string.Empty;
        /// <summary>격자가 보관함을 보여 주는가(상점·대장간 탭).</summary>
        public bool IsStorageView { get; private set; }
        /// <summary>지금 보이는 격자에서 고른 타일 순번. 없으면 -1.</summary>
        public int SelectedIndex => selected;
        /// <summary>상세 칸의 주 행동 단추(구매·고용·모집·제작·강화·승급·판매). 고른 타일이 없으면 숨겨진다.</summary>
        public UnityEngine.UI.Button ActionButton => window != null ? window.primaryButton : null;
        public string ActionText => window != null && window.primaryLabel != null ? window.primaryLabel.text : string.Empty;
        /// <summary>상세 칸의 둘째 단추(상점 잠금, 대장간 판매). 쓰지 않으면 숨겨진다.</summary>
        public UnityEngine.UI.Button SecondButton => window != null ? window.secondaryButton : null;
        public string SecondText => window != null && window.secondaryLabel != null ? window.secondaryLabel.text : string.Empty;
        /// <summary>열린 시설(상점 PREP-03, 주점 PREP-12)의 index번째 매물이 잠겨 있는지.</summary>
        public bool IsOfferLocked(int index) => OpenFacility == FacilityType.Tavern
            ? State != null && State.IsTavernLocked(index)
            : State?.Shop != null && State.Shop.IsLocked(index);
        public int ShopMaxLocked => State != null ? State.ShopMaxLocked : 0;
        public int ShopLockedCount => State?.Shop != null ? State.Shop.LockedCount : 0;

        public void Configure(RunConfig start, GamePhaseView view, FacilityWindowView popup)
        {
            startConfig = start; phaseView = view; window = popup;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            spawner = GetComponent<GameUnitSpawner>();
            flow = GameSession.Flow;
            if (window != null)
            {
                window.gameObject.SetActive(false);
                if (window.tileTemplate != null) window.tileTemplate.gameObject.SetActive(false);
                Listen(window.upgradeButton, TryUpgradeFromButton);
                Listen(window.rerollButton, TryRerollFromButton);
                Listen(window.closeButton, Close);
                Listen(window.offersTab, () => ShowOffers());
                Listen(window.storageTab, () => ShowStorage());
                Listen(window.primaryButton, ActFromButton);
                Listen(window.secondaryButton, SecondFromButton);
            }
        }

        // 연 F로 다시 닫는다(UI-13, 사용자 2026-11-24). 같은 장면의 상호작용을 한 번 찾아 듣는다.
        void OnEnable()
        {
            if (interaction == null) interaction = FindAnyObjectByType<PreparationInteraction>();
            if (interaction != null) interaction.InteractKeyInPanel += OnInteractKeyInPanel;
        }

        void OnDisable()
        {
            if (interaction != null) interaction.InteractKeyInPanel -= OnInteractKeyInPanel;
        }

        void OnInteractKeyInPanel()
        {
            if (IsOpen) Close();
        }

        static void Listen(UnityEngine.UI.Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
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
            if (flow.Phase != GamePhase.Preparation || phaseView == null || window == null) return false;
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
            IsStorageView = false;
            selected = -1;
            PopupBackdrop.Attach(window.gameObject);
            window.gameObject.SetActive(true);
            Rebuild();
            return true;
        }

        /// <summary>팝업을 닫고 1인칭 탐색으로 돌아간다. 닫기 버튼도 이것을 부른다.</summary>
        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        /// <summary>격자를 매물 쪽으로 바꾼다. 고른 타일은 풀린다.</summary>
        public bool ShowOffers() => SwitchView(false);

        /// <summary>격자를 보관함 쪽으로 바꾼다(상점·대장간만). 고른 타일은 풀린다.</summary>
        public bool ShowStorage() => HasStorage && SwitchView(true);

        bool HasStorage => OpenFacility == FacilityType.Shop || OpenFacility == FacilityType.Smithy;

        bool SwitchView(bool storage)
        {
            if (!IsOpen) return false;
            IsStorageView = storage;
            selected = -1;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>지금 보이는 격자의 index번째 타일을 고른다. 이미 고른 타일을 다시 고르면 풀린다. 없는 번호면 false.</summary>
        public bool Select(int index)
        {
            List<Entry> entries = IsStorageView ? storageEntries : offerEntries;
            if (!IsOpen || index < 0 || index >= entries.Count) return false;
            selected = selected == index ? -1 : index;
            Message = string.Empty;
            Rebuild();
            return true;
        }

        /// <summary>고른 타일의 주 행동을 한다. 고른 타일이 없거나 할 수 없으면 false.</summary>
        public bool Act()
        {
            Entry entry = SelectedEntry;
            if (entry?.OnAction == null || !entry.CanAct) return false;
            LastActionSucceeded = false;
            entry.OnAction();
            return LastActionSucceeded;
        }

        /// <summary>고른 타일의 둘째 행동(상점 잠금, 대장간 판매)을 한다.</summary>
        public bool ActSecond()
        {
            Entry entry = SelectedEntry;
            if (entry?.OnSecond == null) return false;
            LastActionSucceeded = false;
            entry.OnSecond();
            return LastActionSucceeded;
        }

        void ActFromButton() => Act();
        void SecondFromButton() => ActSecond();

        // 상세 단추는 값을 돌려받지 않으므로 마지막 Try 결과를 남겨 둔다.
        bool LastActionSucceeded { get; set; }

        Entry SelectedEntry
        {
            get
            {
                List<Entry> entries = IsStorageView ? storageEntries : offerEntries;
                return selected >= 0 && selected < entries.Count ? entries[selected] : null;
            }
        }

        bool Done(bool result)
        {
            LastActionSucceeded = result;
            Rebuild();
            return result;
        }

        // 고른 것이 목록에서 사라지는 행동(구매·판매·고용) 뒤에는 선택을 푼다.
        bool DoneAndRelease(bool result)
        {
            if (result) selected = -1;
            return Done(result);
        }

        /// <summary>열린 시설을 한 레벨 올린다(D-1). 업그레이드 버튼이 부른다.</summary>
        public bool TryUpgrade()
        {
            if (!IsOpen || flow.Phase != GamePhase.Preparation) return false;
            FacilityState facility = Facility(OpenFacility.Value);
            if (facility == null) return false;
            if (facility.IsMaxLevel) { Message = "이미 최고 레벨입니다."; return Done(false); }
            Cost cost = facility.NextUpgradeCost;
            FacilityUpgradeCheck check = State.CheckUpgradeFacility(OpenFacility.Value);
            bool upgraded = check == FacilityUpgradeCheck.Ok && State.TryUpgradeFacility(OpenFacility.Value);
            if (upgraded && OpenFacility == FacilityType.TrainingGround) battle.RunLink.ApplyTraining();
            Message = upgraded ? $"레벨 {facility.Level + 1}(으)로 업그레이드 · {Describe(cost)}"
                : check == FacilityUpgradeCheck.NotEnoughTime ? "시간이 부족합니다." : "자금 부족: " + Describe(cost);
            return Done(upgraded);
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
            return DoneAndRelease(bought);
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
            return DoneAndRelease(sold);
        }

        /// <summary>주점 매물의 index번째 영웅을 예비 영웅으로 고용한다(D-3, UI-13). 비용을 내고 주점 앞에 나타난다.</summary>
        public bool TryHire(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.Tavern || state == null || flow.Phase != GamePhase.Preparation) return false;
            if (index < 0 || index >= state.TavernOffers.Count) return false;
            HeroDefinition hero = state.TavernOffers[index];
            UnitCombatAI unit = null;
            HireCheck check = HireCheck.NotOffered;
            if (spawner != null) unit = spawner.HireAtTavern(hero, -1, out check);
            Message = unit != null
                ? $"{hero.DisplayName} 고용 → 예비 영웅"
                : $"{hero.DisplayName}: " + Reason(check);
            return DoneAndRelease(unit != null);
        }

        static string Reason(HireCheck check) => check switch
        {
            HireCheck.BattlePending => "전투 중에는 고용할 수 없습니다.",
            HireCheck.NoSquad => "대장을 맡길 부대가 없습니다.",
            HireCheck.NotEnoughFunds => "자금이 부족합니다.",
            _ => "고용하지 못했습니다.",
        };

        /// <summary>골드를 내고 열린 시설의 매물을 새로 뽑는다(상점 PREP-02, 주점 PREP-12). 리롤 버튼이 부른다.</summary>
        public bool TryReroll()
        {
            RunState state = State;
            if (OpenFacility == FacilityType.Tavern) return TryRerollTavern(state);
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || flow.Phase != GamePhase.Preparation) return false;
            int cost = state.ShopRerollCost;
            ShopRerollCheck check = state.CheckShopReroll();
            bool rerolled = check == ShopRerollCheck.Ok && state.TryRerollShop(IsShopItemAvailable);
            Message = rerolled ? $"목록 리롤 · {cost}골드" : "리롤: " + Reason(check, cost);
            if (rerolled && !IsStorageView) selected = -1;
            return Done(rerolled);
        }

        public void TryRerollFromButton() => TryReroll();

        bool TryRerollTavern(RunState state)
        {
            if (state == null || flow.Phase != GamePhase.Preparation) return false;
            int cost = state.TavernRerollCost;
            TavernRerollCheck check = state.CheckTavernReroll();
            bool rerolled = check == TavernRerollCheck.Ok && state.TryRerollTavern(IsHeroAvailable);
            Message = rerolled ? $"영웅 목록 리롤 · {cost}골드" : "리롤: " + Reason(check, cost);
            if (rerolled) selected = -1;
            return Done(rerolled);
        }

        static string Reason(TavernRerollCheck check, int cost) => check switch
        {
            TavernRerollCheck.BattlePending => "전투 중에는 리롤할 수 없습니다.",
            TavernRerollCheck.NoOpenSlot => "모든 칸이 잠겨 있습니다.",
            TavernRerollCheck.NotEnoughFunds => $"자금 부족: {cost}골드",
            _ => "지금은 리롤할 수 없습니다.",
        };

        static string Reason(ShopRerollCheck check, int cost) => check switch
        {
            ShopRerollCheck.BattlePending => "전투 중에는 리롤할 수 없습니다.",
            ShopRerollCheck.NotEnoughFunds => $"자금 부족: {cost}골드",
            _ => "지금은 리롤할 수 없습니다.",
        };

        /// <summary>열린 시설 매물의 index번째 잠금을 토글한다(상점 PREP-03, 주점 PREP-12). 매물 상세의 잠금 단추가 부른다.</summary>
        public bool TryToggleLock(int index)
        {
            RunState state = State;
            if (OpenFacility == FacilityType.Tavern) return TryToggleTavernLock(state, index);
            if (OpenFacility != FacilityType.Shop || state?.Shop == null || flow.Phase != GamePhase.Preparation) return false;
            if (index < 0 || index >= state.Shop.Stock.Count) return false;
            bool wasLocked = state.Shop.IsLocked(index);
            ShopLockCheck check = state.CheckShopLock(index);
            bool toggled = check == ShopLockCheck.Ok && state.TryToggleShopLock(index);
            ItemDefinition item = state.Shop.Stock[index];
            Message = toggled
                ? (wasLocked ? $"{item.DisplayName} 잠금 해제" : $"{item.DisplayName} 잠금 (최대 {state.ShopMaxLocked}개)")
                : Reason(check, state.ShopMaxLocked);
            return Done(toggled);
        }

        bool TryToggleTavernLock(RunState state, int index)
        {
            if (state == null || flow.Phase != GamePhase.Preparation) return false;
            if (index < 0 || index >= state.TavernOffers.Count) return false;
            bool wasLocked = state.IsTavernLocked(index);
            TavernLockCheck check = state.CheckTavernLock(index);
            bool toggled = check == TavernLockCheck.Ok && state.TryToggleTavernLock(index);
            HeroDefinition hero = state.TavernOffers[index];
            Message = toggled
                ? (wasLocked ? $"{hero.DisplayName} 잠금 해제" : $"{hero.DisplayName} 잠금 (최대 {state.TavernMaxLocked}명)")
                : check switch
                {
                    TavernLockCheck.BattlePending => "전투 중에는 잠금을 바꿀 수 없습니다.",
                    TavernLockCheck.LimitReached => $"잠금 한도: {state.TavernMaxLocked}명.",
                    _ => "지금은 잠금을 바꿀 수 없습니다.",
                };
            return Done(toggled);
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
            return Done(crafted);
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
            return DoneAndRelease(sold);
        }

        /// <summary>대장간에서 보관함 장비의 index번째(장비만 센 순번)를 한 단계 강화한다(PREP-01).</summary>
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
            return Done(upgraded);
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
            return Done(promoted);
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

        /// <summary>병영에서 모집할 수 있는 잡졸의 index번째를 모집한다(D-4). 비용을 낸다. 새 잡졸은 보관 병사가 된다(UI-07).</summary>
        public bool TryRecruit(int index)
        {
            RunState state = State;
            if (OpenFacility != FacilityType.TrainingGround || state == null || flow.Phase != GamePhase.Preparation) return false;
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            if (index < 0 || index >= soldiers.Count || soldiers[index] == null) return false;
            SoldierDefinition soldier = soldiers[index];
            RecruitCheck check = state.CheckBarracksRecruit(soldier);
            UnitCombatAI unit = null;
            if (check == RecruitCheck.Ok && spawner != null) unit = spawner.RecruitAtBarracks(soldier);
            Message = unit != null ? $"{soldier.DisplayName} 모집 · {Describe(soldier.RecruitCost)} -> 보관"
                : $"{soldier.DisplayName}: " + Reason(check);
            return Done(unit != null);
        }

        static string Reason(RecruitCheck check) => check switch
        {
            RecruitCheck.BattlePending => "전투 중에는 모집할 수 없습니다.",
            RecruitCheck.NotRecruitable => "이 훈련장 레벨에서는 모집할 수 없습니다.",
            RecruitCheck.NotEnoughFunds => "자금이 부족합니다.",
            _ => "모집하지 못했습니다.",
        };

        void Hide()
        {
            OpenFacility = null;
            selected = -1;
            if (window != null) window.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!OpenFacility.HasValue) return;
            // Esc(PreparationInteraction)나 단계 전환으로 팝업 화면이 끝나면 함께 닫는다.
            if (phaseView == null || !phaseView.IsPanelView) Hide();
        }

        // ---- 화면 ----

        void Rebuild()
        {
            FacilityState facility = OpenFacility.HasValue ? Facility(OpenFacility.Value) : null;
            if (facility == null || window == null) return;
            RunState state = State;
            FacilityType type = facility.Definition.Type;

            if (window.title != null) window.title.text = facility.Definition.DisplayName;
            if (window.status != null)
                window.status.text = $"레벨 {facility.Level + 1} / {facility.Definition.Levels.Count} · " + DescribeWallet(state.Wallet);
            if (window.upgradeLabel != null)
                window.upgradeLabel.text = facility.IsMaxLevel ? "최고 레벨"
                    : "업그레이드 · " + Describe(facility.NextUpgradeCost) + WorkTime(state, state.FacilityUpgradeHours, RunState.DirectActionArtisans);
            if (window.upgradeButton != null) window.upgradeButton.interactable = !facility.IsMaxLevel;

            offerEntries.Clear();
            storageEntries.Clear();
            switch (type)
            {
                case FacilityType.Shop: if (state.Shop != null) ShopEntries(state); break;
                case FacilityType.Tavern: TavernEntries(state); break;
                case FacilityType.TrainingGround: BarracksEntries(state); break;
                case FacilityType.Smithy: if (state.Shop != null) SmithyEntries(state); break;
                case FacilityType.HeroHall: HeroHallEntries(state); break;
            }
            if (!HasStorage) IsStorageView = false;
            List<Entry> visible = IsStorageView ? storageEntries : offerEntries;
            if (selected >= visible.Count) selected = -1;

            // 왼쪽 칸 위: 상점·대장간은 탭 두 개, 나머지는 제목과 수.
            bool tabs = HasStorage;
            if (window.header != null)
            {
                window.header.gameObject.SetActive(!tabs);
                window.header.text = OfferCaption(type);
            }
            if (window.count != null)
            {
                window.count.gameObject.SetActive(!tabs);
                window.count.text = offerEntries.Count.ToString();
            }
            if (window.offersTab != null) window.offersTab.gameObject.SetActive(tabs);
            if (window.storageTab != null) window.storageTab.gameObject.SetActive(tabs);
            if (window.offersTabLabel != null) window.offersTabLabel.text = $"{OfferCaption(type)} {offerEntries.Count}";
            if (window.storageTabLabel != null) window.storageTabLabel.text = $"{StorageCaption(type)} {storageEntries.Count}";
            if (tabs) window.MarkTab(IsStorageView);

            ClearTiles();
            if (window.offerScroll != null) window.offerScroll.SetActive(!IsStorageView);
            if (window.storageScroll != null) window.storageScroll.SetActive(IsStorageView);
            FillGrid(window.offerGrid, offerEntries, !IsStorageView);
            FillGrid(window.storageGrid, storageEntries, IsStorageView);
            if (window.emptyLabel != null)
            {
                window.emptyLabel.gameObject.SetActive(visible.Count == 0);
                window.emptyLabel.text = EmptyText(type, IsStorageView);
            }

            ShowDetail(SelectedEntry, facility, state);
            if (window.notice != null) window.notice.text = Message;
            bool tavern = type == FacilityType.Tavern;
            bool rerolls = (type == FacilityType.Shop && state.Shop != null) || tavern;
            if (window.rerollButton != null)
            {
                window.rerollButton.gameObject.SetActive(rerolls);
                // 주점(PREP-12): 골드가 모자라거나 모든 칸이 잠겼으면 누를 수 없다.
                window.rerollButton.interactable = !tavern || state.CheckTavernReroll() == TavernRerollCheck.Ok;
            }
            if (rerolls && window.rerollLabel != null)
                window.rerollLabel.text = $"리롤 · {(tavern ? state.TavernRerollCost : state.ShopRerollCost)}골드";
        }

        void FillGrid(RectTransform grid, List<Entry> entries, bool showing)
        {
            if (grid == null || window.tileTemplate == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                Entry entry = entries[i];
                FacilityTileView tile = Instantiate(window.tileTemplate, grid);
                tile.gameObject.SetActive(true);
                tile.Show(entry.Name, entry.Sub, entry.Icon, entry.Tag, showing && index == selected, entry.Dim, () => Select(index));
                tiles.Add(tile);
            }
        }

        void ShowDetail(Entry entry, FacilityState facility, RunState state)
        {
            FacilityType type = facility.Definition.Type;
            if (window.detailTitle != null) window.detailTitle.text = entry != null ? entry.Name : Purpose(type);
            if (window.detailPrice != null) window.detailPrice.text = entry != null ? entry.Price ?? string.Empty : string.Empty;
            if (window.detailThumbnail != null) window.detailThumbnail.SetActive(entry != null);
            if (window.detailIcon != null)
            {
                window.detailIcon.sprite = entry?.Icon;
                window.detailIcon.enabled = entry?.Icon != null;
            }
            if (window.statsRoot != null) window.statsRoot.SetActive(entry?.Stats != null);
            if (entry?.Stats != null && window.statValues != null)
            {
                StatType[] shown = { StatType.MaxHealth, StatType.Attack, StatType.Defense };
                for (int i = 0; i < shown.Length && i < window.statValues.Length; i++)
                    if (window.statValues[i] != null) window.statValues[i].text = StatText(entry.Stats, shown[i]);
            }
            if (window.detailBody != null) window.detailBody.text = entry != null ? entry.Detail ?? string.Empty : FacilityInfo(type, facility, state);
            ShowButton(window.primaryButton, window.primaryLabel, entry?.OnAction != null ? entry.Action : null, entry != null && entry.CanAct);
            ShowButton(window.secondaryButton, window.secondaryLabel, entry?.OnSecond != null ? entry.Second : null, true);
        }

        static void ShowButton(UnityEngine.UI.Button button, TMPro.TMP_Text label, string caption, bool interactable)
        {
            if (button == null) return;
            button.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            button.interactable = interactable;
            if (label != null) label.text = caption ?? string.Empty;
        }

        void ClearTiles()
        {
            foreach (FacilityTileView tile in tiles)
            {
                if (tile == null) continue;
                // 같은 프레임에 다시 그리므로 먼저 끈다.
                tile.gameObject.SetActive(false);
                Destroy(tile.gameObject);
            }
            tiles.Clear();
        }

        /// <summary>지금 그려진 타일(테스트용). 보이지 않는 격자의 타일도 들어 있다.</summary>
        public int TileCount => tiles.Count;

        static string OfferCaption(FacilityType type) => type switch
        {
            FacilityType.Shop => "매물",
            FacilityType.Tavern => "고용",
            FacilityType.TrainingGround => "모집",
            FacilityType.Smithy => "제작",
            FacilityType.HeroHall => "승급",
            _ => string.Empty,
        };

        static string StorageCaption(FacilityType type) => type == FacilityType.Smithy ? "장비" : "보관함";

        static string EmptyText(FacilityType type, bool storage) => storage
            ? (type == FacilityType.Smithy ? "강화하거나 팔 장비가 없습니다." : "보관함이 비어 있습니다.")
            : type switch
            {
                FacilityType.Shop => "살 수 있는 물건이 없습니다.",
                FacilityType.Tavern => "고용할 수 있는 영웅이 없습니다.",
                FacilityType.TrainingGround => "모집할 수 있는 병사가 없습니다.",
                FacilityType.Smithy => "제작할 수 있는 장비가 없습니다.",
                FacilityType.HeroHall => "아직 영웅이 없습니다. 주점에서 고용하세요.",
                _ => string.Empty,
            };

        // 아무것도 고르지 않았을 때 상세에 보이는 시설 설명.
        string FacilityInfo(FacilityType type, FacilityState facility, RunState state) => type switch
        {
            FacilityType.Shop => $"매물은 준비마다 바뀝니다.\n잠근 매물은 리롤해도 남습니다 (최대 {state.ShopMaxLocked}개).",
            FacilityType.TrainingGround => DescribeTraining(facility) + $"\n보관 병사 {state.StoredSoldiers.Count}명",
            FacilityType.Smithy => DescribeSmithy(facility),
            FacilityType.Tavern => DescribeTavern(facility) + $"\n잠근 영웅은 리롤해도 남습니다 (최대 {state.TavernMaxLocked}명).",
            FacilityType.HeroHall => DescribeHeroHall(state),
            FacilityType.Defense => DescribeDefense(facility),
            _ => Purpose(type) + "\n시설 기능과 업그레이드 효과는 나중에 추가됩니다.",
        };

        void ShopEntries(RunState state)
        {
            IReadOnlyList<ItemDefinition> stock = state.Shop.Stock;
            for (int i = 0; i < stock.Count; i++)
            {
                int index = i;
                ItemDefinition item = stock[i];
                bool locked = state.Shop.IsLocked(index);
                Cost price = state.Shop.GetBuyPrice(item);
                offerEntries.Add(new Entry
                {
                    Name = item.DisplayName, Sub = Describe(price), Tag = locked ? "잠금" : null, Icon = item.Icon,
                    Price = Describe(price), Detail = DescribeItem(item),
                    Dim = state.Wallet.Gold < price.gold,
                    Action = "구매", OnAction = () => TryBuy(index),
                    Second = locked ? "잠금 해제" : "잠금", OnSecond = () => TryToggleLock(index),
                });
            }
            IReadOnlyList<ItemInstance> items = state.Inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                ItemInstance item = items[i];
                int sell = state.Shop.GetInstanceSellPrice(item);
                storageEntries.Add(new Entry
                {
                    Name = item.DisplayName, Sub = $"판매 {sell}골드", Icon = item.Definition.Icon, Price = $"{sell}골드",
                    Detail = DescribeItem(item.Definition), Action = "판매", OnAction = () => TrySell(index),
                });
            }
        }

        void TavernEntries(RunState state)
        {
            IReadOnlyList<HeroDefinition> offers = state.TavernOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                HeroDefinition hero = offers[i];
                bool locked = state.IsTavernLocked(index);
                offerEntries.Add(new Entry
                {
                    Name = hero.DisplayName, Sub = $"{hero.Tier}티어 · {Gold(hero.HireCost)}", Price = Gold(hero.HireCost),
                    Tag = locked ? "잠금" : null,
                    Stats = hero.BaseStats, Detail = DescribeSkills(hero),
                    Dim = state.Wallet.Gold < hero.HireCost.gold,
                    Action = "고용", OnAction = () => TryHire(index),
                    Second = locked ? "잠금 해제" : "잠금", OnSecond = () => TryToggleLock(index),
                });
            }
        }

        void BarracksEntries(RunState state)
        {
            IReadOnlyList<SoldierDefinition> soldiers = state.RecruitableSoldiers;
            for (int i = 0; i < soldiers.Count; i++)
            {
                int index = i;
                SoldierDefinition soldier = soldiers[i];
                if (soldier == null) continue;
                offerEntries.Add(new Entry
                {
                    Name = soldier.DisplayName, Sub = Gold(soldier.RecruitCost), Price = Gold(soldier.RecruitCost),
                    Stats = soldier.BaseStats,
                    Dim = state.Wallet.Gold < soldier.RecruitCost.gold,
                    Action = "모집", OnAction = () => TryRecruit(index),
                });
            }
        }

        void SmithyEntries(RunState state)
        {
            IReadOnlyList<CraftingRecipe> recipes = state.CraftableRecipes(IsRecipeAvailable);
            float craftHours = state.PrepTime?.CraftHours ?? 0f;
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                CraftingRecipe recipe = recipes[i];
                offerEntries.Add(new Entry
                {
                    Name = recipe.item.DisplayName, Sub = Gold(recipe.cost), Icon = recipe.item.Icon,
                    Price = Gold(recipe.cost), Detail = DescribeItem(recipe.item) + "\n\n" + CostLines(recipe.cost, state, craftHours, RunState.DirectActionArtisans),
                    Dim = !state.Wallet.CanAfford(recipe.cost),
                    Action = "제작", OnAction = () => TryCraft(index),
                });
            }
            List<ItemInstance> equipment = EquipmentIn(state.Inventory);
            float upgradeHours = state.PrepTime?.UpgradeHours ?? 0f;
            for (int i = 0; i < equipment.Count; i++)
            {
                int index = i;
                ItemInstance item = equipment[i];
                EquipmentUpgradeCheck check = state.CheckUpgradeEquipment(item);
                Cost cost = default;
                bool hasCost = state.EquipmentUpgrades != null && state.EquipmentUpgrades.TryGetNextCost(item, out cost);
                string upgrade = check == EquipmentUpgradeCheck.MaximumLevel ? $"강화 최대 +{state.MaxEquipmentUpgrade}"
                    : hasCost ? $"+{item.UpgradeLevel + 1} 강화\n" + CostLines(cost, state, upgradeHours, RunState.DirectActionArtisans) : UpgradeReason(check);
                int sell = state.Shop.GetInstanceSellPrice(item);
                bool canTry = check == EquipmentUpgradeCheck.Allowed || check == EquipmentUpgradeCheck.NotEnoughFunds
                    || check == EquipmentUpgradeCheck.NotEnoughTime;
                storageEntries.Add(new Entry
                {
                    Name = item.DisplayName, Sub = $"판매 {sell}골드", Icon = item.Definition.Icon,
                    Tag = item.UpgradeLevel > 0 ? $"+{item.UpgradeLevel}" : null, Price = $"판매 {sell}골드",
                    Detail = DescribeItem(item.Definition) + "\n\n" + upgrade, Dim = !canTry,
                    Action = "강화", OnAction = () => TryUpgradeEquipment(index), CanAct = canTry,
                    Second = "판매", OnSecond = () => TrySellEquipment(index),
                });
            }
        }

        void HeroHallEntries(RunState state)
        {
            IReadOnlyList<RunHero> heroes = state.GradedHeroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                int index = i;
                RunHero hero = heroes[i];
                string role = state.LeadingSquadOf(hero) is Squad<RunUnit> squad ? $"{state.IndexOf(squad) + 1}부대 대장" : "예비";
                bool canGrow = state.TryGetNextGradeStep(hero, out HeroGradeStep step);
                string grow = canGrow
                    ? $"EXP {hero.Exp} / {step.expRequired}\n\n" + CostLines(step.cost, state, state.PromotionHours)
                    : "최고 등급입니다.";
                offerEntries.Add(new Entry
                {
                    Name = hero.Definition.DisplayName, Sub = $"{hero.Grade}등급 · {role}",
                    Price = canGrow ? Gold(step.cost) : string.Empty,
                    Stats = hero.Definition.BaseStats, Detail = $"{role}\n{grow}",
                    Dim = !canGrow || hero.Exp < step.expRequired,
                    // 최고 등급이 아니면 눌러서 모자란 이유(경험치·골드·시간)를 볼 수 있게 둔다.
                    Action = "승급", OnAction = () => TryPromote(index), CanAct = canGrow,
                });
            }
        }

        static string DescribeGradeBonus(RunState state)
        {
            HeroPromotionSettings settings = state.HeroPromotion;
            if (settings == null) return "승급 설정이 없습니다.";
            return "등급마다: " + DescribeModifiers(settings.BonusPerGrade) + $" · 스킬 칸 +1 (최대 {settings.MaxGrade}등급)";
        }

        static string DescribeItem(ItemDefinition item)
        {
            if (item == null) return string.Empty;
            if (item.Kind == ItemKind.Consumable)
                return item.UseHealRatio > 0f ? $"소모품 · 체력 {item.UseHealRatio * 100f:0}% 회복" : "소모품";
            if (item.Kind != ItemKind.Equipment) return string.Empty;
            return SlotName(item.Slot) + "\n" + DescribeModifiers(item.EquipModifiers);
        }

        static string SlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => "무기",
            EquipmentSlot.Offhand => "보조",
            EquipmentSlot.Armor => "갑옷",
            EquipmentSlot.Accessory => "장신구",
            EquipmentSlot.Helmet => "투구",
            _ => slot.ToString(),
        };

        static string DescribeSkills(HeroDefinition hero)
        {
            if (hero.Skills == null || hero.Skills.Count == 0) return string.Empty;
            var text = new StringBuilder("스킬: ");
            bool first = true;
            foreach (var skill in hero.Skills)
            {
                if (skill == null) continue;
                if (!first) text.Append(" · ");
                text.Append(skill.DisplayName);
                first = false;
            }
            return text.ToString();
        }

        static string StatText(IReadOnlyList<StatValue> stats, StatType type)
        {
            foreach (StatValue stat in stats)
                if (stat.stat == type) return $"{stat.value:0.#}";
            return "-";
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
            BattlementStyle.Parapet => "낮은 흉벽 (돌 뒤에 웅크리면 숨음, 사이는 트임)",
            BattlementStyle.Crenellated => "총안 흉벽 (돌출부가 화살과 시야를 막음)",
            _ => "none",
        };

        static string DescribeTavern(FacilityState facility)
        {
            var text = new StringBuilder("영웅 목록은 준비마다 바뀝니다.");
            if (!facility.IsMaxLevel) text.Append("\n업그레이드하면 더 높은 티어 영웅이 나옵니다.");
            return text.ToString();
        }

        static string DescribeHeroHall(RunState state) =>
            "대장으로 전투에 이기면 EXP를 얻고, EXP가 차면 승급합니다.\n" + DescribeGradeBonus(state);

        string DescribeSmithy(FacilityState facility)
        {
            var text = new StringBuilder($"레벨마다 정해진 장비를 만듭니다.\n장비 탭: 강화 (최대 +{State.MaxEquipmentUpgrade}) · 판매");
            if (!facility.IsMaxLevel)
            {
                List<CraftingRecipe> next = facility.Definition.Levels[facility.Level + 1].recipes;
                text.Append($"\n다음 레벨: 제작법 {(next != null ? next.Count : 0)}개");
            }
            return text.ToString();
        }

        static string DescribeTraining(FacilityState facility)
        {
            var text = new StringBuilder("훈련 보너스 (모든 병사): " + DescribeModifiers(SoldierTraining.ModifiersOf(facility)));
            if (!facility.IsMaxLevel)
                text.Append("\n다음 레벨: " + DescribeModifiers(facility.Definition.Levels[facility.Level + 1].soldierModifiers));
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

        static string Gold(Cost cost) => $"{Mathf.Max(0, cost.gold)}골드";

        // 상세 본문의 비용 목록(사용자 2026-11-24: 한 줄 칸에는 골드만, 재료·시간은 여기에 한 줄씩 가진 수와 함께).
        // 모자라는 줄은 붉게 칠한다.
        /// <summary>
        /// 시간을 쓰는 행동의 짧은 꼬리(PREP-11): " · 1시간 · 장인 1명". 시간 설정이 없거나 0시간이면 비어 있다.
        /// 장인을 쓰지 않는 행동(승급)은 시간만 붙는다.
        /// </summary>
        public static string WorkTime(RunState state, float hours, int artisans)
        {
            if (state == null || state.PrepTime == null || hours <= 0f) return string.Empty;
            return artisans > 0 ? $" · {hours:0.#}시간 · 장인 {artisans}명" : $" · {hours:0.#}시간";
        }

        static string CostLines(Cost cost, RunState state, float hours, int artisans = 0)
        {
            const string Short = "<color=#D98A7A>", End = "</color>";
            Wallet wallet = state.Wallet;
            int gold = Mathf.Max(0, cost.gold);
            var text = new StringBuilder("필요");
            string line = $"\n· {gold}골드 (보유 {wallet.Gold})";
            text.Append(wallet.Gold < gold ? Short + line + End : line);
            if (cost.materials != null)
                foreach (MaterialAmount entry in cost.materials)
                {
                    if (entry.material == null || entry.amount <= 0) continue;
                    int owned = wallet.GetMaterial(entry.material);
                    line = $"\n· {entry.material.DisplayName} {entry.amount} (보유 {owned})";
                    text.Append(owned < entry.amount ? Short + line + End : line);
                }
            if (state.PrepTime != null && hours > 0f)
                text.Append(artisans > 0 ? $"\n· 준비 시간 {hours:0.#}시간 · 장인 {artisans}명" : $"\n· 준비 시간 {hours:0.#}시간");
            return text.ToString();
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
