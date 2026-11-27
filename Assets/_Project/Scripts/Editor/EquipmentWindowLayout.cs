using System;
using System.Collections.Generic;
using System.IO;
using ProjectNeo.Economy;
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
    /// UI-07 equipment window (I), option B of three (user pick 2026-11-23); since UI-13 gear and skills sit side by side in
    /// one 800x560 window like the organization (O) window. Paper doll on the left (empty portrait frame,
    /// one square slot per equipment slot, five stats with a hover preview) and the storage as an icon grid on the right
    /// with a detail box under it. Rebuilds the inside of the EquipmentPanel made by <see cref="GameFacilitySetup"/> in the
    /// UI-12 look (dark plate in the thick gold Box22 frame); the skill view keeps the two lists. Runs from
    /// <see cref="PrepHudSidebarLayout.Apply"/>, and running it again gives the same result. Also gives the prototype gear
    /// pack icons (<see cref="AssignItemIcons"/>).
    /// </summary>
    public static class EquipmentWindowLayout
    {
        // Layout (UI-13, user 2026-11-24): gear and skills side by side in one window with no tabs, the same 800x560 plate as
        // the organization (O) and perk (K) windows. Title row at 14/36 and content from 60 like those windows. Left column:
        // the paper doll and stats in two squares, the 3x6 storage grid under them (item info as a tooltip beside the hovered
        // cell). Right column: the skill triangle and the selected skill's details. Bottom row: hero switcher on the left,
        // close on the right, Pad from the bottom edge.
        const float Pad = 24f, TitleTop = 14f, TitleHeight = 36f, ContentTop = 60f;
        // Section headers ("착용 장비", "능력치", "보관함", "스킬") in one style, then the boxes under them.
        const float HeaderHeight = 24f, BoxTop = ContentTop + HeaderHeight;
        // 56 is the largest cell that fits the gear column (two squares of three cells plus the grid) inside 560.
        const float Cell = 56f, CellGap = 8f;
        const int Columns = 6, Rows = 3;
        const float InnerWidth = Columns * Cell + (Columns - 1) * CellGap;
        // The equipment slots are exactly storage cells (user 2026-11-23): the paper doll is three storage columns wide
        // and stands on the left three columns, the stats box on the right three, with the storage gap between them.
        const float Gap = CellGap, Square = 3f * Cell + 2f * CellGap, BoxPad = 12f;
        const float DollGap = CellGap, DollCell = Cell;
        const float StorageTop = BoxTop + Square + 10f, GridTop = StorageTop + HeaderHeight;
        const float GridHeight = Rows * Cell + (Rows - 1) * CellGap, CloseHeight = 36f, CloseWidth = 160f, CloseBottom = Pad;
        const float PanelWidth = 800f, PanelHeight = 560f, ColumnGap = 24f;
        // Skill column right of the gear column; its details box ends level with the storage grid.
        const float SkillLeft = Pad + InnerWidth + ColumnGap, SkillWidth = PanelWidth - Pad - SkillLeft;
        const float ContentBottom = PanelHeight - GridTop - GridHeight;
        // Tab-view leftovers (status line, two lists, message line) stay placed but hidden in this window.
        const float ListTop = ContentTop + 34f, BodyBottom = CloseBottom + CloseHeight + 10f, ListBottom = BodyBottom + 40f;
        const float HeroNameWidth = 150f, TooltipWidth = 210f;
        internal const string GearViewName = "GearView", SlotTemplateName = "SlotTemplate", TooltipName = "Tooltip";
        internal const string SkillTabName = "SkillTab", AutoPlateName = "AutoPlate";
        static readonly string[] Owned =
            { GearViewName, "Visual", "Tab_Gear", "Tab_Skill", "Hero_Previous", "Hero_Next", "HeroName", TooltipName, SkillTabName, AutoPlateName, "AutoSwitch", "Button_Auto" };
        // Skill column (user sketch 2026-11-23, scaled to the column): three round slots in a triangle (1 top, 2 bottom-left,
        // 3 bottom-right) with the opening grade on a badge at each slot's top-left and the clicked slot's details below.
        // The Auto plate sits at the top-right, on the title row.
        const float SkillCircle = 96f, SkillBadge = 30f, TriangleHalfWidth = 110f, TriangleHeight = 140f;
        const float TriangleTop = BoxTop + 8f, DetailTop = TriangleTop + SkillCircle + TriangleHeight + 18f;
        const float SkillCentreX = SkillLeft + SkillWidth * 0.5f - PanelWidth * 0.5f;
        // Square plate arrows as tall as the close button (user pick A, 2026-11-23); Auto is one plate that is green when on
        // and grey when off (user, same day: status-coloured plate instead of the two-segment switch).
        const float ArrowWidth = CloseHeight, NameGap = 8f, AutoWidth = 150f, AutoHeight = 30f;
        static readonly Color SkillDisc = new Color32(0x12, 0x2A, 0x5A, 0xFF), SkillLine = new Color32(0xB8, 0x92, 0x4A, 0x90);
        static readonly Color RestingRing = new Color(0.62f, 0.62f, 0.62f, 1f);
        static readonly Color Plate = new Color32(0x10, 0x16, 0x1A, 0xEE), SlotFill = new Color32(0x22, 0x26, 0x2A, 0xFF);
        static readonly Color BoxFill = new Color32(0x08, 0x0B, 0x0D, 0xC8);
        static readonly Color ThinFrame = new Color(0.85f, 0.85f, 0.85f, 1f), EmptyFrame = new Color(0.85f, 0.85f, 0.85f, 0.3f);

        [MenuItem("ProjectNeo/Setup/Equipment Window - Paper Doll (UI-07)")]
        public static void ApplyToActiveScene()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>() ?? throw new InvalidOperationException("Game HUD is required.");
            Apply(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
        }

        public static void Apply(GameFlowHud hud)
        {
            var panel = (RectTransform)hud.transform.Find("EquipmentPanel");
            var equipment = UnityEngine.Object.FindAnyObjectByType<GameEquipment>(FindObjectsInactive.Include);
            if (panel == null || equipment == null) return;
            AssignItemIcons();
            AssignSkillIcons();
            TMP_FontAsset noto = FW.Noto;
            foreach (string name in Owned)
            {
                Transform old = panel.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            // Screen-centered (user 2026-11-23); PrepHudSidebarLayout.Popups no longer moves this window.
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var hit = panel.GetComponent<Image>();
            hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; // still blocks clicks to the world
            RectTransform visual = FW.FreshVisual(panel);
            PrepHudSidebarLayout.GoldPlate(visual, Plate);

            var title = panel.Find("Title").GetComponent<TMP_Text>();
            FW.Font(title, noto, 26f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            title.text = "장비 · 스킬";
            title.raycastTarget = false;
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -TitleTop), new Vector2(300f, TitleHeight), new Vector2(0f, 1f));
            title.gameObject.SetActive(true);
            // Hero switcher on the bottom row, left of the close button (user 2026-11-23: the bottom row had room).
            Button previous = Arrow(panel, "Hero_Previous", "<", Pad, noto, equipment.PreviousHeroFromButton);
            Button next = Arrow(panel, "Hero_Next", ">", Pad + ArrowWidth + 2f * NameGap + HeroNameWidth, noto, equipment.NextHeroFromButton);
            var heroName = FW.NewLabel(panel, "HeroName", "영웅", noto, 18f, FW.Cream, TextAlignmentOptions.Center);
            heroName.overflowMode = TextOverflowModes.Ellipsis;
            FW.Place(heroName.rectTransform, Vector2.zero, new Vector2(Pad + ArrowWidth + NameGap, CloseBottom), new Vector2(HeroNameWidth, CloseHeight), Vector2.zero);

            var status = panel.Find("Status").GetComponent<TMP_Text>();
            FW.Font(status, noto, 16f, FW.Cream, TextAlignmentOptions.MidlineLeft);
            status.raycastTarget = false;
            Top(status.rectTransform, -ContentTop, 26f);
            var body = panel.Find("Body").GetComponent<TMP_Text>();
            FW.Font(body, noto, 15f, FW.Muted, TextAlignmentOptions.BottomLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.raycastTarget = false;
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = new Vector2(1f, 0f); bodyRect.pivot = new Vector2(0.5f, 0f);
            bodyRect.offsetMin = new Vector2(Pad, BodyBottom); bodyRect.offsetMax = new Vector2(-Pad, ListBottom - 4f);

            var close = (RectTransform)panel.Find("Button_Close");
            FW.PlateButton(close, "닫기", noto, 17f); // Esc and I close it too (user 2026-11-23: no key in the caption)
            // Same size and place as the organization window's buttons (UI-13).
            FW.Place(close, new Vector2(1f, 0f), new Vector2(-Pad, CloseBottom), new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));

            // Skill view: the two lists keep working, with gold headers (restyle later).
            var lists = (RectTransform)panel.Find("Lists");
            lists.anchorMin = Vector2.zero; lists.anchorMax = Vector2.one; lists.pivot = new Vector2(0.5f, 0.5f);
            lists.offsetMin = new Vector2(Pad, ListBottom); lists.offsetMax = new Vector2(-Pad, -ListTop);
            lists.localScale = Vector3.one;
            foreach (TMP_Text header in lists.GetComponentsInChildren<TMP_Text>(true))
                if (header.name == "Header") FW.Font(header, noto, 17f, FW.Gold, TextAlignmentOptions.TopLeft);

            var gear = FW.Node(panel, GearViewName);
            FW.Stretch(gear, Vector2.zero, Vector2.zero);
            gear.SetSiblingIndex(lists.GetSiblingIndex());

            // Top-left box: portrait on the top-left 2x2 cells, the five slots around it from the top-right corner down
            // and then left: helmet, armor, accessory, offhand, weapon (user 2026-11-23).
            Header(gear, "DollHeader", "착용 장비", noto, new Vector2(Pad, -ContentTop), Square);
            Header(gear, "StatsHeader", "능력치", noto, new Vector2(Pad + Square + Gap, -ContentTop), Square);
            RectTransform dollBox = Box(gear, "DollBox", new Vector2(Pad, -BoxTop), framed: false);
            var portrait = FW.Node(dollBox, "Portrait");
            float portraitSide = 2f * DollCell + DollGap;
            FW.Place(portrait, new Vector2(0f, 1f), Vector2.zero, new Vector2(portraitSide, portraitSide), new Vector2(0f, 1f));
            var portraitFill = FW.Image(portrait, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x12, 0x2A, 0x5A, 0xFF), 4f);
            FW.Stretch(portraitFill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var portraitFrame = FW.Image(portrait, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 14f);
            FW.Stretch(portraitFrame.rectTransform, Vector2.zero, Vector2.zero);

            var worn = FW.Node(dollBox, "WornSlots");
            FW.Stretch(worn, Vector2.zero, Vector2.zero);
            // Cell (column, row) of each slot kind, in EquipmentSlot value order: weapon, offhand, armor, accessory, helmet.
            var cells = new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 0) };
            var anchors = new RectTransform[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                anchors[i] = DollCellNode(worn, "Slot_" + (EquipmentSlot)i, cells[i]);

            // Top-right box: the five stats, with the hover preview change after each value.
            RectTransform statsBox = Box(gear, "StatsBox", new Vector2(Pad + Square + Gap, -BoxTop));
            var stats = FW.NewLabel(statsBox, "Stats", "최대 체력 <pos=62%>100", noto, 18f, FW.Cream, TextAlignmentOptions.TopLeft);
            stats.lineSpacing = 22f;
            FW.Stretch(stats.rectTransform, new Vector2(BoxPad + 4f, BoxPad), new Vector2(-BoxPad, -BoxPad - 4f));

            Header(gear, "StorageHeader", "보관함", noto, new Vector2(Pad, -StorageTop), InnerWidth);
            var storage = FW.Node(gear, "StorageGrid");
            FW.Place(storage, new Vector2(0f, 1f), new Vector2(Pad, -GridTop), new Vector2(InnerWidth, GridHeight), new Vector2(0f, 1f));
            var grid = storage.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Cell, Cell);
            grid.spacing = new Vector2(CellGap, CellGap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;

            EquipmentSlotView template = SlotTemplate(gear, noto);
            TMP_Text tooltipText = Tooltip(panel, noto, out RectTransform tooltip);

            equipment.ConfigurePaperDoll(gear.gameObject, lists.gameObject, template, worn, storage, stats, tooltipText,
                null, null, previous, next, heroName, Columns);
            BuildSkillTab(panel, equipment, noto, lists.GetSiblingIndex());
            equipment.ConfigureSideBySide(true);
            equipment.ConfigureSlotPlaceholders(SlotPlaceholders());
            equipment.ConfigureWornAnchors(anchors);
            equipment.ConfigureTooltip(tooltip);
            var data = new SerializedObject(equipment);
            data.FindProperty("minStorageCells").intValue = Columns * Rows;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(equipment);
        }

        static void Header(RectTransform parent, string name, string text, TMP_FontAsset noto, Vector2 topLeft, float width)
        {
            var label = FW.NewLabel(parent, name, text, noto, 17f, FW.Gold, TextAlignmentOptions.TopLeft);
            FW.Place(label.rectTransform, new Vector2(0f, 1f), topLeft, new Vector2(width, 24f), new Vector2(0f, 1f));
        }

        // Square box hung from the gear view's top-left. The stats box gets a dark fill and the thin gold frame; the paper
        // doll box is bare (user 2026-11-23: no frame around the portrait and the slots).
        static RectTransform Box(RectTransform parent, string name, Vector2 topLeft, bool framed = true)
        {
            var box = FW.Node(parent, name);
            FW.Place(box, new Vector2(0f, 1f), topLeft, new Vector2(Square, Square), new Vector2(0f, 1f));
            if (!framed) return box;
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", BoxFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 0.6f), 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            return box;
        }

        // Tooltip beside the hovered cell: grows with its lines (one effect per line), never takes the mouse.
        static TMP_Text Tooltip(RectTransform panel, TMP_FontAsset noto, out RectTransform box)
        {
            box = FW.Node(panel, TooltipName);
            box.sizeDelta = new Vector2(TooltipWidth, 40f);
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0x06, 0x08, 0x0A, 0xFF), 4f);
            fill.type = Image.Type.Simple; // the sliced box leaves its middle see-through
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", FW.Gold, 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 9, 10);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var text = FW.NewLabel(box, "Text", "", noto, 16f, Color.white, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 4f;
            foreach (var graphic in box.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            box.gameObject.SetActive(false);
            return text;
        }

        static RectTransform DollCellNode(RectTransform parent, string name, Vector2Int cell)
        {
            var node = FW.Node(parent, name);
            FW.Place(node, new Vector2(0f, 1f), new Vector2(cell.x * (DollCell + DollGap), -cell.y * (DollCell + DollGap)),
                new Vector2(DollCell, DollCell), new Vector2(0f, 1f));
            return node;
        }

        static void Top(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Pad, y - height); rect.offsetMax = new Vector2(-Pad, y);
        }

        // Square plate button (same plate as 닫기) with the pack's gold arrow, on the bottom row, x from the panel's left edge.
        static Button Arrow(RectTransform panel, string name, string caption, float x, TMP_FontAsset noto, UnityEngine.Events.UnityAction action)
        {
            Button button = GameFacilitySetup.ButtonAt(panel, name, caption, Vector2.zero, Vector2.zero, new Vector2(ArrowWidth, CloseHeight), action, out _);
            var rect = (RectTransform)button.transform;
            FW.PlateButton(rect, string.Empty, noto, 17f);
            var icon = FW.Image(rect, "ICON", "SPR_HUD_FantasyWarrior_Arrow02", FW.Gold, 1f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            FW.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, caption == "<" ? 180f : 0f);
            FW.Place(rect, Vector2.zero, new Vector2(x, CloseBottom), new Vector2(ArrowWidth, CloseHeight), Vector2.zero);
            return button;
        }

        // Skill tab: triangle of round slots, lines between their centres, the details box, and the Auto button.
        static void BuildSkillTab(RectTransform panel, GameEquipment equipment, TMP_FontAsset noto, int siblingIndex)
        {
            var tab = FW.Node(panel, SkillTabName);
            FW.Stretch(tab, Vector2.zero, Vector2.zero);
            tab.SetSiblingIndex(siblingIndex);

            Header(tab, "SkillHeader", "스킬", noto, new Vector2(SkillLeft, -ContentTop), SkillWidth);
            float topY = -(TriangleTop + SkillCircle * 0.5f), bottomY = topY - TriangleHeight;
            var centres = new[]
            {
                new Vector2(SkillCentreX, topY), new Vector2(SkillCentreX - TriangleHalfWidth, bottomY),
                new Vector2(SkillCentreX + TriangleHalfWidth, bottomY),
            };
            for (int i = 0; i < centres.Length; i++) Line(tab, "Line" + i, centres[i], centres[(i + 1) % centres.Length]);
            var circles = new SkillCircleView[centres.Length];
            for (int i = 0; i < centres.Length; i++) circles[i] = Circle(tab, "Skill" + (i + 1), centres[i], noto);

            var box = FW.Node(tab, "DetailBox");
            box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one; box.pivot = new Vector2(0.5f, 0.5f);
            box.offsetMin = new Vector2(SkillLeft, ContentBottom); box.offsetMax = new Vector2(-Pad, -DetailTop);
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", BoxFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", new Color(0.85f, 0.85f, 0.85f, 0.6f), 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            var detail = FW.NewLabel(box, "Detail", "", noto, 16f, FW.Cream, TextAlignmentOptions.TopLeft);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.richText = true;
            detail.lineSpacing = 4f;
            detail.raycastTarget = false;
            FW.Stretch(detail.rectTransform, new Vector2(BoxPad + 4f, BoxPad), new Vector2(-BoxPad - 4f, -BoxPad - 2f));

            // Auto plate at the top-right corner, centred on the title row and Pad from the right. GameEquipment paints the
            // plate and caption by state.
            Button auto = GameFacilitySetup.ButtonAt(panel, AutoPlateName, "자동 사용 켜짐", new Vector2(1f, 1f), Vector2.zero,
                new Vector2(AutoWidth, AutoHeight), equipment.ToggleHeroAutoFromButton, out _);
            var autoRect = (RectTransform)auto.transform;
            FW.PlateButton(autoRect, "자동 사용 켜짐", noto, 15f);
            FW.Place(autoRect, new Vector2(1f, 1f), new Vector2(-Pad, -(TitleTop + (TitleHeight - AutoHeight) * 0.5f)),
                new Vector2(AutoWidth, AutoHeight), new Vector2(1f, 1f));
            auto.gameObject.SetActive(false);

            tab.gameObject.SetActive(false);
            equipment.ConfigureSkillTab(tab.gameObject, circles, detail, auto);
        }

        static void Line(RectTransform parent, string name, Vector2 from, Vector2 to)
        {
            var line = FW.Node(parent, name).gameObject.AddComponent<Image>();
            line.color = SkillLine;
            line.raycastTarget = false;
            var rect = line.rectTransform;
            Vector2 delta = to - from;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, 3f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // One round slot: dark-blue disc in the thick gold ring (same art as the HP portrait), icon or name letters, a gold
        // selection ring, and the grade badge on the top-left. The disc, icon and ring dim together when locked.
        static SkillCircleView Circle(RectTransform parent, string name, Vector2 centre, TMP_FontAsset noto)
        {
            var root = FW.Node(parent, name);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = centre;
            root.sizeDelta = new Vector2(SkillCircle, SkillCircle);
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;

            var body = FW.Node(root, "Body");
            FW.Stretch(body, Vector2.zero, Vector2.zero);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Round(body, "SPR_Background", "SPR_HUD_FantasyWarrior_Circle_Medium01_Clean", SkillDisc, 8f);
            var icon = FW.Node(body, "Icon").gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            FW.Stretch(icon.rectTransform, new Vector2(20f, 20f), new Vector2(-20f, -20f));
            var letters = FW.NewLabel(body, "Letters", "", noto, 22f, FW.Cream, TextAlignmentOptions.Center);
            letters.raycastTarget = false;
            FW.Stretch(letters.rectTransform, Vector2.zero, Vector2.zero);
            // Unselected rings are dimmed so the selected one (bright gold, larger) stands out.
            Round(body, "SPR_Frame", "SPR_FantasyWarrior_Ring_Large06", RestingRing, 0f);

            var selection = FW.Node(root, "Selected");
            FW.Stretch(selection, new Vector2(-10f, -10f), new Vector2(10f, 10f));
            Round(selection, "SPR_Ring", "SPR_FantasyWarrior_Ring_Large06", new Color(1f, 0.95f, 0.75f, 1f), 0f);
            selection.gameObject.SetActive(false);

            var badge = FW.Node(root, "GradeBadge");
            FW.Place(badge, new Vector2(0f, 1f), new Vector2(2f, -2f), new Vector2(SkillBadge, SkillBadge), new Vector2(0f, 1f));
            Round(badge, "SPR_Background", "SPR_HUD_FantasyWarrior_Circle_Medium01_Clean", new Color32(0x1C, 0x1A, 0x17, 0xFF), 3f);
            Round(badge, "SPR_Frame", "SPR_FantasyWarrior_Ring_Large06", Color.white, 0f);
            var grade = FW.NewLabel(badge, "Grade", "1", FW.Grenze, 16f, FW.Gold, TextAlignmentOptions.Center);
            grade.raycastTarget = false;
            FW.Stretch(grade.rectTransform, Vector2.zero, new Vector2(0f, 1f));

            var view = root.gameObject.AddComponent<SkillCircleView>();
            view.Configure(button, icon, letters, grade, group, selection.gameObject);
            return view;
        }

        static void Round(RectTransform parent, string name, string sprite, Color color, float inset)
        {
            var image = FW.Image(parent, name, sprite, color, 1f);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            FW.Stretch(image.rectTransform, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }

        // One square cell: dark fill, thin gold frame (dim when empty), icon or name letters, upgrade level, hover outline.
        static EquipmentSlotView SlotTemplate(RectTransform parent, TMP_FontAsset noto)
        {
            var root = FW.Node(parent, SlotTemplateName);
            root.sizeDelta = new Vector2(Cell, Cell);
            var size = root.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = Cell;
            size.minHeight = size.preferredHeight = Cell;
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;

            var fill = FW.Image(root, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", SlotFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var filled = FW.Image(root, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 20f);
            FW.Stretch(filled.rectTransform, Vector2.zero, Vector2.zero);
            var empty = FW.Image(root, "SPR_FrameEmpty", "SPR_FantasyWarrior_Frame_Box22_Variant01", EmptyFrame, 20f);
            FW.Stretch(empty.rectTransform, Vector2.zero, Vector2.zero);
            // Empty equipment slot: the slot kind's pack icon, dimmed (UI-07, user 2026-11-23).
            var placeholder = FW.Node(root, "Placeholder").gameObject.AddComponent<Image>();
            placeholder.preserveAspect = true;
            placeholder.raycastTarget = false;
            placeholder.color = PlaceholderTint;
            placeholder.enabled = false;
            FW.Stretch(placeholder.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var icon = FW.Node(root, "Icon").gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            FW.Stretch(icon.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            var letters = FW.NewLabel(root, "Letters", "", noto, 15f, FW.Cream, TextAlignmentOptions.Center);
            FW.Stretch(letters.rectTransform, Vector2.zero, Vector2.zero);
            var level = FW.NewLabel(root, "Level", "", noto, 12f, FW.Gold, TextAlignmentOptions.BottomRight);
            FW.Stretch(level.rectTransform, new Vector2(4f, 3f), new Vector2(-5f, -3f));
            var highlight = FW.Image(root, "SPR_Highlight", "SPR_FantasyWarrior_Frame_Box22_Variant01", FW.Gold, 14f);
            FW.Stretch(highlight.rectTransform, new Vector2(-3f, -3f), new Vector2(3f, 3f));
            highlight.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<EquipmentSlotView>();
            view.Configure(button, icon, letters, level, filled.gameObject, empty.gameObject, highlight.gameObject);
            view.ConfigurePlaceholder(placeholder);
            root.gameObject.SetActive(false);
            return view;
        }

        static readonly Color PlaceholderTint = new Color(1f, 1f, 1f, 0.28f);

        // Pack category icons for empty slots, in EquipmentSlot value order (weapon, offhand, armor, accessory, helmet).
        static Sprite[] SlotPlaceholders() => new[]
        {
            FW.Sprite("ICON_FantasyWarrior_Inventory_Swords01_Clean"),
            FW.Sprite("ICON_FantasyWarrior_Inventory_Shields01_Clean"),
            FW.Sprite("ICON_FantasyWarrior_Inventory_Armor01_Clean"),
            FW.Sprite("ICON_FantasyWarrior_Inventory_Rings01_Clean"),
            FW.Sprite("ICON_FantasyWarrior_Inventory_Helmets01_Clean"),
        };

        // Prototype gear slots (user 2026-11-23: weapon, offhand, armor, accessory; the wooden shield is an offhand).
        static readonly Dictionary<string, EquipmentSlot> Slots = new Dictionary<string, EquipmentSlot>
        {
            { "item_iron_sword", EquipmentSlot.Weapon },
            { "item_steel_sword", EquipmentSlot.Weapon },
            { "item_wood_shield", EquipmentSlot.Offhand },
            { "item_leather_armor", EquipmentSlot.Armor },
            { "item_chain_armor", EquipmentSlot.Armor },
            // CONTENT-03 helmets and accessories.
            { HelmetAccessorySetup.LeatherCap, EquipmentSlot.Helmet },
            { HelmetAccessorySetup.IronHelm, EquipmentSlot.Helmet },
            { HelmetAccessorySetup.TravelerCharm, EquipmentSlot.Accessory },
            { HelmetAccessorySetup.IronRing, EquipmentSlot.Accessory },
            { HelmetAccessorySetup.MarksmanClasp, EquipmentSlot.Accessory },
            { HelmetAccessorySetup.WarBannerCharm, EquipmentSlot.Accessory },
        };

        // Prototype gear icons from the team's packs (user pick of option B, 2026-11-23). Only fills an empty icon, so an
        // icon chosen in the inspector stays.
        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            { "item_iron_sword", "ICON_SM_Wep_Sword_01" },
            { "item_steel_sword", "ICON_SM_Wep_Sword_02" },
            { "item_wood_shield", "ICON_SM_Wep_Shield_01" },
            { "item_leather_armor", "ICON_SM_PickUp_Armor_01" },
            { "item_chain_armor", "ICON_SM_PickUp_Armor_02" },
            { "item_potion", "ICON_FantasyWarrior_Inventory_Potions01_Clean" },
            // CONTENT-03 placeholder icons until helmet/accessory art lands.
            { HelmetAccessorySetup.LeatherCap, "ICON_FantasyWarrior_Inventory_Helmets01_Clean" },
            { HelmetAccessorySetup.IronHelm, "ICON_SM_Char_Attach_Helmet_01" },
            { HelmetAccessorySetup.TravelerCharm, "ICON_FantasyWarrior_Inventory_Necklaces01_Clean" },
            { HelmetAccessorySetup.IronRing, "ICON_FantasyWarrior_Inventory_Rings01_Clean" },
            { HelmetAccessorySetup.MarksmanClasp, "ICON_FantasyWarrior_Inventory_Arrows01_Clean" },
            { HelmetAccessorySetup.WarBannerCharm, "ICON_FantasyWarrior_Map_Flag01_Clean" },
        };

        // Sample skill icons from the team's packs (UI-07, 2026-11-23). Skills without one show their name's first letters.
        // Only fills an empty icon, so an icon chosen in the inspector stays.
        static readonly Dictionary<string, string> SkillIcons = new Dictionary<string, string>
        {
            { "skill_whirlwind", "ICON_SM_Wep_Sword_01" },
            { "skill_shield_bash", "ICON_SM_Wep_Shield_01" },
            { "skill_healing_light", "ICON_FantasyWarrior_Inventory_Potions01_Clean" },
        };

        public static void AssignSkillIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<ProjectNeo.Skills.SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || skill.Icon != null || !SkillIcons.TryGetValue(skill.Id ?? string.Empty, out string spriteName)) continue;
                Sprite sprite = PackSprite(spriteName);
                if (sprite == null) { Debug.LogWarning("Skill icon sprite is missing: " + spriteName); continue; }
                var data = new SerializedObject(skill);
                data.FindProperty("icon").objectReferenceValue = sprite;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skill);
            }
            AssetDatabase.SaveAssets();
        }

        public static void AssignItemIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                if (Slots.TryGetValue(item.Id ?? string.Empty, out EquipmentSlot kind) && item.Slot != kind)
                {
                    var slotData = new SerializedObject(item);
                    slotData.FindProperty("slot").enumValueIndex = (int)kind;
                    slotData.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
                if (item.Icon != null || !Icons.TryGetValue(item.Id ?? string.Empty, out string spriteName)) continue;
                Sprite sprite = PackSprite(spriteName);
                if (sprite == null) { Debug.LogWarning("Item icon sprite is missing: " + spriteName); continue; }
                var data = new SerializedObject(item);
                data.FindProperty("icon").objectReferenceValue = sprite;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();
        }

        static Sprite PackSprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/ThirdParty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }
    }
}
