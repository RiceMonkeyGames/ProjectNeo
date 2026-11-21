using System;
using ProjectNeo.Flow;
using ProjectNeo.Morale;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-06 준비 HUD 구조를 Game 씬에 만든다: 위 자원 바(단계·준비 시간 게이지·골드와 재료), 왼쪽 분대 카드 열,
    /// 오른쪽 적 정보 창, 조준점 아래 상호작용 안내, H 전체 키 목록과 오른쪽 아래 "H 키 안내" 줄.
    /// 색과 글자 크기는 HudStyle 에셋에서 읽는다(모양은 UI-10이 정해지면 에셋만 바꾸고 다시 실행). 다시 실행해도 같은 결과다.
    /// 기준 해상도는 Game 씬 캔버스 그대로(1280×720)다.
    /// </summary>
    public static class PrepHudSetup
    {
        public const string StylePath = "Assets/_Project/Data/UI/HudStyle.asset";
        public const string BarName = "PrepTopBar";
        public const string CardsName = "PrepSquadCards";
        public const string KeyListName = "KeyList";
        public const string KeyHintName = "KeyListHint";
        public const string ContextName = "ContextPrompt";
        const string ClockName = "PrepClock";
        internal const float BarHeight = 34f;
        // 자원 바 아래 여백. 분대 카드와 적 정보 창이 이 아래에서 시작한다.
        internal const float BelowBar = BarHeight + 12f;
        // 카드 높이: 분대 최대 10개(SquadConfig)가 자원 바 아래에서 아래 안내 줄 위까지 들어가게 한다(10 × (52 + 4) = 560).
        const float Margin = 16f, CardWidth = 240f, CardHeight = 52f, CardSpacing = 4f, KeyListWidth = 340f;

        [MenuItem("ProjectNeo/Setup/Preparation HUD (UI-06)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");
            HudStyle style = LoadOrCreateStyle();

            var scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            var battle = UnityEngine.Object.FindAnyObjectByType<GameBattleController>();
            var view = UnityEngine.Object.FindAnyObjectByType<GamePhaseView>();
            var interaction = UnityEngine.Object.FindAnyObjectByType<PreparationInteraction>(FindObjectsInactive.Include);
            var clock = hud != null ? hud.GetComponent<PrepClockHud>() : null;
            var center = UnityEngine.Object.FindAnyObjectByType<SquadCommandCenter>(FindObjectsInactive.Include);
            var morale = UnityEngine.Object.FindAnyObjectByType<GameSquadMorale>(FindObjectsInactive.Include);
            var commandHud = UnityEngine.Object.FindAnyObjectByType<SquadCommandHud>(FindObjectsInactive.Include);
            var commandInput = UnityEngine.Object.FindAnyObjectByType<SquadCommandInput>(FindObjectsInactive.Include);
            if (hud == null || battle == null || view == null || interaction == null || clock == null || center == null)
                throw new InvalidOperationException("Game HUD, battle, phase view, interaction, prep clock (PREP-06) and squad center are required.");
            TMP_FontAsset font = hud.GetComponentInChildren<TMP_Text>(true).font;
            Transform organization = hud.transform.Find("OrganizationPanel");
            GameObject covered = organization != null ? organization.gameObject : null;

            BuildTopBar(hud, battle, clock, style, font, covered);
            BuildSquadCards(hud, style, font, center, morale, view, commandHud, commandInput);
            PlaceEnemyIntel(hud);
            BuildKeyList(hud, style, font, interaction, view, battle, center);
            BuildContextPrompt(hud, style, font);

            // 자원 바는 시설 창보다 위에 그린다(PREP-06 시계 줄과 같은 규칙).
            hud.transform.Find(BarName).SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static HudStyle LoadOrCreateStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(StylePath);
            if (style != null) return style;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/UI")) AssetDatabase.CreateFolder("Assets/_Project/Data", "UI");
            style = HudStyle.Create();
            AssetDatabase.CreateAsset(style, StylePath);
            AssetDatabase.SaveAssets();
            return style;
        }

        static void BuildTopBar(GameFlowHud hud, GameBattleController battle, PrepClockHud clock, HudStyle style,
            TMP_FontAsset font, GameObject covered)
        {
            RectTransform bar = Rect(hud.transform, BarName);
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0f, BarHeight);
            Panel(bar, style, style.Panel);

            TextMeshProUGUI stage = Text(bar, "Stage", font, style.TitleSize, style.Heading, TextAlignmentOptions.MidlineLeft, "준비 · 스테이지 -");
            Stretch(stage.rectTransform, new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(Margin, 0f), Vector2.zero);
            TextMeshProUGUI resources = Text(bar, "Resources", font, style.BodySize, style.Text, TextAlignmentOptions.MidlineRight, "골드 -");
            Stretch(resources.rectTransform, new Vector2(0.62f, 0f), Vector2.one, Vector2.zero, new Vector2(-Margin, 0f));

            // PREP-06 시계 줄을 자원 바 가운데로 옮기고 아래에 얇은 남은 시간 게이지를 붙인다.
            Transform existing = hud.transform.Find(ClockName) ?? bar.Find(ClockName);
            RectTransform line = existing != null ? (RectTransform)existing : Rect(bar, ClockName);
            line.SetParent(bar, false);
            line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(360f, BarHeight - 6f);
            var lineImage = line.GetComponent<Image>();
            if (lineImage != null) lineImage.color = new Color(0f, 0f, 0f, 0f);
            RectTransform track = Rect(line, "Track");
            track.anchorMin = Vector2.zero; track.anchorMax = new Vector2(1f, 0f); track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = Vector2.zero; track.sizeDelta = new Vector2(0f, 6f);
            Gauge(track, style, style.GaugeTrack);
            RectTransform fill = Rect(track, "Fill");
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            Gauge(fill, style, style.TimeFill);
            track.SetAsFirstSibling();
            TextMeshProUGUI label = Text(line, "Text", font, style.BodySize, style.Heading, TextAlignmentOptions.Center, "준비 시간 -- / --");
            Stretch(label.rectTransform, new Vector2(0f, 0f), Vector2.one, new Vector2(0f, 8f), Vector2.zero);
            line.gameObject.SetActive(false);
            clock.Configure(battle, line.gameObject, label, covered);
            clock.ConfigureGauge(fill);
            EditorUtility.SetDirty(clock);

            bar.gameObject.SetActive(false);
            var controller = Get<PrepTopBarHud>(hud.gameObject);
            Transform phase = hud.transform.Find("Panel");
            controller.Configure(battle, bar.gameObject, stage, resources, phase != null ? phase.gameObject : null, covered);
            EditorUtility.SetDirty(controller);
        }

        static void BuildSquadCards(GameFlowHud hud, HudStyle style, TMP_FontAsset font, SquadCommandCenter center,
            GameSquadMorale morale, GamePhaseView view, SquadCommandHud commandHud, SquadCommandInput commandInput)
        {
            RectTransform column = Rect(hud.transform, CardsName);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(Margin, -BelowBar); column.sizeDelta = new Vector2(CardWidth, 0f);
            var layout = Get<VerticalLayoutGroup>(column.gameObject);
            layout.spacing = CardSpacing; layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Get<ContentSizeFitter>(column.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // 예전 실행이 남긴 복제 카드는 지운다(실행 중에만 생긴다).
            for (int i = column.childCount - 1; i >= 0; i--)
                if (column.GetChild(i).name.StartsWith("SquadCard", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(column.GetChild(i).gameObject);

            RectTransform card = Rect(column, "CardTemplate");
            Get<LayoutElement>(card.gameObject).preferredHeight = CardHeight;
            Image background = Panel(card, style, style.Panel);
            // 1줄: 이름(왼쪽)과 인원(오른쪽). 그 아래 얇은 인원 게이지. 2줄: 사기 글과 사기 게이지.
            TextMeshProUGUI title = Text(card, "Title", font, style.BodySize, style.Heading, TextAlignmentOptions.MidlineLeft, "부대 1 - 근접");
            Row(title.rectTransform, 3f, 18f, 10f, -80f);
            TextMeshProUGUI people = Text(card, "Headcount", font, style.SmallSize, style.Text, TextAlignmentOptions.MidlineRight, "0/0명");
            Row(people.rectTransform, 3f, 18f, CardWidth - 80f, -10f);
            RectTransform peopleFill = Bar(card, "HeadcountTrack", 23f, 5f, 10f, style, style.HeadcountFill, out _);
            TextMeshProUGUI moraleLabel = Text(card, "Morale", font, style.SmallSize, style.Text, TextAlignmentOptions.MidlineLeft, "사기 -");
            Row(moraleLabel.rectTransform, 31f, 18f, 10f, -(CardWidth - 84f));
            RectTransform moraleFill = Bar(card, "MoraleTrack", 36f, 8f, 84f, style, style.MoraleFill(MoraleBand.Normal), out Image moraleImage);
            var template = Get<PrepSquadCard>(card.gameObject);
            template.Configure(background, title, people, peopleFill, moraleLabel, moraleFill, moraleImage);
            EditorUtility.SetDirty(template);
            card.SetAsFirstSibling();
            card.gameObject.SetActive(false);

            TextMeshProUGUI message = Text(column, "Message", font, style.SmallSize, style.Warning, TextAlignmentOptions.TopLeft, string.Empty);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.transform.SetAsLastSibling();
            column.gameObject.SetActive(false);

            var controller = Get<PrepSquadCardsHud>(hud.gameObject);
            controller.Configure(style, center, morale, view, commandHud, commandInput, column.gameObject, template, message);
            EditorUtility.SetDirty(controller);
        }

        static void PlaceEnemyIntel(GameFlowHud hud)
        {
            // C-5 적 정보 창: 준비 미리보기는 오른쪽 위(자원 바 아래). 왼쪽은 분대 카드가 쓴다.
            // 전투 중에는 오른쪽 위 전투 상태 상자를 피해 예전 자리(왼쪽 분대 목록 아래)를 그대로 쓴다.
            var intel = hud.GetComponent<EnemyIntelHud>();
            if (intel == null) return;
            intel.ConfigurePlacement(new Vector2(1f, 1f), new Vector2(-Margin, -BelowBar), new Vector2(0f, 1f), EnemyIntelHudSetup.Offset);
            EditorUtility.SetDirty(intel);
        }

        static void BuildKeyList(GameFlowHud hud, HudStyle style, TMP_FontAsset font, PreparationInteraction interaction,
            GamePhaseView view, GameBattleController battle, SquadCommandCenter center)
        {
            // 직접 여는 도움말이라 화면 가운데에 띄운다. 양옆의 분대 카드·적 정보 창과 겹치지 않는다.
            RectTransform panel = Rect(hud.transform, KeyListName);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, 20f); panel.sizeDelta = new Vector2(KeyListWidth, 0f);
            Panel(panel, style, style.Panel);
            var layout = Get<VerticalLayoutGroup>(panel.gameObject);
            layout.padding = new RectOffset(12, 12, 8, 10); layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Get<ContentSizeFitter>(panel.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text(panel, "Title", font, style.TitleSize, style.Heading, TextAlignmentOptions.TopLeft, "키 안내");
            TextMeshProUGUI body = Text(panel, "Rows", font, style.SmallSize, style.Text, TextAlignmentOptions.TopLeft, "-");
            panel.gameObject.SetActive(false);

            // 목록이 닫혀 있을 때의 짧은 안내. 아래 안내 줄(가로 0.15~0.85)의 오른쪽 빈자리에 둔다.
            TextMeshProUGUI hint = Text(hud.transform, KeyHintName, font, style.SmallSize, style.Muted, TextAlignmentOptions.BottomRight, "H 키 안내");
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-24f, 16f); hintRect.sizeDelta = new Vector2(160f, 24f);
            hint.gameObject.SetActive(false);

            var controller = Get<KeyListHud>(hud.gameObject);
            controller.Configure(interaction, view, battle, center, panel.gameObject, body, hint);
            EditorUtility.SetDirty(controller);
        }

        static void BuildContextPrompt(GameFlowHud hud, HudStyle style, TMP_FontAsset font)
        {
            // 조준점(가운데) 바로 아래. 글 길이에 맞춰 배경 폭이 늘어난다.
            RectTransform box = Rect(hud.transform, ContextName);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(0f, -44f); box.sizeDelta = new Vector2(0f, 30f);
            Panel(box, style, style.Panel);
            var layout = Get<HorizontalLayoutGroup>(box.gameObject);
            layout.padding = new RectOffset(14, 14, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Get<ContentSizeFitter>(box.gameObject).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI label = Text(box, "Text", font, style.BodySize, style.Text, TextAlignmentOptions.Center, "F · -");
            box.gameObject.SetActive(false);
            hud.ConfigureContextPrompt(label);
            EditorUtility.SetDirty(hud);
        }

        static RectTransform Bar(RectTransform card, string name, float top, float height, float left, HudStyle style,
            Color color, out Image fillImage)
        {
            RectTransform track = Rect(card, name);
            Row(track, top, height, left, -10f);
            Gauge(track, style, style.GaugeTrack);
            RectTransform fill = Rect(track, "Fill");
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage = Gauge(fill, style, color);
            return fill;
        }

        // 카드 위에서 top만큼 내려온 높이 height의 줄. left/right는 카드 왼쪽·오른쪽 가장자리에서의 거리(오른쪽은 음수).
        static void Row(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(right, -top);
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        static Image Panel(RectTransform rect, HudStyle style, Color color)
        {
            Image image = Get<Image>(rect.gameObject);
            image.sprite = style.PanelSprite;
            image.type = style.PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image Gauge(RectTransform rect, HudStyle style, Color color)
        {
            Image image = Get<Image>(rect.gameObject);
            image.sprite = style.GaugeSprite;
            image.type = style.GaugeSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            var rect = existing != null ? (RectTransform)existing
                : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static T Get<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment, string text)
        {
            RectTransform rect = Rect(parent, name);
            var label = Get<TextMeshProUGUI>(rect.gameObject);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
    }
}
