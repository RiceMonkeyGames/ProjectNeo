using System.Collections.Generic;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 퍽 창(META-09). 퍽 트리 3개를 나란히 보여 주고, 퍽을 두 번 누르면 이번 런에 찍는다. 규칙과 글자는 <see cref="PerkWindowModel"/>.
    /// 준비 단계 1인칭 탐색에서 K로 열고, 보상 화면에서는 'Perks' 버튼(또는 K)으로 연다. 전투 중에는 열리지 않는다(Q-06).
    /// Game.unity를 고치지 않도록 화면을 실행 중에 코드로 만든다(사용자 결정 2026-11-20).
    /// <see cref="GameBattleController"/>가 실행 중에 붙인다.
    /// </summary>
    public sealed class GamePerkWindow : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.09f, 0.96f);
        static readonly Color TakenColor = new Color(0.16f, 0.3f, 0.2f);
        static readonly Color AvailableColor = new Color(0.15f, 0.24f, 0.29f);
        static readonly Color ArmedColor = new Color(0.45f, 0.36f, 0.12f);
        static readonly Color LockedColor = new Color(0.12f, 0.13f, 0.15f);
        static readonly Color TextColor = new Color(0.92f, 0.96f, 1f);

        readonly List<KeyValuePair<PerkDefinition, Button>> rows = new List<KeyValuePair<PerkDefinition, Button>>();
        readonly List<RectTransform> columns = new List<RectTransform>();
        GameBattleController battle;
        GamePhaseView phaseView;
        PerkTreeCatalog trees;
        GameFlow flow;
        InputAction toggle;
        GameObject panel;
        GameObject rewardButton;
        TMP_Text pointsLabel;
        TMP_Text statusLabel;
        PerkWindowModel model;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsRewardButtonVisible => rewardButton != null && rewardButton.activeSelf;
        public string PointsText => pointsLabel != null ? pointsLabel.text : string.Empty;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        public int RowCount => rows.Count;
        public string KeyText => toggle != null ? toggle.GetBindingDisplayString() : "K";
        public PerkTreeCatalog Trees => trees;

        public void Configure(GamePhaseView view, PerkTreeCatalog perkTrees)
        {
            phaseView = view;
            trees = perkTrees;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            toggle = new InputAction("TogglePerks", InputActionType.Button, "<Keyboard>/k");
            Build();
        }

        void OnEnable() => toggle?.Enable();
        void OnDisable() => toggle?.Disable();
        void OnDestroy() => toggle?.Dispose();

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>퍽 창을 연다. 준비 단계(1인칭 탐색)나 보상 화면에서만 열린다. 열었으면 true.</summary>
        public bool Open()
        {
            if (IsOpen || trees == null || State?.Perks == null) return false;
            if (flow.Phase == GamePhase.Preparation)
            {
                if (phaseView == null || !phaseView.IsExplorationView || !phaseView.EnterPanel()) return false;
            }
            else if (flow.Phase != GamePhase.Reward) return false;
            model = new PerkWindowModel(State, trees);
            BuildRows();
            panel.SetActive(true);
            Refresh();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            model?.Disarm();
            panel.SetActive(false);
            if (flow.Phase == GamePhase.Preparation && phaseView != null) phaseView.ExitPanel();
        }

        /// <summary>퍽을 누른다. 퍽 버튼이 부르고 테스트도 쓴다.</summary>
        public UnlockClickResult Click(PerkDefinition perk)
        {
            if (model == null) return UnlockClickResult.Refused;
            UnlockClickResult result = model.Click(perk);
            // 보상 화면의 저장은 이미 끝났으므로, 여기서 찍은 퍽도 이어 하기에 남도록 다시 저장한다.
            if (result == UnlockClickResult.Opened && flow.Phase == GamePhase.Reward) battle.SaveRun(RunSaveTrigger.RewardGranted);
            Refresh();
            return result;
        }

        public string RowText(PerkDefinition perk)
        {
            foreach (KeyValuePair<PerkDefinition, Button> row in rows)
                if (row.Key == perk) return row.Value.GetComponentInChildren<TMP_Text>(true).text;
            return string.Empty;
        }

        void Update()
        {
            if (panel == null) return;
            if (toggle != null && toggle.WasPressedThisFrame())
            {
                if (IsOpen) Close();
                else Open();
            }
            // Esc(준비 단계는 PreparationInteraction이 팝업 화면을 끝낸다)나 단계 전환으로 닫는다.
            if (IsOpen && flow.Phase == GamePhase.Preparation && (phaseView == null || !phaseView.IsPanelView)) panel.SetActive(false);
            if (IsOpen && flow.Phase == GamePhase.Reward && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (IsOpen && flow.Phase != GamePhase.Preparation && flow.Phase != GamePhase.Reward) panel.SetActive(false);
            if (rewardButton != null)
            {
                bool show = flow.Phase == GamePhase.Reward && !IsOpen && trees != null && State?.Perks != null;
                if (rewardButton.activeSelf != show) rewardButton.SetActive(show);
            }
        }

        void Refresh()
        {
            if (model == null) return;
            pointsLabel.text = model.PointsText;
            statusLabel.text = model.Notice;
            foreach (KeyValuePair<PerkDefinition, Button> row in rows)
            {
                row.Value.GetComponentInChildren<TMP_Text>(true).text = model.EntryText(row.Key);
                ((Image)row.Value.targetGraphic).color = model.IsAllocated(row.Key) ? TakenColor
                    : row.Key == model.ArmedPerk ? ArmedColor
                    : model.CanAllocate(row.Key) ? AvailableColor : LockedColor;
            }
        }

        void BuildRows()
        {
            foreach (KeyValuePair<PerkDefinition, Button> row in rows) if (row.Value != null) Destroy(row.Value.gameObject);
            rows.Clear();
            for (int i = 0; i < columns.Count; i++)
            {
                PerkTreeDefinition tree = i < trees.Trees.Count ? trees.Trees[i] : null;
                columns[i].gameObject.SetActive(tree != null);
                if (tree == null) continue;
                columns[i].GetComponentInChildren<TMP_Text>(true).text = tree.DisplayName;
                foreach (PerkDefinition perk in tree.Perks)
                {
                    if (perk == null) continue;
                    PerkDefinition target = perk;
                    Button button = MakeButton(columns[i], "Row_" + perk.Id, string.Empty, 15f, 74f, () => Click(target));
                    rows.Add(new KeyValuePair<PerkDefinition, Button>(perk, button));
                }
            }
        }

        // 화면을 코드로 만든다: 위에 포인트·닫기, 안내 줄, 아래에 트리 3열. 보상 화면용 여는 버튼은 오른쪽 아래.
        void Build()
        {
            var root = new GameObject("PerkWindowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("PerkWindow", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            Stretch((RectTransform)panel.transform, 60f, 40f);
            panel.GetComponent<Image>().color = PanelColor;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18); layout.spacing = 10f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            var header = Group("Header", panel.transform, true, 12f);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            Label(header, "Perks", 26f, 40f, FontStyles.Bold).GetComponent<LayoutElement>().preferredWidth = 120f;
            pointsLabel = Label(header, string.Empty, 20f, 40f, FontStyles.Normal);
            pointsLabel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(header, "Button_Close", "Close", 17f, 40f, Close).GetComponent<LayoutElement>().preferredWidth = 140f;
            statusLabel = Label(panel.transform, string.Empty, 16f, 24f, FontStyles.Normal);

            var body = Group("Trees", panel.transform, true, 14f);
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var bodyLayout = body.GetComponent<HorizontalLayoutGroup>();
            bodyLayout.childForceExpandWidth = true; bodyLayout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                RectTransform column = Group("Column_" + (i + 1), body, false, 8f);
                Label(column, "Tree", 20f, 30f, FontStyles.Bold);
                columns.Add(column);
            }
            panel.SetActive(false);

            rewardButton = MakeButton(root.transform, "Button_OpenPerks", "Perks (K)", 18f, 44f, () => Open()).gameObject;
            var rect = (RectTransform)rewardButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(180f, 44f);
            rewardButton.SetActive(false);
        }

        static void Stretch(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y);
        }

        static RectTransform Group(string name, Transform parent, bool horizontal, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            HorizontalOrVerticalLayoutGroup group = horizontal
                ? go.AddComponent<HorizontalLayoutGroup>() : (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = !horizontal; group.childForceExpandHeight = horizontal;
            return (RectTransform)go.transform;
        }

        static TMP_Text Label(Transform parent, string text, float size, float height, FontStyles style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.fontStyle = style;
            label.color = TextColor; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static Button MakeButton(Transform parent, string name, string text, float size, float height, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var image = go.GetComponent<Image>(); image.color = AvailableColor;
            var button = go.GetComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TMP_Text label = Label(go.transform, text, size, height, FontStyles.Normal);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 2f); rect.offsetMax = new Vector2(-10f, -2f);
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }
    }
}
