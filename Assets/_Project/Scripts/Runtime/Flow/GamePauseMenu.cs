using ProjectNeo.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 준비 단계의 Esc 일시정지 메뉴(FLOW-04). 1인칭 탐색에서 다른 창이 열려 있지 않을 때 Esc로 열고,
    /// '계속하기'나 Esc로 닫는다. '저장 후 타이틀로'는 준비 중에 끌 때와 같은 저장(<see cref="RunSaveTrigger.QuitDuringPreparation"/>)을
    /// 한 뒤 타이틀로 돌아간다. 저장이 안 되면 메뉴를 열어 둔 채 알리고 타이틀로 가지 않는다.
    /// 전투·보상 화면에서는 열리지 않는다. 설정 창(UI-11)은 나중에 이 메뉴에 붙인다.
    /// Game.unity를 고치지 않도록 화면을 실행 중에 코드로 만든다. <see cref="GameBattleController"/>가 실행 중에 붙인다.
    /// </summary>
    // PreparationInteraction(100)이 Esc로 다른 창을 닫은 뒤에 돈다. 그 Esc로 메뉴가 다시 열리지 않게
    // 지난 프레임에도 1인칭 탐색이었을 때만 연다.
    [DefaultExecutionOrder(110)]
    public sealed class GamePauseMenu : MonoBehaviour
    {
        public const string SaveFailedText = "저장하지 못했습니다. 타이틀로 돌아가지 않습니다.";
        public const string NoSlotText = "저장 슬롯 없이 시작해 저장할 수 없습니다. 타이틀에서 시작해 주세요.";

        static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.09f, 0.96f);
        static readonly Color ButtonColor = new Color(0.15f, 0.24f, 0.29f);
        static readonly Color TextColor = new Color(0.92f, 0.96f, 1f);
        static readonly Color ErrorColor = new Color(1f, 0.55f, 0.5f);

        GameBattleController battle;
        GamePhaseView phaseView;
        GameFlow flow;
        InputAction toggle;
        GameObject panel;
        TMP_Text statusLabel;
        bool wasExploration;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        public string KeyText => toggle != null ? toggle.GetBindingDisplayString() : "Esc";

        public void Configure(GamePhaseView view) => phaseView = view;

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            toggle = new InputAction("TogglePauseMenu", InputActionType.Button, "<Keyboard>/escape");
            Build();
        }

        void OnEnable() => toggle?.Enable();
        void OnDisable() => toggle?.Disable();
        void OnDestroy() => toggle?.Dispose();

        /// <summary>메뉴를 연다. 준비 단계 1인칭 탐색이고 다른 창이 열려 있지 않을 때만 열린다. 열었으면 true.</summary>
        public bool Open()
        {
            if (IsOpen || flow.Phase != GamePhase.Preparation) return false;
            if (phaseView == null || !phaseView.IsExplorationView || !phaseView.EnterPanel()) return false;
            statusLabel.text = string.Empty;
            panel.SetActive(true);
            return true;
        }

        /// <summary>메뉴를 닫고 1인칭 탐색으로 돌아간다('계속하기').</summary>
        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            if (phaseView != null) phaseView.ExitPanel();
        }

        /// <summary>
        /// 런을 저장하고 타이틀로 돌아간다. 저장하지 못하면 메뉴를 열어 둔 채 알리고 false. 버튼이 부르고 테스트도 쓴다.
        /// </summary>
        public bool SaveAndReturnToTitle()
        {
            if (!IsOpen || flow.Phase != GamePhase.Preparation || SceneSwitcher.IsLoading) return false;
            if (GameSession.ActiveSlot < 0) { ShowError(NoSlotText); return false; }
            if (battle == null || !battle.SaveRun(RunSaveTrigger.QuitDuringPreparation)) { ShowError(SaveFailedText); return false; }
            GameSession.SaveMeta();
            panel.SetActive(false);
            // 타이틀 단계로 바뀌면 GameDirector가 타이틀 씬을 부른다.
            flow.ReturnToTitle();
            return true;
        }

        void ShowError(string text)
        {
            statusLabel.text = text;
            Debug.LogWarning("Save and return to Title refused: " + text);
        }

        void Update()
        {
            if (panel == null) return;
            // Esc(PreparationInteraction이 팝업 화면을 끝낸다)나 단계 전환으로 닫혔으면 화면도 숨긴다.
            if (IsOpen && (flow.Phase != GamePhase.Preparation || phaseView == null || !phaseView.IsPanelView)) panel.SetActive(false);
            else if (!IsOpen && wasExploration && toggle != null && toggle.WasPressedThisFrame()) Open();
        }

        void LateUpdate() => wasExploration = flow.Phase == GamePhase.Preparation && phaseView != null && phaseView.IsExplorationView;

        // 화면을 코드로 만든다: 화면 전체를 어둡게 덮고 가운데에 제목·버튼 둘·안내 줄.
        void Build()
        {
            var root = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("PauseMenu", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var shade = (RectTransform)panel.transform;
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = ShadeColor;

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            box.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)box.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 300f);
            box.GetComponent<Image>().color = PanelColor;
            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 14f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            TMP_Text title = Label(box.transform, "일시정지", 28f, 44f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            MakeButton(box.transform, "Button_Resume", "계속하기", Close);
            MakeButton(box.transform, "Button_SaveAndReturn", "저장 후 타이틀로", () => SaveAndReturnToTitle());
            statusLabel = Label(box.transform, string.Empty, 15f, 48f, FontStyles.Normal);
            statusLabel.color = ErrorColor;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.textWrappingMode = TextWrappingModes.Normal;
            panel.SetActive(false);
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

        static void MakeButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 52f;
            var image = go.GetComponent<Image>(); image.color = ButtonColor;
            var button = go.GetComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TMP_Text label = Label(go.transform, text, 20f, 52f, FontStyles.Normal);
            label.alignment = TextAlignmentOptions.Center;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }
    }
}
