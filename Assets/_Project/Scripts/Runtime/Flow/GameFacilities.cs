using System.Collections.Generic;
using System.Text;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 시설 팝업(D-0·D-1·D-5). 런 데이터에 시설 상태·상점 창구를 붙이고, 건물에서 F로 시설 팝업을 열며
    /// Esc나 닫기 버튼으로 닫는다. 팝업이 열린 동안은 <see cref="GamePhaseView.IsPanelView"/>로 이동·시점을 멈춘다.
    /// 모든 시설은 팝업에서 업그레이드하고, 상점은 매물 구매와 보관함 판매를 한다. 다른 시설의 기능은 후속 작업이다.
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
        /// <summary>지금 보이는 보관함 줄 수(상점).</summary>
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
            if (body != null)
            {
                string text = shop ? "매물은 준비 단계마다 새로 바뀝니다. 업그레이드는 다음 준비의 매물부터 반영됩니다."
                    : Purpose(type) + "\n이 시설의 기능과 업그레이드 효과는 다음 작업에서 붙습니다.";
                body.text = string.IsNullOrEmpty(Message) ? text : Message + "\n" + text;
            }
            if (tradeArea != null) tradeArea.SetActive(shop);
            ClearRows();
            if (!shop || rowTemplate == null) return;
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
