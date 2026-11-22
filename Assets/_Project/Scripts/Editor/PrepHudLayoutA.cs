using System;
using ProjectNeo.Morale;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FW = ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-06 preparation HUD, layout A (picked 2026-11-22 from <c>Docs/mockups/prep-hud-mockups.html</c>), after the pack sample
    /// <c>06_Demo_FantasyWarrior_HUD_Adventure01</c>: stage banner and title top-left, squad portrait cards in a left column,
    /// prep time ribbon (sample compass) top-center, resource chip (sample game time) top-right, enemy preview under it,
    /// HP bar (sample hotbar HP) bottom-left, battle-start plate (sample XP log) bottom-center with a hold gauge.
    /// Runs <see cref="PrepHudSetup.Apply"/> first for the structure and wiring, then replaces the art. Sample values are on
    /// the 3840x2160 canvas and are divided by 3 for the 1280x720 Game canvas; sliced sprites get a 3x pixels-per-unit
    /// multiplier so borders keep the sample's proportions. Running it again gives the same result.
    /// </summary>
    public static class PrepHudLayoutA
    {
        const float S = FW.Scale, P = 3f;
        internal const float TopHeight = 64f;
        const float CardWidth = 280f, CardHeight = 60f, CardSpacing = 8f;
        static readonly Vector2 CardsAt = new Vector2(20f, -150f);
        static readonly Vector2 IntelAt = new Vector2(-24f, -78f);
        const string Shadow = "SPR_FX_FantasyWarrior_Glow_Box01_Stepped";
        const string BarTrack = "SPR_HUD_FantasyWarrior_Bar_Diamond01";
        const string BarFill = "SPR_FantasyWarrior_Bar_Horizontal05";
        static readonly Color Cream = FW.Cream;
        static readonly Color HpRed = new Color32(0xBE, 0x30, 0x30, 0xFF);
        static readonly Color TrackColor = new Color32(0x1F, 0x28, 0x2E, 0xD9);

        [MenuItem("ProjectNeo/Setup/Preparation HUD - Layout A (UI-06)")]
        public static void Apply()
        {
            PrepHudSetup.Apply();
            var scene = EditorSceneManager.GetActiveScene();
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(PrepHudSetup.StylePath);
            if (hud == null || style == null) throw new InvalidOperationException("Game HUD and HUD style are required.");
            TMP_FontAsset noto = FW.Noto, grenze = FW.Grenze;
            outlined = OutlinedNoto(noto);

            TopBar(hud, style, noto);
            SquadCards(hud, style, noto);
            var intel = hud.GetComponent<EnemyIntelHud>();
            if (intel != null)
            {
                intel.ConfigurePlacement(new Vector2(1f, 1f), IntelAt, new Vector2(0f, 1f), EnemyIntelHudSetup.Offset);
                EditorUtility.SetDirty(intel);
            }
            KeyList(hud);
            Health(hud, grenze);
            StartPlate(hud, noto);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public const string OutlinedNotoPath = "Assets/_Project/Art/Fonts/NotoSansKR SDF - HUD Outline.mat";
        static Material outlined;

        // Noto material preset with a dark outline and drop shadow, for HUD text drawn straight over the 3D view
        // (stage title, squad names). The plain font material stays as it is for text on plates.
        static Material OutlinedNoto(TMP_FontAsset noto)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlinedNotoPath);
            if (material == null)
            {
                material = new Material(noto.material);
                AssetDatabase.CreateAsset(material, OutlinedNotoPath);
            }
            else material.CopyPropertiesFromMaterial(noto.material);
            // Shadow only: an SDF outline on the dynamic Noto atlas grayed the small text (first try), so the face stays
            // as it is and a dilated dark underlay gives the edge.
            material.shader = noto.material.shader;
            material.DisableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            // Crisp drop shadow down-right (a soft wide one smeared into the thin glyphs and grayed them).
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.02f, 0.03f, 0.05f, 1f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void Outline(TMP_Text label)
        {
            var serialized = new SerializedObject(label);
            serialized.FindProperty("m_sharedMaterial").objectReferenceValue = outlined;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Full key list: right side above the "H 키 안내" line (feedback 2026-11-22: not in the middle of the view).
        static void KeyList(GameFlowHud hud)
        {
            var panel = (RectTransform)hud.transform.Find(PrepHudSetup.KeyListName);
            if (panel == null) return;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-24f, 48f);
        }

        // ---------------------------------------------------------------- top bar

        static void TopBar(GameFlowHud hud, HudStyle style, TMP_FontAsset noto)
        {
            var bar = (RectTransform)hud.transform.Find(PrepHudSetup.BarName);
            bar.sizeDelta = new Vector2(0f, TopHeight);
            Bare(bar);
            RectTransform visual = FW.FreshVisual(bar);

            // Sample Objective_Header icon: Banner01 pennant (120x300, +20 wide) with wings and lion head on top.
            var icon = FW.Node(visual, "Banner");
            FW.Place(icon, new Vector2(0f, 1f), new Vector2(52f, -40f), new Vector2(120f, 300f) * S, new Vector2(0.5f, 1f));
            var banner = FW.Image(icon, "SPR_Banner", "SPR_FantasyWarrior_Banner01", Color.white, 1f * P);
            FW.Stretch(banner.rectTransform, new Vector2(-10f, 0f) * S, new Vector2(10f, 0f) * S);
            var wings = FW.Image(icon, "SPR_Greeble_Wings01", "SPR_FantasyWarrior_Greeble_Wings01_Variant01", Color.white, 1f);
            FW.Place(wings.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 15f) * S, new Vector2(320f, 160f) * S);
            var lion = FW.Image(icon, "SPR_Greeble_LionHead01", "SPR_FantasyWarrior_Greeble_LionHead01", Color.white, 1f);
            FW.Place(lion.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f) * S, new Vector2(160f, 160f) * S);

            // No plate behind the title (feedback: the soft shadow box looked off); the text carries its own outline.
            var title = Label(bar, "StageTitle", "스테이지 -", noto, 32f, FW.Gold, TextAlignmentOptions.TopLeft);
            Outline(title);
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(94f, -10f), new Vector2(360f, 40f), new Vector2(0f, 1f));
            var stage = bar.Find("Stage").GetComponent<TMP_Text>();
            FW.Font(stage, noto, 15f, Color.white, TextAlignmentOptions.TopLeft);
            Outline(stage);
            stage.raycastTarget = false;
            FW.Place(stage.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -50f), new Vector2(360f, 22f), new Vector2(0f, 1f));
            var controller = hud.GetComponent<PrepTopBarHud>();
            controller.ConfigureTitle(title);

            Ribbon(bar, noto, style.TimeFill);
            ResourceChip(bar, noto, FW.Grenze, controller);
            EditorUtility.SetDirty(controller);
        }

        // Prep clock as the sample compass (990x70): stepped gradient, Bar_Horizontal01 frame, curlicues left/right/below.
        static void Ribbon(RectTransform bar, TMP_FontAsset noto, Color timeFill)
        {
            var line = (RectTransform)bar.Find("PrepClock");
            FW.Place(line, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(420f, 30f));
            RectTransform visual = FW.FreshVisual(line);
            var shadow = FW.Image(visual, "SPR_Shadow", Shadow, new Color32(0x16, 0x16, 0x16, 0x88), 1f * P);
            FW.Stretch(shadow.rectTransform, new Vector2(-60f, -40f + 10f) * S, new Vector2(60f, 40f + 10f) * S);
            var gradient = FW.Image(visual, "SPR_Background_Gradient", "SPR_HUD_FantasyWarrior_Gradient_Outwards_Stepped02",
                new Color32(0x03, 0x16, 0x2B, 0xA6), 0.7f * P);
            FW.Stretch(gradient.rectTransform, Vector2.zero, Vector2.zero);
            var box = FW.Image(visual, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x0C, 0x0C, 0x0C, 0x80), 2.6f * P);
            FW.Stretch(box.rectTransform, Vector2.zero, Vector2.zero);
            var frame = FW.Image(visual, "SPR_Frame", "SPR_FantasyWarrior_Bar_Horizontal01", Color.white, 1.5f * P);
            FW.Stretch(frame.rectTransform, new Vector2(-14f, -5f) * S, new Vector2(14f, 5f) * S);
            var below = FW.Image(visual, "SPR_Curlicue_Bottom", "SPR_FantasyWarrior_Greeble_Curlicue04_Down", Color.white, 1f);
            FW.Place(below.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f) * S, new Vector2(120f, 90f) * S, new Vector2(0.5f, 1f));
            var left = FW.Image(visual, "SPR_Curlicue_Left", "SPR_FantasyWarrior_Greeble_Curlicue45_Left", Color.white, 1f);
            FW.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(-8f, 0f) * S, new Vector2(100f, 100f) * S, new Vector2(1f, 0.5f));
            var right = FW.Image(visual, "SPR_Curlicue_Right", "SPR_FantasyWarrior_Greeble_Curlicue45_Right", Color.white, 1f);
            FW.Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(8f, 0f) * S, new Vector2(100f, 100f) * S, new Vector2(0f, 0.5f));

            var text = line.Find("Text").GetComponent<TMP_Text>();
            FW.Font(text, noto, 15f, Cream, TextAlignmentOptions.Center);
            text.raycastTarget = false;
            FW.Stretch(text.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, 0f));
            var track = (RectTransform)line.Find("Track");
            FW.Place(track, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(360f, 5f), new Vector2(0.5f, 0f));
            Gauge(track, timeFill);
        }

        // Gold as the sample reward currency plate (10_..._Misc01 Award_Currency): dark parchment plate, gold coin icon,
        // gold Grenze number; materials follow in cream Noto (feedback 2026-11-22: the text-only chip looked cheap).
        static void ResourceChip(RectTransform bar, TMP_FontAsset noto, TMP_FontAsset grenze, PrepTopBarHud controller)
        {
            Transform old = bar.Find("ResourceChip");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var chip = FW.Node(bar, "ResourceChip");
            FW.Place(chip, new Vector2(1f, 1f), new Vector2(-24f, -30f), new Vector2(0f, 36f), new Vector2(1f, 0.5f));
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 16, 0, 0);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform visual = FW.FreshVisual(chip);
            visual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var plate = FW.Image(visual, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_Small_Parchment01", new Color32(0x1F, 0x2B, 0x2E, 0xE6), 3.5f);
            FW.Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            var frame = FW.Image(visual, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box21_Variant01", new Color(0.78f, 0.78f, 0.78f, 1f), 8f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);

            var coin = FW.Image(chip, "ICON_Currency", "ICON_FantasyWarrior_Inventory_Currency01_Underlay", new Color32(0xE9, 0xBF, 0x3F, 0xFF), 1f);
            coin.preserveAspect = true;
            var coinSize = coin.gameObject.AddComponent<LayoutElement>();
            coinSize.preferredWidth = coinSize.preferredHeight = 28f;
            var gold = FW.NewLabel(chip, "Gold", "0", grenze, 22f, new Color32(0xE4, 0xBD, 0x3F, 0xFF), TextAlignmentOptions.MidlineLeft);
            controller.ConfigureGold(gold);

            var resources = bar.Find("Resources").GetComponent<TMP_Text>();
            resources.rectTransform.SetParent(chip, false);
            resources.rectTransform.SetAsLastSibling();
            FW.Font(resources, noto, 15f, Cream, TextAlignmentOptions.MidlineLeft);
            resources.raycastTarget = false;
            resources.margin = new Vector4(6f, 0f, 0f, 0f);
        }

        // ---------------------------------------------------------------- squad cards

        // Sample Companion_Portrait02 (portrait box with HP plate) on the left, name, morale bar and line on the right.
        static void SquadCards(GameFlowHud hud, HudStyle style, TMP_FontAsset noto)
        {
            var column = (RectTransform)hud.transform.Find(PrepHudSetup.CardsName);
            column.anchoredPosition = CardsAt;
            column.sizeDelta = new Vector2(CardWidth, column.sizeDelta.y);
            column.GetComponent<VerticalLayoutGroup>().spacing = CardSpacing;

            var card = (RectTransform)column.Find("CardTemplate");
            card.GetComponent<LayoutElement>().preferredHeight = CardHeight;
            Bare(card);
            FW.ClearChildrenExcept(card);
            RectTransform visual = FW.FreshVisual(card);

            var portrait = FW.Node(visual, "Portrait");
            FW.Place(portrait, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(56f, CardHeight), new Vector2(0f, 0.5f));
            Fill(FW.Image(portrait, "SPR_Background", "SPR_HUD_FantasyWarrior_Gradient_Vertical_Solid01", new Color32(0x00, 0x13, 0x35, 0xFF), 1f));
            var gradient = FW.Image(portrait, "SPR_Gradient", "SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01", new Color32(0x31, 0x63, 0xDB, 0xE6), 1f);
            Fill(gradient);
            gradient.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            var tracery = FW.Image(portrait, "SPR_Tracery", "SPR_HUD_FantasyWarrior_Tracery_Diamond01", new Color32(0x4D, 0x77, 0xE0, 0x66), 1f);
            // The sample lets the diamond tracery hang 90 below each portrait into the gap; kept inside the box here.
            FW.Stretch(tracery.rectTransform, Vector2.zero, Vector2.zero);
            var emblem = FW.Image(portrait, "ICON", "ICON_FantasyWarrior_Inventory_Swords01_Clean", new Color(1f, 1f, 1f, 0.92f), 1f);
            emblem.preserveAspect = true;
            FW.Place(emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 7f), new Vector2(34f, 34f));
            Fill(FW.Image(portrait, "SPR_Vignette", "SPR_HUD_FantasyWarrior_Vignette_Box_Large01", new Color32(0x01, 0x0E, 0x25, 0xB2), 3f * P));
            var frame = FW.Image(portrait, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box_Medium01_Variant01", Color.white, 3f * P);
            FW.Stretch(frame.rectTransform, new Vector2(-10f, -10f) * S, new Vector2(10f, 10f) * S);
            var detail = FW.Image(portrait, "SPR_FrameDetail", "SPR_FantasyWarrior_Frame_Box_Medium01_Detail01", Color.white, 2f * P);
            detail.rectTransform.anchorMin = new Vector2(0f, 0.5f); detail.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            detail.rectTransform.sizeDelta = new Vector2(90f, 60f) * S;
            // Selection: a bright gold outline just outside the portrait frame, on only while selected
            // (feedback 2026-11-22: replaces the round glow behind the card).
            var outline = FW.Image(portrait, "SPR_SelectOutline", "SPR_HUD_FantasyWarrior_Frame_Box_Small02", new Color32(0xFF, 0xD8, 0x7A, 0xFF), 4f);
            FW.Stretch(outline.rectTransform, new Vector2(-6f, -6f), new Vector2(6f, 6f));
            outline.gameObject.SetActive(false);

            // Headcount plate over the portrait bottom (sample HitPoints): dark red track, red fill, count on top.
            var plate = FW.Node(card, "HeadcountTrack");
            FW.Place(plate, new Vector2(0f, 0f), new Vector2(3f, 3f), new Vector2(50f, 16f), Vector2.zero);
            var plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = FW.Sprite("SPR_HUD_FantasyWarrior_Box_White01");
            plateImage.color = new Color32(0x3A, 0x0E, 0x10, 0xE6);
            plateImage.raycastTarget = false;
            var peopleFill = FW.Node(plate, "Fill");
            peopleFill.anchorMin = Vector2.zero; peopleFill.anchorMax = Vector2.one; peopleFill.offsetMin = peopleFill.offsetMax = Vector2.zero;
            var peopleImage = peopleFill.gameObject.AddComponent<Image>();
            peopleImage.sprite = plateImage.sprite;
            peopleImage.color = new Color32(0xB4, 0x30, 0x2F, 0xFF);
            peopleImage.raycastTarget = false;
            var people = Label(card, "Headcount", "0/0명", noto, 12f, Color.white, TextAlignmentOptions.Center);
            FW.Place(people.rectTransform, new Vector2(0f, 0f), new Vector2(3f, 3f), new Vector2(50f, 16f), Vector2.zero);

            var title = Label(card, "Title", "부대 1", noto, 15f, Color.white, TextAlignmentOptions.TopLeft);
            Outline(title);
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(68f, -6f), new Vector2(CardWidth - 72f, 20f), new Vector2(0f, 1f));
            var moraleTrack = FW.Node(card, "MoraleTrack");
            FW.Place(moraleTrack, new Vector2(0f, 1f), new Vector2(70f, -31f), new Vector2(CardWidth - 86f, 7f), new Vector2(0f, 1f));
            Image moraleImage = Gauge(moraleTrack, style.MoraleFill(MoraleBand.Normal));
            var moraleFrame = FW.Image(card, "MoraleFrame", "SPR_FantasyWarrior_Bar_Horizontal02", Color.white, 3f * P);
            FW.Place(moraleFrame.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -26f), new Vector2(CardWidth - 74f, 17f), new Vector2(0f, 1f));
            var morale = Label(card, "Morale", "사기 -", noto, 12f, Color.white, TextAlignmentOptions.TopLeft);
            Outline(morale);
            FW.Place(morale.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -44f), new Vector2(CardWidth - 72f, 16f), new Vector2(0f, 1f));

            var template = card.GetComponent<PrepSquadCard>();
            template.Configure(null, title, people, peopleFill, morale, (RectTransform)moraleImage.transform, moraleImage);
            template.ConfigureFrame(null);
            template.ConfigureSelectionOutline(outline.gameObject);
            EditorUtility.SetDirty(template);

            var message = column.Find("Message").GetComponent<TMP_Text>();
            FW.Font(message, noto, 14f, style.Warning, TextAlignmentOptions.TopLeft);
            Outline(message);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.raycastTarget = false;
        }

        // ---------------------------------------------------------------- HP and battle-start plate

        // Sample health bar (02_..._HPBars_Stats HUD_HealthBar01) at half the sample size so it reads at a glance
        // (feedback 2026-11-22: the hotbar bar was too thin): heart in a diamond box on the left, hexagon slider box with
        // vignette and Bar_Horizontal12 frame, value in white Grenze inside the bar.
        const float H = 0.5f;

        static void Health(GameFlowHud hud, TMP_FontAsset grenze)
        {
            var panel = (RectTransform)hud.transform.Find("PlayerHealth");
            if (panel == null) return;
            FW.Place(panel, Vector2.zero, new Vector2(28f, 26f), new Vector2(320f, 80f * H), Vector2.zero);
            Bare(panel);
            FW.FreshVisual(panel);
            // A previous run moved the wired Track and Value into Box; bring them back before Box is rebuilt.
            Transform previous = panel.Find("Box");
            if (previous != null)
                foreach (string wired in new[] { "Track", "Value" })
                {
                    Transform child = previous.Find(wired);
                    if (child != null) child.SetParent(panel, false);
                }
            foreach (string name in new[] { "BarFrame", "Box", "IconBox" })
            {
                Transform old = panel.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            // SliderBox: stretched across, shifted right of the icon (sample pos 40, size -80 x 40).
            var box = FW.Node(panel, "Box");
            box.anchorMin = new Vector2(0f, 0.5f); box.anchorMax = new Vector2(1f, 0.5f); box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(40f * H, 0f); box.sizeDelta = new Vector2(-80f * H, 40f * H);
            var plate = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Mask_Hexagon01", new Color32(0x1F, 0x2B, 0x2E, 0xCC), 2f / H);
            FW.Stretch(plate.rectTransform, new Vector2(-5f, -5f) * H, new Vector2(5f, 5f) * H);
            var track = (RectTransform)panel.Find("Track");
            track.SetParent(box, false);
            FW.Stretch(track, Vector2.zero, Vector2.zero);
            Image fill = Gauge(track, new Color32(0xF1, 0x31, 0x31, 0xFF));
            // The sample slider has no mask and no track color; the hexagon plate is the track.
            UnityEngine.Object.DestroyImmediate(track.GetComponent<Mask>());
            UnityEngine.Object.DestroyImmediate(track.GetComponent<Image>());
            fill.pixelsPerUnitMultiplier = 2f / H;
            var vignette = FW.Image(box, "SPR_Vignette", "SPR_HUD_FantasyWarrior_Vignette_Hexagon01", new Color32(0x1F, 0x2B, 0x2E, 0xFF), 3f / H);
            FW.Stretch(vignette.rectTransform, new Vector2(-10f, -5f) * H, new Vector2(10f, 5f) * H);
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Bar_Horizontal12_Variant01", new Color32(0xCA, 0xC7, 0xC7, 0xFF), 3f / H);
            FW.Stretch(frame.rectTransform, new Vector2(-14f, -8f) * H, new Vector2(14f, 8f) * H);

            // IconBox: rotated dark square in a diamond frame with the heart (sample pos 8, size 90).
            var icon = FW.Node(panel, "IconBox");
            FW.Place(icon, new Vector2(0f, 0.5f), new Vector2(8f * H, 0f), new Vector2(90f, 90f) * H, new Vector2(0f, 0.5f));
            var square = FW.Image(icon, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x33, 0x27, 0x16, 0xFF), 2f);
            FW.Stretch(square.rectTransform, Vector2.zero, Vector2.zero);
            square.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            var diamond = FW.Image(icon, "SPR_Frame", "SPR_FantasyWarrior_Frame_Diamond01", Color.white, 4f);
            FW.Stretch(diamond.rectTransform, new Vector2(-35f, -35f) * H, new Vector2(35f, 35f) * H);
            var heart = FW.Image(icon, "ICON", "SPR_FantasyWarrior_Heart02", new Color32(0xFF, 0xB3, 0xB3, 0xFF), 1f);
            heart.preserveAspect = true;
            FW.Stretch(heart.rectTransform, new Vector2(10f, 0f) * H, new Vector2(0f, -10f) * H);

            var value = panel.Find("Value").GetComponent<TMP_Text>();
            value.rectTransform.SetParent(box, false);
            value.rectTransform.SetAsLastSibling();
            FW.Font(value, grenze, 19f, Color.white, TextAlignmentOptions.Center);
            value.raycastTarget = false;
            FW.Stretch(value.rectTransform, new Vector2(20f, 0f), new Vector2(0f, 1f));
        }

        // Sample XP log item as the battle-start plate: blue vertical gradient, Curlicue02 hanging below, hold gauge inside.
        static void StartPlate(GameFlowHud hud, TMP_FontAsset noto)
        {
            var prompt = (RectTransform)hud.transform.Find("InteractionPrompt");
            if (prompt == null) return;
            Bare(prompt);
            RectTransform visual = FW.FreshVisual(prompt);
            var shadow = FW.Image(visual, "SPR_Shadow", Shadow, new Color32(0x16, 0x16, 0x16, 0x88), 1f * P);
            FW.Stretch(shadow.rectTransform, new Vector2(-16f, -10f), new Vector2(16f, 8f));
            var basePlate = FW.Image(visual, "SPR_Base", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x0B, 0x16, 0x22, 0xB3), 1f);
            FW.Stretch(basePlate.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var gradient = FW.Image(visual, "SPR_Gradient", "SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01", new Color32(0x07, 0x68, 0xA8, 0xE6), 1.4f);
            FW.Stretch(gradient.rectTransform, new Vector2(8f, -10f), new Vector2(-8f, 0f));
            var additive = FW.Image(gradient.rectTransform, "SPR_Gradient_Additive", "SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01",
                new Color32(0x07, 0x68, 0xA8, 0x69), 1.4f);
            Fill(additive);
            // The sample slices Curlicue02 across a 420-wide plate; on this wider plate that stretched it into a long bar
            // (feedback 2026-11-22), so it is drawn whole at a fixed size, centered under the plate.
            var curl = FW.Image(visual, "SPR_Greeble_Curlicue", "SPR_FantasyWarrior_Greeble_Curlicue02", Color.white, 1f);
            curl.type = Image.Type.Simple;
            curl.preserveAspect = true;
            FW.Place(curl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(170f, 44f), new Vector2(0.5f, 1f));

            var hint = prompt.GetComponentInChildren<TMP_Text>(true);
            FW.Font(hint, noto, 16f, Cream, TextAlignmentOptions.Center);
            hint.raycastTarget = false;
            hint.textWrappingMode = TextWrappingModes.Normal;

            Transform oldGauge = prompt.Find("HoldGauge");
            if (oldGauge != null) UnityEngine.Object.DestroyImmediate(oldGauge.gameObject);
            var track = FW.Node(prompt, "HoldGauge");
            track.anchorMin = Vector2.zero; track.anchorMax = new Vector2(1f, 0f); track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(0f, 7f); track.sizeDelta = new Vector2(-80f, 5f);
            Color start = new Color32(0xF6, 0xCC, 0x72, 0xFF), wait = new Color32(0x59, 0xB6, 0xF0, 0xFF);
            Image fill = Gauge(track, start);
            hud.ConfigureHoldGauge((RectTransform)fill.transform, fill, start, wait);
            EditorUtility.SetDirty(hud);
        }

        // ---------------------------------------------------------------- helpers

        // Gauge in the sample slider look: Bar_Diamond01 track that masks a Bar_Horizontal05 fill. Returns the fill.
        static Image Gauge(RectTransform track, Color fillColor)
        {
            var image = Get<Image>(track.gameObject);
            image.sprite = FW.Sprite(BarTrack);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f * P;
            image.color = TrackColor;
            image.raycastTarget = false;
            Get<Mask>(track.gameObject).showMaskGraphic = true;

            Transform existing = track.Find("Fill");
            var fill = existing != null ? (RectTransform)existing : FW.Node(track, "Fill");
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = Get<Image>(fill.gameObject);
            fillImage.sprite = FW.Sprite(BarFill);
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 2f * P;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        // Clears the flat panel look PrepHudSetup paints (own image and the Frame child); the art goes under Visual.
        static void Bare(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image != null) { image.sprite = null; image.color = Color.clear; image.raycastTarget = false; }
            Transform frame = rect.Find(PrepHudSetup.FrameName);
            if (frame != null) UnityEngine.Object.DestroyImmediate(frame.gameObject);
        }

        static void Fill(Image image) => FW.Stretch(image.rectTransform, Vector2.zero, Vector2.zero);

        static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment)
        {
            Transform old = parent.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            return FW.NewLabel(parent, name, text, font, size, color, alignment);
        }

        static T Get<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
