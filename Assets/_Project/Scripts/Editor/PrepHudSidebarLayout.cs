using System;
using ProjectNeo.Flow;
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
    /// UI-12 preparation HUD, sidebar layout (picked 2026-11-22 as candidate B of three free rearrangements). Runs
    /// <see cref="PrepHudLayoutA.Apply"/> for the art and wiring, then moves the pieces into a left column: a header panel
    /// (stage title with ascension, hourglass prep clock) inside the top bar and a squad panel (cards, message, HP bar) under it.
    /// Gold chip and enemy preview stay top-right, the battle-start plate bottom-center and the key list bottom-right.
    /// The top bar stays the last sibling and an invisible full-width strip, so it still draws over facility windows; its
    /// visible pieces (header panel, gold chip) never overlap the cards or the enemy preview, which hangs inside the strip
    /// right under the chip. Running it again gives the same result; running layout A alone undoes it.
    /// </summary>
    public static class PrepHudSidebarLayout
    {
        // 1280x720 Game canvas units.
        // Edge: every panel and the gold chip sit this far from the screen edges (user 2026-11-22: same margin on all sides;
        // only the gold chip moved, the key guide keeps its caps on the chip's left line).
        // Width: the left panels match the right ones (gold chip and enemy preview, ChipMinWidth 260; user 2026-11-22, was 300).
        const float Edge = 12f, Width = ChipMinWidth, HeaderHeight = 112.5f, PanelGap = 6f;
        const float BarHeight = Edge + HeaderHeight + 4f;
        const float Inner = 24f, InnerWidth = Width - 2f * (Inner - Edge);
        const float HealthHeight = 76f, HealthBottom = 30.5f;
        // Enemy preview (V) right under the gold chip, with the same gap as between the header and the squad panel. It uses
        // the chip's plate and frame and the chip's width, so both frames line up (captures, user 2026-11-22).
        static readonly Vector2 IntelAt = new Vector2(-Edge, -(Edge + ChipHeight + 9.5f));

        [MenuItem("ProjectNeo/Setup/Preparation HUD - Sidebar Layout (UI-12)")]
        public static void Apply()
        {
            PrepHudLayoutA.Apply();
            var scene = EditorSceneManager.GetActiveScene();
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            if (hud == null) throw new InvalidOperationException("Game HUD is required.");
            TMP_FontAsset noto = FW.Noto;

            Header(hud, noto);
            SquadPanel(hud);
            // UI-12 squad card option A (user pick 2026-11-22): empty portrait, number badge, order badge, headcount segments.
            PrepHudSquadCardLayout.Apply(hud, AssetDatabase.LoadAssetAtPath<HudStyle>(PrepHudSetup.StylePath), noto);
            Health(hud);
            // UI-07 equipment window option B (user pick 2026-11-23), before the windows are moved right of the column.
            EquipmentWindowLayout.Apply(hud);
            // UI-13 facility window option C (user pick 2026-11-24): screen-centered like the equipment window.
            var facilities = UnityEngine.Object.FindAnyObjectByType<GameFacilities>(FindObjectsInactive.Include);
            if (facilities != null)
            {
                var data = new SerializedObject(facilities);
                FacilityWindowLayout.Apply(hud, facilities, (ProjectNeo.Run.RunConfig)data.FindProperty("startConfig").objectReferenceValue,
                    (GamePhaseView)data.FindProperty("phaseView").objectReferenceValue);
            }
            GoldChip(hud);
            IntelPanel(hud, noto);
            KeyGuide(hud, noto);
            HoldRing(hud, noto);
            ContextPrompt(hud, noto);
            Shrink(hud);
            // The top bar stays the last sibling so it draws over the facility windows (PREP-06 rule, GamePrepTimePlayTests).
            hud.transform.Find(PrepHudSetup.BarName).SetAsLastSibling();
            var intel = hud.GetComponent<EnemyIntelHud>();
            if (intel != null)
            {
                intel.ConfigurePlacement(new Vector2(1f, 1f), IntelAt * HudScale, new Vector2(0f, 1f), EnemyIntelHudSetup.Offset);
                intel.ConfigurePreviewScale(HudScale);
                EditorUtility.SetDirty(intel);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Whole preparation HUD at 0.8 (user 2026-11-22: about 20% smaller), except the HP portrait and bar. Every piece is
        // scaled about its own screen anchor (scale and anchored position both times HudScale), so the margins, gaps and
        // alignments between pieces shrink with them. The enemy preview gets the same scale at runtime, in preparation only.
        const float HudScale = 0.8f;

        static void Shrink(GameFlowHud hud)
        {
            ScaleAboutAnchor(hud.transform.Find(PrepHudSetup.BarName));
            foreach (string name in new[] { PrepHudSetup.CardsName, PrepHudSetup.KeyListName, HoldRingName, NoticeName, PrepHudSetup.ContextName })
                ScaleAboutAnchor(hud.transform.Find(name));
        }

        static void ScaleAboutAnchor(Transform target)
        {
            if (!(target is RectTransform rect)) return;
            if (rect.anchorMin != rect.anchorMax)
            {
                // Stretched (the top bar strip, its Visual): scale the pieces inside it instead.
                foreach (Transform child in rect) ScaleAboutAnchor(child);
                return;
            }
            rect.localScale = Vector3.one * HudScale;
            rect.anchoredPosition *= HudScale;
        }

        // Header panel at the top of the column, drawn by the top bar (which keeps the gold chip on its right end).
        // User pick 2026-11-22 (option C-2 of the header mockups): dark plate in the thick gold Box22 frame (the gold chip
        // look), stage title and stage line, a gold line with a small diamond, then the prep clock row below.
        static void Header(GameFlowHud hud, TMP_FontAsset noto)
        {
            var bar = (RectTransform)hud.transform.Find(PrepHudSetup.BarName);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            RectTransform visual = FW.FreshVisual(bar); // drops the layout A pennant
            var panel = FW.Node(visual, "Sidebar_Header");
            FW.Place(panel, new Vector2(0f, 1f), new Vector2(Edge, -Edge), new Vector2(Width, HeaderHeight), new Vector2(0f, 1f));
            GoldPlate(panel);

            var title = bar.Find("StageTitle").GetComponent<TMP_Text>();
            title.fontSize = 21f; // was 26 (user 2026-11-22: too big for the 260-wide header)
            title.alignment = TextAlignmentOptions.MidlineLeft; // vertically centred with the ascension tag (user 2026-11-23)
            // Glyph offsets inside the boxes were measured on captures so the visible padding is even (about 14 from the
            // frame's outer line on every side, user 2026-11-22).
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Inner - 1f, -Edge - 8.5f), new Vector2(InnerWidth, 32f), new Vector2(0f, 1f));
            // The "준비 · 스테이지 N" line repeated the title (user 2026-11-22): hidden but still wired (PrepTopBarHud.StageText),
            // and only the ascension shows, small on the right of the title row.
            bar.Find("Stage").gameObject.SetActive(false);
            Ascension(hud, bar, title);
            Divider(panel, Inner - Edge, TitleRow + 8f);
            Clock(hud, bar, noto);
        }

        internal const string AscensionName = "Ascension";
        const float TitleRow = 39.5f;

        static void Ascension(GameFlowHud hud, RectTransform bar, TMP_Text title)
        {
            Transform old = bar.Find(AscensionName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var label = FW.NewLabel(bar, AscensionName, "승천 1", title.font, 16f, FW.Cream, TextAlignmentOptions.MidlineRight);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            // Bottom-aligned with the title box so it sits on the title's baseline area.
            FW.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(Inner - 1f, -Edge - 8.5f), new Vector2(InnerWidth + 1f, 32f), new Vector2(0f, 1f));
            label.gameObject.SetActive(false);
            var top = hud.GetComponent<PrepTopBarHud>();
            if (top != null)
            {
                top.ConfigureAscension(label);
                EditorUtility.SetDirty(top);
            }
        }

        // Gold line across the header with a small gold diamond in the middle.
        static void Divider(RectTransform panel, float inset, float y)
        {
            float half = (Width - 2f * inset) * 0.5f - DiamondRoom;
            Color line = new Color(FW.Gold.r, FW.Gold.g, FW.Gold.b, 0.75f);
            for (int side = -1; side <= 1; side += 2)
            {
                var stroke = FW.Image(panel, side < 0 ? "SPR_DividerLeft" : "SPR_DividerRight", "SPR_HUD_FantasyWarrior_Box_White01", line, 100f);
                stroke.type = Image.Type.Simple;
                FW.Place(stroke.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * (DiamondRoom + half * 0.5f), -y), new Vector2(half, 1.5f));
            }
            var diamond = FW.Image(panel, "SPR_DividerDiamond", "SPR_HUD_FantasyWarrior_Box_White01", FW.Gold, 100f);
            diamond.type = Image.Type.Simple;
            FW.Place(diamond.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(DiamondSize, DiamondSize));
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        // Prep clock row (user 2026-11-22): the pack's hourglass icon on the left, "remaining/budget" above (budget dimmed,
        // like the HP number) and one gold segment per hour below. The PREP-06 one-line text and the layout A gauge stay
        // wired but hidden, so PrepClockHud.Text and FillRatio keep updating.
        internal const string HourglassName = "Hourglass";
        const float DiamondSize = 7f, DiamondRoom = 10f;
        const float ClockTop = TitleRow + 18f, ClockHeight = 42f, HourglassSize = 40f, ClockGauge = 12f, HoursPerSegment = 1f;

        static void Clock(GameFlowHud hud, RectTransform bar, TMP_FontAsset noto)
        {
            var line = (RectTransform)bar.Find("PrepClock");
            FW.Place(line, new Vector2(0f, 1f), new Vector2(Inner, -Edge - ClockTop), new Vector2(InnerWidth, ClockHeight), new Vector2(0f, 1f));
            foreach (string old in new[] { NumberName, SegmentsName })
            {
                Transform existing = line.Find(old);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            line.Find("Text").gameObject.SetActive(false);
            line.Find("Track").gameObject.SetActive(false);

            RectTransform clockVisual = FW.FreshVisual(line);
            var icon = FW.Image(clockVisual, HourglassName, "ICON_FantasyWarrior_Status_Time01_Stroke", FW.Gold, 1f);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            // The hourglass sprite has transparent room on its left; pulled left so its visible edge lines up with the title.
            FW.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-8.5f, 0f), new Vector2(HourglassSize, HourglassSize), new Vector2(0f, 0.5f));

            float right = HourglassSize + 8f;
            var row = FW.Node(line, SegmentsName);
            FW.Place(row, Vector2.zero, new Vector2(right, 2f), new Vector2(InnerWidth - right, ClockGauge), Vector2.zero);
            var plate = FW.Image(row, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x05, 0x07, 0x08, 0xE6), 4f);
            FW.Stretch(plate.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var segment = FW.Image(row, "SegmentTemplate", "SPR_HUD_FantasyWarrior_Box_White01", FW.Gold, 4f);
            segment.type = Image.Type.Filled;
            segment.fillMethod = Image.FillMethod.Horizontal;
            segment.fillOrigin = (int)Image.OriginHorizontal.Left;
            segment.gameObject.SetActive(false);

            var number = FW.NewLabel(line, NumberName, "8<alpha=#99>/8", FW.Grenze, 22f, Color.white, TextAlignmentOptions.BottomLeft);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            FW.Place(number.rectTransform, Vector2.zero, new Vector2(right, 2f + ClockGauge + 2f), new Vector2(InnerWidth - right, 24f), Vector2.zero);
            foreach (var graphic in line.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var clock = hud.GetComponent<PrepClockHud>();
            if (clock != null)
            {
                clock.ConfigureHourglassLayout(number, segment, HoursPerSegment);
                EditorUtility.SetDirty(clock);
            }
        }

        // Enemy preview (user 2026-11-22): titled "적 정보", one row per enemy kind with an empty portrait frame (art comes
        // later), the name and "× count"; same plate and frame as the gold chip and exactly its width. The multi-line body
        // stays wired (EnemyIntelHud.Body) and only shows when there are no enemies.
        internal const string IntelRowTemplateName = "IntelRowTemplate";
        const float IntelWidth = ChipMinWidth, IntelPad = 12f, IntelRowHeight = 30f, IntelPortrait = 28f;

        static void IntelPanel(GameFlowHud hud, TMP_FontAsset noto)
        {
            var panel = (RectTransform)hud.transform.Find(EnemyIntelHudSetup.PanelName);
            var controller = hud.GetComponent<EnemyIntelHud>();
            if (panel == null || controller == null) return;
            for (int i = panel.childCount - 1; i >= 0; i--)
                if (panel.GetChild(i).name.StartsWith("IntelRow", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);
            Transform oldFrame = panel.Find(PrepHudSetup.FrameName);
            if (oldFrame != null) UnityEngine.Object.DestroyImmediate(oldFrame.gameObject);
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null) { panelImage.sprite = null; panelImage.color = Color.clear; panelImage.raycastTarget = false; }
            panel.sizeDelta = new Vector2(IntelWidth, panel.sizeDelta.y);

            RectTransform visual = FW.FreshVisual(panel);
            visual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            GoldPlate(visual);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)IntelPad, (int)IntelPad, 10, (int)IntelPad);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            var title = panel.Find("Title").GetComponent<TMP_Text>();
            FW.Font(title, noto, 18f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            title.text = EnemyIntelHud.PreviewTitle;
            var body = panel.Find("Rows").GetComponent<TMP_Text>();
            FW.Font(body, noto, 16f, FW.Cream, TextAlignmentOptions.TopLeft);
            body.gameObject.SetActive(false);

            var row = FW.Node(panel, IntelRowTemplateName);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = IntelRowHeight;
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 8f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false; line.childForceExpandHeight = false;

            // Empty portrait: dark blue square (the HP portrait colour) in the thin gold Box22 frame, no picture yet.
            var portrait = FW.Node(row, "Portrait");
            var portraitSize = portrait.gameObject.AddComponent<LayoutElement>();
            portraitSize.minWidth = portraitSize.preferredWidth = IntelPortrait;
            portraitSize.minHeight = portraitSize.preferredHeight = IntelPortrait;
            var fill = FW.Image(portrait, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x12, 0x2A, 0x5A, 0xFF), 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var picture = FW.Node(portrait, "Picture").gameObject.AddComponent<Image>();
            picture.enabled = false; // filled in later when enemy portraits exist
            picture.preserveAspect = true;
            FW.Stretch(picture.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var frame = FW.Image(portrait, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 1f), 20f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);

            var name = FW.NewLabel(row, "Name", "사다리병", noto, 16f, Color.white, TextAlignmentOptions.MidlineLeft);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var count = FW.NewLabel(row, "Count", "× 6", noto, 16f, FW.Cream, TextAlignmentOptions.MidlineRight);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            foreach (var graphic in row.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = row.gameObject.AddComponent<EnemyIntelRowView>();
            view.Configure(picture, name, count);
            row.gameObject.SetActive(false);
            controller.ConfigureRows(view);
            EditorUtility.SetDirty(controller);
        }

        // Dark plate in the thick gold Box22 frame, the gold chip look (header).
        internal static void GoldPlate(RectTransform panel) => GoldPlate(panel, new Color32(0x10, 0x16, 0x1A, 0xD8));

        internal static void GoldPlate(RectTransform panel, Color plateColor)
        {
            var plate = FW.Image(panel, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", plateColor, 2f);
            FW.Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            var frame = FW.Image(panel, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 1f), 10f);
            FW.Stretch(frame.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
        }

        // With a full page of cards the visible padding to the frame is the same on all four sides, about 11 (measured on
        // captures, user 2026-11-22; CardTopPad was 9 and SquadPanelBottomPad 8).
        const float CardTopPad = 1f, CardLeftPad = 6f, SquadPanelBottomPad = 11.5f;

        // Squad panel: the card column stretches from under the header to above the HP bar; its Visual draws the panel
        // around the column only. The column is moved before PlayerHealth so the HP bar draws over the panel.
        static void SquadPanel(GameFlowHud hud)
        {
            var column = (RectTransform)hud.transform.Find(PrepHudSetup.CardsName);
            FW.DestroyComponent<ContentSizeFitter>(column.gameObject);
            // CardTopPad / CardLeftPad: the cards start a little further in so their portrait frames keep the same padding
            // from the panel frame as the HP portrait (measured on captures, user 2026-11-22).
            float top = BarHeight + 12f + CardTopPad, bottom = HealthBottom + HealthHeight + 18f;
            float left = Inner + CardLeftPad;
            column.anchorMin = Vector2.zero; column.anchorMax = new Vector2(0f, 1f); column.pivot = new Vector2(0f, 1f);
            column.offsetMin = new Vector2(left, bottom);
            column.offsetMax = new Vector2(Inner + InnerWidth, -top);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;

            RectTransform visual = FW.FreshVisual(column);
            visual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var panel = FW.Node(visual, "Sidebar_Squads");
            float panelTop = top - (BarHeight - 4f) - PanelGap; // from the header bottom (+gap) down to the column top
            // The panel ends just under the card column, above the HP portrait and bar (user 2026-11-22: the HP is not part
            // of the squad panel), and uses the header's dark plate and thick gold Box22 frame so both panels line up and
            // match (the parchment Box21 plate sat 2 inside the header frame and looked lighter).
            FW.Stretch(panel, new Vector2(Edge - left, -SquadPanelBottomPad), new Vector2(Width - InnerWidth - (Inner - Edge), panelTop));
            GoldPlate(panel);

            Transform health = hud.transform.Find("PlayerHealth");
            if (health != null && column.GetSiblingIndex() > health.GetSiblingIndex())
                column.SetSiblingIndex(health.GetSiblingIndex());
        }

        // HP at the bottom of the squad panel (same spot in battle, without the panel). User request 2026-11-22: portrait on
        // the left (left empty for now, frame only), current/max number above, segmented bar beside it. The layout A bar
        // (Box with Track and Value) stays wired but hidden, so "HP x / y" and the fill ratio keep updating.
        internal const string PortraitName = "Portrait", NumberName = "Number", SegmentsName = "Segments";
        // Portrait enlarged from 58 (user 2026-11-22: should be bigger).
        const float PortraitSize = 76f, PortraitDrop = 4f, HealthPerSegment = 10f;
        // Even padding to the squad panel frame: the ring moves 1.5 in and the bar ends 1.5 earlier (captures, 2026-11-22).
        // HealthArea: the HP keeps its 300-wide-sidebar size (user 2026-11-22: the HP bar stays as it was when the panels
        // were narrowed to 260), so it no longer follows InnerWidth.
        static readonly Vector2 HealthAt = new Vector2(6.9f, 10.9f);
        const float HealthArea = 276f, HealthLeftPad = 1.5f, HealthWidth = HealthArea - HealthLeftPad - 1.5f;

        static void Health(GameFlowHud hud)
        {
            var panel = (RectTransform)hud.transform.Find("PlayerHealth");
            if (panel == null) return;
            var controller = hud.GetComponent<PlayerHealthHud>();
            // HealthAt: the portrait ring's visible left and bottom edges sit as far from the screen edges as the other panels'
            // frames (about 8.7, measured on captures, user 2026-11-22; was Inner + HealthLeftPad, HealthBottom). The portrait
            // and the bar move together.
            FW.Place(panel, Vector2.zero, HealthAt, new Vector2(HealthWidth, HealthHeight), Vector2.zero);
            foreach (string old in new[] { "IconBox", PortraitName, NumberName, SegmentsName })
            {
                Transform existing = panel.Find(old);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            Transform box = panel.Find("Box");
            if (box != null) box.gameObject.SetActive(false);

            // Portrait (user pick 2026-11-22, option 8 of eight): round dark-blue disc in the thick gold Ring_Large06, no icon
            // yet. A square frame read as an item slot.
            var portrait = FW.Node(panel, PortraitName);
            // Sits a little below the bar's bottom line (user 2026-11-22: looks more natural than an exact match).
            FW.Place(portrait, new Vector2(0f, 0.5f), new Vector2(0f, -PortraitDrop), new Vector2(PortraitSize, PortraitSize), new Vector2(0f, 0.5f));
            Image Round(string name, string sprite, Color color, float inset)
            {
                var image = FW.Image(portrait, name, sprite, color, 1f);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                FW.Stretch(image.rectTransform, new Vector2(inset, inset), new Vector2(-inset, -inset));
                return image;
            }
            Round("SPR_Background", "SPR_HUD_FantasyWarrior_Circle_Medium01_Clean", new Color32(0x12, 0x2A, 0x5A, 0xFF), 7f);
            Round("SPR_Frame", "SPR_FantasyWarrior_Ring_Large06", Color.white, 0f);

            // Bar (user pick 2026-11-22, option 4 of six): dark plate in the thin gold Box22 frame (the gold chip frame), red
            // segments inside, one per HealthPerSegment. Its frame bottom sits on the portrait ring's bottom line (user:
            // align the portrait and the bar on the bottom line); the number sits right above the bar.
            float right = PortraitSize + 12f;
            const float BarHeight = 24f, FrameOut = 4f;
            var row = FW.Node(panel, SegmentsName);
            FW.Place(row, Vector2.zero, new Vector2(right + FrameOut, FrameOut), new Vector2(HealthWidth - right - 2f * FrameOut, BarHeight), Vector2.zero);
            var plate = FW.Image(row, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x10, 0x16, 0x1A, 0xD8), 4f);
            FW.Stretch(plate.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(3, 3, 3, 3);
            line.spacing = 2f;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = true;
            // Plain box sprite so every segment is the same clean block (the bar fill sprite gave uneven rounded ends).
            var segment = FW.Image(row, "SegmentTemplate", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0xE8, 0x3A, 0x34, 0xFF), 4f);
            segment.type = Image.Type.Filled;
            segment.fillMethod = Image.FillMethod.Horizontal;
            segment.fillOrigin = (int)Image.OriginHorizontal.Left;
            segment.gameObject.SetActive(false);
            var barFrame = FW.Image(row, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 1f), 14f);
            FW.Stretch(barFrame.rectTransform, new Vector2(-FrameOut, -FrameOut), new Vector2(FrameOut, FrameOut));
            barFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Number right above the bar: current and max the same size, "/max" slightly dimmed (PrepHudText.HealthNumber).
            // Upright (italic was tried and reverted, user 2026-11-22).
            var number = FW.NewLabel(panel, NumberName, "100<alpha=#99>/100", FW.Grenze, 30f, Color.white, TextAlignmentOptions.BottomLeft);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.raycastTarget = false;
            FW.Place(number.rectTransform, Vector2.zero, new Vector2(right, FrameOut + BarHeight + FrameOut + 1f), new Vector2(HealthWidth - right, 34f), Vector2.zero);
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            if (controller != null)
            {
                controller.ConfigurePortraitLayout(number, segment, HealthPerSegment);
                EditorUtility.SetDirty(controller);
            }
        }

        // Gold chip (user pick 2026-11-22, option 6 of six): dark plate in the thick gold Box22 frame, gold ingot icon
        // instead of the coin, taller than layout A so the icon has room above and below.
        const float ChipHeight = 46f, IngotSize = 28f;

        static void GoldChip(GameFlowHud hud)
        {
            var bar = (RectTransform)hud.transform.Find(PrepHudSetup.BarName);
            var chip = (RectTransform)bar.Find("ResourceChip");
            if (chip == null) return;
            chip.sizeDelta = new Vector2(chip.sizeDelta.x, ChipHeight);
            chip.anchoredPosition = new Vector2(-Edge, -Edge - ChipHeight * 0.5f);
            var layout = chip.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 18, 0, 0); // ingot gap to the frame matches top and bottom (2026-11-22)
            layout.spacing = 8f;

            RectTransform visual = FW.FreshVisual(chip);
            visual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var plate = FW.Image(visual, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x10, 0x16, 0x1A, 0xD8), 2f);
            FW.Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            var frame = FW.Image(visual, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 1f), 10f);
            FW.Stretch(frame.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));

            var icon = chip.Find("ICON_Currency").GetComponent<Image>();
            icon.sprite = FW.Sprite("ICON_SM_Item_Ingot_Gold_01");
            icon.type = Image.Type.Simple;
            icon.color = Color.white;
            icon.preserveAspect = true;
            var size = icon.GetComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = IngotSize;
            var goldLabel = chip.Find("Gold").GetComponent<TMP_Text>();
            goldLabel.fontSize = 25f;

            // Fixed room (user request 2026-11-22): the chip no longer shrinks or grows while the gold count changes.
            // The number gets a reserved slot (about seven digits with commas) and the whole chip has a minimum width;
            // it only grows past that when the material line is long.
            var goldSlot = goldLabel.GetComponent<LayoutElement>();
            if (goldSlot == null) goldSlot = goldLabel.gameObject.AddComponent<LayoutElement>();
            goldSlot.minWidth = GoldSlotWidth;
            var chipSize = chip.GetComponent<LayoutElement>();
            if (chipSize == null) chipSize = chip.gameObject.AddComponent<LayoutElement>();
            chipSize.minWidth = ChipMinWidth;
        }

        // The chip's left edge lines up with the key caps of the key guide below (user request 2026-11-22; the caps, not
        // the HOLD tags). Both hang Edge from the right edge, and the caps start after the HOLD slot and the row spacing;
        // the chip frame bleeds 2 past the chip rect, so the rect is 2 narrower for the outer frame line to meet the caps.
        const float GoldSlotWidth = 110f, ChipFrameBleed = 2f;
        const float ChipMinWidth = GuideWidth - HoldSlotWidth - KeyRowSpacing - ChipFrameBleed;

        // On-screen key guide (user request 2026-11-22, after an FPS on-screen key guide): no panel, a column of rows at the
        // bottom-right, each row = optional HOLD tag, dark translucent key cap, white action text with a drop shadow.
        // KeyListHud fills the rows for the current situation; the old title, text body and "H 키 안내" line stay hidden.
        const float KeyGuideBottom = 7.55f;
        const float GuideWidth = 300f, GuideMargin = Edge, GuideBottom = 30f, RowHeight = 22f, RowSpacing = 3f;
        const float HoldSlotWidth = 32f, KeyRowSpacing = 6f;

        static void KeyGuide(GameFlowHud hud, TMP_FontAsset noto)
        {
            var panel = (RectTransform)hud.transform.Find(PrepHudSetup.KeyListName);
            var keys = hud.GetComponent<KeyListHud>();
            if (panel == null || keys == null) return;
            var shadowed = AssetDatabase.LoadAssetAtPath<Material>(PrepHudLayoutA.OutlinedNotoPath);

            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null) { panelImage.sprite = null; panelImage.color = Color.clear; panelImage.raycastTarget = false; }
            Transform oldFrame = panel.Find(PrepHudSetup.FrameName);
            if (oldFrame != null) UnityEngine.Object.DestroyImmediate(oldFrame.gameObject);
            foreach (string legacy in new[] { "Title", "Rows" })
            {
                Transform child = panel.Find(legacy);
                if (child != null) child.gameObject.SetActive(false);
            }
            for (int i = panel.childCount - 1; i >= 0; i--)
                if (panel.GetChild(i).name.StartsWith("KeyRow", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);

            // Fixed 300 wide column whose key caps start on the gold chip's left line (user 2026-11-22; the chip and the
            // guide both hang Edge from the right edge, hugging the edge with the text itself was tried and reverted).
            // Bottom on the bottom line of the battle-start plate (GameFlowHud compact plate, y 30). When folded, the single
            // H row is right-aligned (KeyGuideRow.Align) so it sits in the bottom-right corner (user pick, 2026-11-22).
            // KeyGuideBottom: the bottom key cap (and the folded H row) sits as far from the screen bottom as the other panels'
            // frames from the screen edges, after the 0.8 HUD scale (user 2026-11-22; was GuideBottom, the plate line).
            FW.Place(panel, new Vector2(1f, 0f), new Vector2(-GuideMargin, KeyGuideBottom), new Vector2(GuideWidth, 0f), new Vector2(1f, 0f));
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            // Fixed list of 15 rows (UI-12): kept compact so it stays under the enemy preview (V) at the top-right.
            layout.spacing = RowSpacing;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var row = FW.Node(panel, "KeyRowTemplate");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = KeyRowSpacing;
            // Folded, the single H row is right-aligned; this pulls its label onto the same screen-edge margin as the panel
            // frames (glyph padding left it about 3 short). Rows are left-aligned when open, so it changes nothing there.
            line.padding = new RectOffset(0, -3, 0, 0);
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;

            // The HOLD tag is gone (user 2026-11-22); its slot stays empty so the key caps keep the gold chip's left line.
            var hold = FW.NewLabel(row, "Hold", string.Empty, noto, 10f, FW.Gold, TextAlignmentOptions.MidlineRight);
            Shadow(hold, shadowed);
            var holdSize = hold.gameObject.AddComponent<LayoutElement>();
            holdSize.preferredWidth = holdSize.minWidth = HoldSlotWidth;

            var key = KeyCap(row, "KeyCap", noto, RowHeight, 13f);
            var cap = (RectTransform)key.transform.parent;

            var action = FW.NewLabel(row, "Action", "상호작용", noto, 15f, Color.white, TextAlignmentOptions.MidlineLeft);
            action.textWrappingMode = TextWrappingModes.NoWrap;
            Shadow(action, shadowed);

            foreach (var label in row.GetComponentsInChildren<TMP_Text>(true)) label.raycastTarget = false;
            // Second key cap (UI-12, user 2026-11-22: "[" and "]" must read as two keys): a copy of the cap right after it,
            // on only in rows with a second key (KeyHint.SecondKey).
            var cap2 = UnityEngine.Object.Instantiate(cap, row);
            cap2.name = "KeyCap2";
            cap2.SetSiblingIndex(cap.GetSiblingIndex() + 1);
            cap2.gameObject.SetActive(false);
            var key2 = cap2.Find("Key").GetComponent<TMP_Text>();
            var joiner = KeyJoiner(row, noto, shadowed);
            joiner.transform.SetSiblingIndex(cap2.GetSiblingIndex());
            var template = row.gameObject.AddComponent<KeyGuideRow>();
            template.Configure(null, key, action);
            template.ConfigureSecondKey(key2);
            template.ConfigureJoiner(joiner);
            row.gameObject.SetActive(false);
            keys.ConfigureRows(template);
            EditorUtility.SetDirty(keys);
        }

        // Crosshair hold ring (user pick 2026-11-22, option 1 of four for the bottom-center plate): the battle-start plate is
        // hidden while exploring; holding B or T fills a ring around the crosshair with a short label under it, and
        // warnings (battlefield not ready, unfinished work, gate) go to one shadowed line at the bottom center.
        internal const string HoldRingName = "HoldRing", NoticeName = "NoticeLine";
        const float RingSize = 52f;

        static void HoldRing(GameFlowHud hud, TMP_FontAsset noto)
        {
            var shadowed = AssetDatabase.LoadAssetAtPath<Material>(PrepHudLayoutA.OutlinedNotoPath);
            var style = AssetDatabase.LoadAssetAtPath<HudStyle>(PrepHudSetup.StylePath);
            foreach (string old in new[] { HoldRingName, NoticeName })
            {
                Transform existing = hud.transform.Find(old);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var holder = FW.Node(hud.transform, HoldRingName);
            FW.Place(holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RingSize, RingSize));
            var track = FW.Image(holder, "Track", "SPR_HUD_FantasyWarrior_Reticle_Ring01_Med", new Color(0f, 0f, 0f, 0.45f), 1f);
            track.type = Image.Type.Simple;
            FW.Stretch(track.rectTransform, Vector2.zero, Vector2.zero);
            var fill = FW.Image(holder, "Fill", "SPR_HUD_FantasyWarrior_Reticle_Ring01_Med", Color.white, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            FW.Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            var label = FW.NewLabel(holder, "Label", "전투 시작 0%", noto, 14f, Color.white, TextAlignmentOptions.Top);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Shadow(label, shadowed);
            FW.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(260f, 22f), new Vector2(0.5f, 1f));
            holder.gameObject.SetActive(false);

            Color warning = style != null ? style.Warning : FW.Danger;
            var notice = FW.NewLabel(hud.transform, NoticeName, "", noto, 15f, warning, TextAlignmentOptions.Bottom);
            notice.textWrappingMode = TextWrappingModes.NoWrap;
            Shadow(notice, shadowed);
            FW.Place(notice.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, GuideBottom), new Vector2(640f, 24f), new Vector2(0.5f, 0f));
            notice.gameObject.SetActive(false);

            foreach (var graphic in holder.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            notice.raycastTarget = false;
            hud.ConfigureHoldRing(fill, label, notice);
            EditorUtility.SetDirty(hud);
        }

        // Interaction prompt under the crosshair (UI-07, user picks 2026-11-23: option A of three, no plate, so it no longer
        // reads as a button; key cap option 2, the black 3D KeyCap below). The action text ("상점 사용") follows the cap
        // in white with the outlined drop shadow. GameFlowHud writes the key into the cap.
        internal const string ContextKeyCapName = "KeyCap";
        const float ContextCapSize = 28f;

        static void ContextPrompt(GameFlowHud hud, TMP_FontAsset noto)
        {
            var box = (RectTransform)hud.transform.Find(PrepHudSetup.ContextName);
            if (box == null) return;
            var shadowed = AssetDatabase.LoadAssetAtPath<Material>(PrepHudLayoutA.OutlinedNotoPath);
            var plate = box.GetComponent<Image>();
            if (plate != null) { plate.sprite = null; plate.color = Color.clear; plate.raycastTarget = false; }
            foreach (string old in new[] { PrepHudSetup.FrameName, ContextKeyCapName })
            {
                Transform existing = box.Find(old);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            var layout = box.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var key = KeyCap(box, ContextKeyCapName, noto, ContextCapSize, 14f);
            key.transform.parent.SetAsFirstSibling();

            var label = box.Find("Text").GetComponent<TMP_Text>();
            FW.Font(label, noto, 17f, Color.white, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            Shadow(label, shadowed);

            hud.ConfigureContextKey(key);
            EditorUtility.SetDirty(hud);
        }

        // Neutral black 3D key cap (UI-07, user pick 2026-11-23, option 2 of three, from the user's sketch): a dark base
        // and a lighter top face raised toward the top, so the base shows below it as the key's thickness. No pack has
        // a keycap sprite (the only key art is a flat white square, and the pack boxes have no 9-slice border, so their
        // corners stretch flat), so both layers use the project's own white rounded 9-slice (RoundedSprite, user
        // 2026-11-23: round corners). Every cap is square (user 2026-11-23: 1:1 buttons); longer key names (F10, Esc)
        // shrink their text to fit the top face instead of widening the cap. Returns the key label.
        static readonly Color KeyCapBase = new Color32(0x15, 0x17, 0x1A, 0xF0), KeyCapTop = new Color32(0x3A, 0x3E, 0x44, 0xFF);
        static readonly Color KeyCapText = new Color32(0xF0, 0xEE, 0xE8, 0xFF);

        internal static TMP_Text KeyCap(Transform parent, string name, TMP_FontAsset noto, float height, float fontSize)
        {
            float side = Mathf.Round(height * 0.13f), lip = Mathf.Round(height * 0.07f), depth = Mathf.Round(height * 0.23f);
            Sprite rounded = RoundedSprite();
            var cap = FW.Node(parent, name);
            var baseImage = cap.gameObject.AddComponent<Image>();
            Rounded(baseImage, rounded, KeyCapBase, height * 0.26f);
            var top = FW.Node(cap, "Top").gameObject.AddComponent<Image>();
            Rounded(top, rounded, KeyCapTop, height * 0.2f);
            FW.Stretch(top.rectTransform, new Vector2(side, depth), new Vector2(-side, -lip));
            top.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            float square = height + 2f;
            var size = cap.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = square;
            size.minHeight = size.preferredHeight = square;
            size.flexibleWidth = size.flexibleHeight = 0f;
            cap.sizeDelta = new Vector2(square, square);

            // Text sits on the top face, the base's thickness kept below it.
            var key = FW.NewLabel(cap, "Key", "F", noto, fontSize, KeyCapText, TextAlignmentOptions.Center);
            key.fontStyle = FontStyles.Bold;
            key.textWrappingMode = TextWrappingModes.NoWrap;
            key.enableAutoSizing = true;
            key.fontSizeMin = Mathf.Max(6f, fontSize * 0.5f);
            key.fontSizeMax = fontSize;
            key.margin = Vector4.zero;
            FW.Stretch(key.rectTransform, new Vector2(side + 1f, depth), new Vector2(-side - 1f, -lip));
            return key;
        }

        // Plain text between two key caps (UI-07, user 2026-11-23: "F1~F10" is not a real key, so F1 and F10 get their own
        // caps with a "~" in between); on only in rows whose hint has a joiner (KeyHint.Joiner).
        internal static TMP_Text KeyJoiner(Transform parent, TMP_FontAsset noto, Material shadowed)
        {
            var joiner = FW.NewLabel(parent, "Joiner", "~", noto, 15f, Color.white, TextAlignmentOptions.Center);
            joiner.textWrappingMode = TextWrappingModes.NoWrap;
            Shadow(joiner, shadowed);
            joiner.gameObject.SetActive(false);
            return joiner;
        }

        // White rounded rectangle with a 9-slice border, generated once into the project (no pack sprite fits).
        const string RoundedPath = "Assets/_Project/Art/UI/SPR_RoundedBox_White.png";
        const int RoundedSize = 64, RoundedRadius = 14, RoundedBorder = 16;

        static Sprite RoundedSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            if (sprite != null) return sprite;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RoundedPath));
            var texture = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false);
            float edge = RoundedSize - 1f - RoundedRadius;
            for (int y = 0; y < RoundedSize; y++)
                for (int x = 0; x < RoundedSize; x++)
                {
                    // Distance outside the rounded rectangle, antialiased over one pixel.
                    float dx = Mathf.Max(RoundedRadius - x, x - edge, 0f), dy = Mathf.Max(RoundedRadius - y, y - edge, 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - RoundedRadius;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - outside)));
                }
            System.IO.File.WriteAllBytes(RoundedPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RoundedPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(RoundedBorder, RoundedBorder, RoundedBorder, RoundedBorder);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        }

        // Sliced rounded plate whose corner radius on screen is about radius canvas units.
        static void Rounded(Image image, Sprite sprite, Color color, float radius)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = RoundedRadius / Mathf.Max(1f, radius);
            image.color = color;
            image.raycastTarget = false;
        }

        static void Shadow(TMP_Text label, Material material)
        {
            if (material == null) return;
            var serialized = new SerializedObject(label);
            serialized.FindProperty("m_sharedMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
