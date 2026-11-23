using ProjectNeo.Run;
using ProjectNeo.UI;
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
    /// 모양(UI-07 B안): 왼쪽에 위아래로 긴 금테 판. 판·테두리·글꼴은 <see cref="HudStyle"/>의 창 모양 값에서 가져온다.
    /// </summary>
    // PreparationInteraction(100)이 Esc로 다른 창을 닫은 뒤에 돈다. 그 Esc로 메뉴가 다시 열리지 않게
    // 지난 프레임에도 1인칭 탐색이었을 때만 연다.
    [DefaultExecutionOrder(110)]
    public sealed class GamePauseMenu : MonoBehaviour
    {
        public const string SaveFailedText = "저장하지 못했습니다. 타이틀로 돌아가지 않습니다.";
        public const string NoSlotText = "저장 슬롯 없이 시작해 저장할 수 없습니다. 타이틀에서 시작해 주세요.";

        static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color TextColor = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color MutedColor = new Color32(0xA9, 0xB8, 0xC2, 0xFF);
        static readonly Color ErrorColor = new Color(1f, 0.55f, 0.5f);
        static readonly Color FallbackPlate = new Color32(0x10, 0x16, 0x1A, 0xF2);
        static readonly Color FallbackAccent = new Color32(0xE3, 0xBB, 0x71, 0xFF);

        // B안(UI-07, 사용자 2026-11-23): 왼쪽 세로 메뉴. 사이드바 자리(체력 막대 끝까지)를 덮는 금테 판,
        // 화면 가장자리와 사이드바와 같은 거리.
        const float Edge = 9.6f, Width = 260f, Inner = 22f, RowHeight = 40f;

        GameBattleController battle;
        GamePhaseView phaseView;
        GameFlow flow;
        InputAction toggle;
        GameObject panel;
        TMP_Text statusLabel;
        TMP_Text stageLabel;
        HudStyle style;
        bool wasExploration;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        public string KeyText => toggle != null ? toggle.GetBindingDisplayString() : "Esc";

        /// <summary>제목 아래 줄(예: "스테이지 3 · 승천 1"). 테스트가 읽는다.</summary>
        public string StageText => stageLabel != null ? stageLabel.text : string.Empty;

        /// <summary>화면 전환기와 모양 값(금테 판·글꼴, 없으면 단색 판)을 받고 화면을 만든다.</summary>
        public void Configure(GamePhaseView view, HudStyle hudStyle = null)
        {
            phaseView = view;
            style = hudStyle;
            Build();
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            toggle = new InputAction("TogglePauseMenu", InputActionType.Button, "<Keyboard>/escape");
        }

        void OnEnable() => toggle?.Enable();
        void OnDisable() => toggle?.Disable();
        void OnDestroy() => toggle?.Dispose();

        /// <summary>메뉴를 연다. 준비 단계 1인칭 탐색이고 다른 창이 열려 있지 않을 때만 열린다. 열었으면 true.</summary>
        public bool Open()
        {
            if (panel == null) Build();
            if (IsOpen || flow.Phase != GamePhase.Preparation) return false;
            if (phaseView == null || !phaseView.IsExplorationView || !phaseView.EnterPanel()) return false;
            statusLabel.text = string.Empty;
            stageLabel.text = StageLine(flow.Run.StageNumber, flow.Run.AscensionLevel);
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

        /// <summary>제목 아래 줄. 승천은 0이어도 보인다(사용자 2026-11-23). 예: "스테이지 3 · 승천 0".</summary>
        public static string StageLine(int stage, int ascension) =>
            PrepHudText.StageTitle(stage) + " · 승천 " + Mathf.Max(0, ascension);

        Color Accent => style != null ? style.WindowAccent : FallbackAccent;
        TMP_FontAsset Font => style != null ? style.WindowFont : null;

        // 화면을 코드로 만든다: 화면 전체를 어둡게 덮고, 왼쪽에 위아래로 긴 금테 판(제목, 스테이지 줄, 금색 선, 버튼 둘,
        // 안내 줄). 'Esc 닫기' 줄은 뺐다(사용자 2026-11-23). 다시 부르면 새로 만든다.
        void Build()
        {
            bool wasOpen = IsOpen;
            if (panel != null) Destroy(panel.transform.parent.gameObject);
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
            Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.zero);
            panel.GetComponent<Image>().color = ShadeColor;

            var column = Node(panel.transform, "Column");
            column.anchorMin = new Vector2(0f, 0f); column.anchorMax = new Vector2(0f, 1f); column.pivot = new Vector2(0f, 0.5f);
            column.offsetMin = new Vector2(Edge, Edge); column.offsetMax = new Vector2(Edge + Width, -Edge);
            GoldPlate(column);

            var content = Node(column, "Content");
            Stretch(content, Vector2.zero, Vector2.zero);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Inner, (int)Inner, 14, 20); // top was 24: the title glyph sat ~10 lower than the side margins (user 2026-11-23) layout.spacing = 4f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            TMP_Text title = Label(content, "Title", "일시정지", 26f, 34f, FontStyles.Bold);
            title.color = Accent;
            stageLabel = Label(content, "Stage", string.Empty, 15f, 22f, FontStyles.Normal);
            stageLabel.color = MutedColor;
            Divider(content);
            MakeButton(content, "Button_Resume", "계속하기", Close);
            MakeButton(content, "Button_SaveAndReturn", "저장 후 타이틀로", () => SaveAndReturnToTitle());
            statusLabel = Label(content, "Status", string.Empty, 14f, 60f, FontStyles.Normal);
            statusLabel.color = ErrorColor;
            statusLabel.alignment = TextAlignmentOptions.TopLeft;
            statusLabel.textWrappingMode = TextWrappingModes.Normal;

            panel.SetActive(wasOpen);
        }

        // 사이드바와 같은 어두운 판에 굵은 금테. 모양 값이 없으면 단색 판. 판 그림이 반투명이라 뒤의 사이드바가
        // 비치지 않게 테두리 안쪽에 불투명한 바닥을 먼저 깐다.
        void GoldPlate(RectTransform parent)
        {
            Image floor = Node(parent, "Floor").gameObject.AddComponent<Image>();
            Stretch(floor.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Color baseColor = style != null ? style.WindowPlate : FallbackPlate;
            floor.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            floor.raycastTarget = false;
            Image plate = Node(parent, "SPR_Background").gameObject.AddComponent<Image>();
            Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            plate.color = style != null ? style.WindowPlate : FallbackPlate;
            Skin(plate, style != null ? style.WindowPlateSprite : null, style != null ? style.WindowPlatePixelsPerUnit : 1f);
            if (style == null || style.WindowFrameSprite == null) return;
            Image frame = Node(parent, "SPR_Frame").gameObject.AddComponent<Image>();
            Stretch(frame.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            frame.color = style.WindowFrame;
            Skin(frame, style.WindowFrameSprite, style.WindowFramePixelsPerUnit);
        }

        static void Skin(Image image, Sprite sprite, float pixelsPerUnit)
        {
            image.raycastTarget = false;
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
        }

        // 제목 아래 금색 선(사이드바 머리 패널의 선과 같은 색).
        void Divider(Transform parent)
        {
            var holder = Node(parent, "Divider");
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
            Image line = Node(holder, "Line").gameObject.AddComponent<Image>();
            line.raycastTarget = false;
            line.color = new Color(Accent.r, Accent.g, Accent.b, 0.75f);
            var rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 1.5f);
        }

        TMP_Text Label(Transform parent, string name, string text, float size, float height, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text; label.fontSize = size; label.fontStyle = fontStyle;
            label.color = TextColor; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        // 글자 줄 버튼. 마우스를 올리면 글자색과 마름모만 바뀌고 바탕은 그대로다.
        void MakeButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var image = go.GetComponent<Image>(); image.color = Color.white;
            var button = go.GetComponent<Button>(); button.targetGraphic = image;
            Color gold = Accent;
            var colors = button.colors;
            colors.normalColor = new Color(gold.r, gold.g, gold.b, 0f);
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = colors.normalColor; // no hover background; MenuRowHighlight recolours the text and shows the marker (user 2026-11-23)
            colors.pressedColor = colors.normalColor;
            colors.colorMultiplier = 1f; colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            TMP_Text label = Label(go.transform, "Label", text, 19f, RowHeight, FontStyles.Normal);
            Stretch(label.rectTransform, new Vector2(22f, 0f), Vector2.zero);
            // 선택 표시: 사이드바 머리 패널 선의 마름모와 같은 금색 마름모(사용자 2026-11-23).
            Image marker = Node(go.transform, "Marker").gameObject.AddComponent<Image>();
            marker.raycastTarget = false;
            marker.color = gold;
            var markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0f, 0.5f);
            markerRect.anchoredPosition = new Vector2(9f, 0f);
            markerRect.sizeDelta = new Vector2(7f, 7f);
            markerRect.localEulerAngles = new Vector3(0f, 0f, 45f);
            go.AddComponent<MenuRowHighlight>().Configure(label, marker.gameObject, TextColor, gold);
        }

        static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}

