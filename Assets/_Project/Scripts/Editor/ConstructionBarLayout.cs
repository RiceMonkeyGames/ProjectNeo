using System;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FW = ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-13 construction mode HUD, option B of three (user pick 2026-11-24), Game scene only. Turns the left
    /// <c>BuildPanel</c> test column into a bottom-center gold bar: category tabs (walls / siege weapons) on its top edge,
    /// piece cards in a horizontal scroll (<see cref="FacilityTileView"/> clones, name and gold cost) and the tools
    /// (dismantle, repair all, battlements) at its right end. Above the bar a strip shows the selected piece and its
    /// placement state (<see cref="ConstructionHud"/> selection label); the economy line (repair cost, dismantling, work
    /// queue) is no longer shown: repair cost sits on the repair button and the last result on the strip's right; the shared key guide (KeyListHud) shows the build keys.
    /// The hidden test buttons and the rotate / cancel buttons (keys R and right click) leave the screen.
    /// Running it again gives the same result. Other setup menus that add buttons to <c>BuildPanel</c> (bastion,
    /// siege weapon, build economy, battlements) must be followed by this menu again.
    /// </summary>
    public static class ConstructionBarLayout
    {
        const float BarWidth = 780f, BarHeight = 112f, BarBottom = 16f, Inset = 10f, ToolsWidth = 190f, ToolHeight = 28f, ToolGap = 4f;
        const float CardWidth = 84f, CardHeight = 92f, CardThumb = 44f, CardGap = 8f;
        const float TabWidth = 120f, TabHeight = 28f, StripHeight = 70f, Edge = 16f;
        static readonly Color PlateColor = new Color32(0x10, 0x16, 0x1A, 0xEE), BoxFill = new Color32(0x08, 0x0B, 0x0D, 0xD8);
        static readonly Color ThinFrame = new Color(0.85f, 0.85f, 0.85f, 0.6f);

        [MenuItem("ProjectNeo/Setup/Construction Bar (UI-13)")]
        public static void ApplyToActiveScene()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<ConstructionHud>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Construction HUD is required.");
            Apply(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
        }

        public static void Apply(ConstructionHud hud)
        {
            var economy = UnityEngine.Object.FindAnyObjectByType<GameBuildEconomy>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Run Connect Build Costs To Game Scene first.");
            var battlements = UnityEngine.Object.FindAnyObjectByType<GameBattlements>(FindObjectsInactive.Include);
            TMP_FontAsset noto = FW.Noto;
            Transform root = hud.transform;
            foreach (string old in new[] { "BuildTabs", "BuildStatus", "BuildEconomy", "BuildKeys", "BuildUnused" })
            {
                Transform found = root.Find(old);
                if (found != null) UnityEngine.Object.DestroyImmediate(found.gameObject);
            }

            // The bar keeps the BuildPanel name and object (tests and other menus find it by name).
            var bar = (RectTransform)root.Find("BuildPanel") ?? throw new InvalidOperationException("BuildPanel is missing.");
            for (int i = bar.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(bar.GetChild(i).gameObject);
            FW.DestroyComponent<VerticalLayoutGroup>(bar.gameObject);
            FW.DestroyComponent<ContentSizeFitter>(bar.gameObject);
            FW.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, BarBottom), new Vector2(BarWidth, BarHeight), new Vector2(0.5f, 0f));
            var hit = bar.GetComponent<Image>();
            hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; // blocks world clicks and wheel zoom over the bar
            RectTransform visual = FW.FreshVisual(bar);
            PrepHudSidebarLayout.GoldPlate(visual, PlateColor);

            // Cards: horizontal scroll left of the tools.
            var scroll = FW.Node(bar, "CardScroll");
            FW.Stretch(scroll, new Vector2(Inset, Inset - 2f), new Vector2(-Inset - ToolsWidth - 12f, -Inset + 2f));
            var viewport = FW.Node(scroll, "Viewport");
            FW.Stretch(viewport, new Vector2(0f, 6f), Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = FW.Node(viewport, "Content");
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = CardGap; row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var track = FW.Node(scroll, "Scrollbar");
            track.anchorMin = Vector2.zero; track.anchorMax = new Vector2(1f, 0f); track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = Vector2.zero; track.offsetMax = new Vector2(0f, 4f);
            track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var area = FW.Node(track, "SlidingArea");
            FW.Stretch(area, Vector2.zero, Vector2.zero);
            var handle = FW.Node(area, "Handle");
            FW.Stretch(handle, Vector2.zero, Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = FW.Gold;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            var view = scroll.gameObject.AddComponent<ScrollRect>();
            view.viewport = viewport; view.content = content;
            view.horizontal = true; view.vertical = false; // the wheel scrolls sideways over the bar
            view.movementType = ScrollRect.MovementType.Clamped; view.scrollSensitivity = 30f;
            view.horizontalScrollbar = scrollbar;
            view.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Tools at the right end.
            Button dismantle = Tool(bar, "Button_Dismantle", "해체", 0, noto, economy.ToggleDismantle, out _);
            Tool(bar, "Button_Repair all", "전체 수리", 1, noto, economy.RepairAllFromButton, out TMP_Text repairLabel);
            TMP_Text battlementsLabel = null;
            Button battlementsButton = battlements != null
                ? Tool(bar, "Button_Battlements", "흉벽", 2, noto, battlements.TryUpgradeFromButton, out battlementsLabel) : null;
            if (battlements != null)
            {
                var facilities = (GameFacilities)new SerializedObject(battlements).FindProperty("facilities").objectReferenceValue;
                battlements.ConfigureUpgrade(facilities, battlementsButton, battlementsLabel);
                EditorUtility.SetDirty(battlements);
            }

            FacilityTileView card = FacilityWindowLayout.TileTemplate(bar, noto, "CardTemplate", CardWidth, CardHeight, CardThumb);

            // Category tabs sitting on the bar's top edge, left aligned with the cards.
            var tabs = FW.Node(root, "BuildTabs");
            FW.Place(tabs, new Vector2(0.5f, 0f), new Vector2(-BarWidth * 0.5f + Inset, BarBottom + BarHeight + 2f),
                new Vector2(2f * TabWidth + 6f, TabHeight), new Vector2(0f, 0f));
            Button walls = Plate(tabs, "Tab_Walls", "성벽", noto, 15f, out _);
            FW.Place((RectTransform)walls.transform, Vector2.zero, Vector2.zero, new Vector2(TabWidth, TabHeight), Vector2.zero);
            Button weapons = Plate(tabs, "Tab_Weapons", "수성 무기", noto, 15f, out _);
            FW.Place((RectTransform)weapons.transform, Vector2.zero, new Vector2(TabWidth + 6f, 0f), new Vector2(TabWidth, TabHeight), Vector2.zero);

            // Selected piece and placement state, above the bar right of the tabs.
            float stripLeft = 2f * TabWidth + 6f + Inset + 10f;
            var strip = Chip(root, "BuildStatus", new Vector2(0.5f, 0f), new Vector2(-BarWidth * 0.5f + stripLeft, BarBottom + BarHeight + 6f),
                new Vector2(BarWidth - stripLeft, StripHeight), new Vector2(0f, 0f));
            // Text is vertically centered in the strip (user 2026-11-24: it stuck to the bottom). Left: selection and state;
            // right: the last build economy result (refund, not enough gold, dismantle hint).
            TMP_Text selection = FW.NewLabel(strip, "Selection", "지을 조각을 고르세요", noto, 15f, FW.Cream, TextAlignmentOptions.MidlineLeft);
            selection.textWrappingMode = TextWrappingModes.Normal;
            selection.rectTransform.anchorMin = Vector2.zero; selection.rectTransform.anchorMax = new Vector2(0.58f, 1f);
            selection.rectTransform.offsetMin = new Vector2(12f, 4f); selection.rectTransform.offsetMax = new Vector2(-6f, -4f);
            TMP_Text notice = FW.NewLabel(strip, "Notice", "", noto, 14f, FW.Parchment, TextAlignmentOptions.MidlineRight);
            notice.textWrappingMode = TextWrappingModes.Normal;
            notice.rectTransform.anchorMin = new Vector2(0.58f, 0f); notice.rectTransform.anchorMax = Vector2.one;
            notice.rectTransform.offsetMin = new Vector2(6f, 4f); notice.rectTransform.offsetMax = new Vector2(-12f, -4f);

            // The build economy still writes its old multi-line status; the screen no longer shows it (user 2026-11-24: the
            // top-left status panel is not needed). Repair cost is on the repair button, results in the strip.
            var unused = FW.Node(root, "BuildUnused");
            TMP_Text economyLabel = FW.NewLabel(unused, "EconomyStatus", "", noto, 14f, FW.Cream, TextAlignmentOptions.TopLeft);
            var economyData = new SerializedObject(economy);
            economyData.FindProperty("label").objectReferenceValue = economyLabel;
            economyData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economy);

            // Keys: the shared bottom-right key guide (KeyListHud, UI-12 key caps) lists the build keys, camera keys included,
            // so the old top-right camera hint line goes away (user 2026-11-24: use the original key guide style).
            Transform cameraHint = root.Find("ViewHint");
            if (cameraHint != null) cameraHint.gameObject.SetActive(false);

            // Labels the construction HUD still writes but the game screen does not show (sandbox message, test unit HP).
            TMP_Text status = FW.NewLabel(unused, "Status", "", noto, 14f, FW.Cream, TextAlignmentOptions.TopLeft);
            TMP_Text health = FW.NewLabel(unused, "UnitHealth", "", noto, 14f, FW.Cream, TextAlignmentOptions.TopLeft);
            unused.gameObject.SetActive(false);
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("selection").objectReferenceValue = selection;
            hudData.FindProperty("status").objectReferenceValue = status;
            hudData.FindProperty("unitHealth").objectReferenceValue = health;
            hudData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            var barHud = hud.GetComponent<ConstructionBarHud>() ?? hud.gameObject.AddComponent<ConstructionBarHud>();
            barHud.Configure(hud, economy, walls, weapons, view, content, card, dismantle, repairLabel, notice);
            EditorUtility.SetDirty(barHud);
        }

        static Button Plate(RectTransform parent, string name, string caption, TMP_FontAsset noto, float size, out TMP_Text label)
        {
            Button button = GameFacilitySetup.ButtonAt(parent, name, caption, Vector2.zero, Vector2.zero, new Vector2(100f, 30f), null, out _);
            label = FW.PlateButton((RectTransform)button.transform, caption, noto, size);
            label.raycastTarget = false;
            return button;
        }

        static Button Tool(RectTransform bar, string name, string caption, int index, TMP_FontAsset noto,
            UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            Button button = Plate(bar, name, caption, noto, 14f, out label);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            FW.Place((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(-Inset, -Inset - index * (ToolHeight + ToolGap)),
                new Vector2(ToolsWidth, ToolHeight), new Vector2(1f, 1f));
            return button;
        }

        // Dark plate with the thin frame.
        static RectTransform Chip(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var chip = FW.Node(parent, name);
            FW.Place(chip, anchor, position, size, pivot);
            var fill = FW.Image(chip, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", BoxFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var frame = FW.Image(chip, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            return chip;
        }
    }
}
