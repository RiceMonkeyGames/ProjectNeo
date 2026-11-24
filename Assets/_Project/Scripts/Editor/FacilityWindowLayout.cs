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
    /// UI-13 facility popup, option C of three (user pick 2026-11-24): the window opened with F at a courtyard building
    /// (tavern, shop, training ground, smithy, hero hall) in the look of the I / O / K windows. 800x560 centered plate in the
    /// thick gold Box22 frame; title row (facility name, level and wallet, upgrade plate button at the top right); left
    /// column with a tile grid (shop and smithy switch offers and storage with two tab plates, option C2); right column with
    /// the selected tile's details, a notice line and two action plates; reroll bottom-left, close bottom-right.
    /// Builds the inside of <c>FacilityPanel</c> (keeps the object and its sibling place) and connects
    /// <see cref="FacilityWindowView"/> to <see cref="GameFacilities"/>. Running it again gives the same result.
    /// </summary>
    public static class FacilityWindowLayout
    {
        internal const string PanelName = "FacilityPanel";
        const float PanelWidth = 800f, PanelHeight = 560f, Pad = 24f, TitleTop = 14f, TitleHeight = 36f, ColumnsTop = 60f;
        const float CloseHeight = 36f, CloseWidth = 160f, ColumnsBottom = Pad + CloseHeight + 12f, ColumnGap = 12f;
        const float LeftWidth = 440f, RightWidth = PanelWidth - 2f * Pad - LeftWidth - ColumnGap;
        const float HeaderTop = 8f, HeaderHeight = 28f, ListInset = 8f, ListTop = HeaderTop + HeaderHeight + 8f;
        const float TabWidth = 130f, TabHeight = 28f, UpgradeWidth = 250f, UpgradeHeight = 30f;
        // Four 96 wide tiles fill the 440 column (minus insets and the thin scrollbar).
        const float TileWidth = 96f, TileHeight = 104f, TileGap = 8f, Thumb = 52f;
        // Noto needs about 1.45x the font size per line; text boxes with ellipsis that are shorter show nothing at all.
        const float DetailThumb = 64f, NoticeHeight = 50f, StatRow = 21f, ButtonGap = 12f;
        static readonly Color PlateColor = new Color32(0x10, 0x16, 0x1A, 0xEE), BoxFill = new Color32(0x08, 0x0B, 0x0D, 0xC8);
        static readonly Color TileFill = new Color32(0x22, 0x26, 0x2A, 0xFF), TileHover = new Color32(0x3B, 0x33, 0x22, 0xFF);
        static readonly Color ThumbFill = new Color32(0x00, 0x13, 0x35, 0xFF), ThinFrame = new Color(0.85f, 0.85f, 0.85f, 0.6f);

        [MenuItem("ProjectNeo/Setup/Facility Window - Tiles And Detail (UI-13)")]
        public static void ApplyToActiveScene()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>() ?? throw new InvalidOperationException("Game HUD is required.");
            var facilities = UnityEngine.Object.FindAnyObjectByType<GameFacilities>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Run Connect Facilities To Game Scene first.");
            var data = new SerializedObject(facilities);
            var start = (ProjectNeo.Run.RunConfig)data.FindProperty("startConfig").objectReferenceValue;
            var view = (GamePhaseView)data.FindProperty("phaseView").objectReferenceValue;
            Apply(hud, facilities, start, view);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
        }

        public static void Apply(GameFlowHud hud, GameFacilities facilities, ProjectNeo.Run.RunConfig start, GamePhaseView phaseView)
        {
            TMP_FontAsset noto = FW.Noto;
            Transform existing = hud.transform.Find(PanelName);
            RectTransform panel;
            if (existing != null)
            {
                panel = (RectTransform)existing;
                for (int i = panel.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);
                foreach (MonoBehaviour behaviour in panel.GetComponents<MonoBehaviour>())
                    if (!(behaviour is Graphic)) UnityEngine.Object.DestroyImmediate(behaviour);
            }
            else
            {
                panel = FW.Node(hud.transform, PanelName);
                panel.gameObject.AddComponent<Image>();
            }
            panel.gameObject.SetActive(true);
            // Screen-centered like the equipment and organization windows (UI-13).
            FW.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            var hit = panel.GetComponent<Image>();
            hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; // still blocks clicks to the world
            RectTransform visual = FW.FreshVisual(panel);
            PrepHudSidebarLayout.GoldPlate(visual, PlateColor);
            var window = panel.gameObject.AddComponent<FacilityWindowView>();

            // Title row: name on the left, level and wallet right-aligned next to the upgrade plate.
            window.title = FW.NewLabel(panel, "Title", "시설", noto, 26f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            FW.Place(window.title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -TitleTop), new Vector2(220f, TitleHeight), new Vector2(0f, 1f));
            window.status = FW.NewLabel(panel, "Status", "레벨 1 / 3 · 골드 0", noto, 16f, FW.Cream, TextAlignmentOptions.MidlineRight);
            window.status.overflowMode = TextOverflowModes.Ellipsis;
            FW.Place(window.status.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad - UpgradeWidth - 12f, -TitleTop),
                new Vector2(PanelWidth - 2f * Pad - UpgradeWidth - 12f - 230f, TitleHeight), new Vector2(1f, 1f));
            window.upgradeButton = Plate(panel, "Button_Upgrade", "업그레이드", noto, 15f, out window.upgradeLabel);
            FW.Place((RectTransform)window.upgradeButton.transform, new Vector2(1f, 1f),
                new Vector2(-Pad, -(TitleTop + (TitleHeight - UpgradeHeight) * 0.5f)), new Vector2(UpgradeWidth, UpgradeHeight), new Vector2(1f, 1f));

            // Left column: caption and count (or two tabs) over two scrolling tile grids.
            RectTransform left = Box(panel, "OffersColumn", Pad, LeftWidth);
            window.header = FW.NewLabel(left, "Header", "매물", noto, 17f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            TopRow(window.header.rectTransform, 0.62f, true);
            window.count = FW.NewLabel(left, "Count", "0", noto, 15f, FW.Cream, TextAlignmentOptions.MidlineRight);
            TopRow(window.count.rectTransform, 0.38f, false);
            window.offersTab = Plate(left, "Tab_Offers", "매물", noto, 15f, out window.offersTabLabel);
            FW.Place((RectTransform)window.offersTab.transform, new Vector2(0f, 1f), new Vector2(ListInset, -8f), new Vector2(TabWidth, TabHeight), new Vector2(0f, 1f));
            window.storageTab = Plate(left, "Tab_Storage", "보관함", noto, 15f, out window.storageTabLabel);
            FW.Place((RectTransform)window.storageTab.transform, new Vector2(0f, 1f), new Vector2(ListInset + TabWidth + 6f, -8f), new Vector2(TabWidth, TabHeight), new Vector2(0f, 1f));
            window.offerGrid = Grid(left, "OfferScroll", out window.offerScroll);
            window.storageGrid = Grid(left, "StorageScroll", out window.storageScroll);
            window.emptyLabel = FW.NewLabel(left, "Empty", "", noto, 16f, FW.Muted, TextAlignmentOptions.Center);
            window.emptyLabel.textWrappingMode = TextWrappingModes.Normal;
            FW.Stretch(window.emptyLabel.rectTransform, new Vector2(24f, 24f), new Vector2(-24f, -ListTop));

            // Right column: selected tile's name and price, thumbnail with HP / attack / defense icons, text, notice.
            RectTransform right = Box(panel, "DetailColumn", Pad + LeftWidth + ColumnGap, RightWidth);
            window.detailTitle = FW.NewLabel(right, "DetailTitle", "", noto, 17f, FW.Gold, TextAlignmentOptions.MidlineLeft);
            window.detailTitle.overflowMode = TextOverflowModes.Ellipsis;
            TopRow(window.detailTitle.rectTransform, 0.6f, true);
            window.detailPrice = FW.NewLabel(right, "DetailPrice", "", noto, 14f, FW.Cream, TextAlignmentOptions.MidlineRight);
            window.detailPrice.overflowMode = TextOverflowModes.Ellipsis;
            TopRow(window.detailPrice.rectTransform, 0.4f, false);
            var content = FW.Node(right, "DetailContent");
            FW.Stretch(content, new Vector2(12f, 12f + NoticeHeight + 6f), new Vector2(-12f, -ListTop));
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 10f;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            var thumbRow = FW.Node(content, "Thumbnail");
            var thumbRowSize = thumbRow.gameObject.AddComponent<LayoutElement>();
            thumbRowSize.preferredHeight = thumbRowSize.minHeight = DetailThumb;
            var thumb = FW.Node(thumbRow, "Box");
            FW.Place(thumb, new Vector2(0f, 1f), Vector2.zero, new Vector2(DetailThumb, DetailThumb), new Vector2(0f, 1f));
            window.detailIcon = Thumbnail(thumb, 6f);
            window.detailThumbnail = thumbRow.gameObject;
            var stats = FW.Node(thumbRow, "Stats");
            FW.Place(stats, new Vector2(0f, 1f), new Vector2(DetailThumb + 14f, 0f), new Vector2(RightWidth - 24f - DetailThumb - 14f, DetailThumb), new Vector2(0f, 1f));
            window.statsRoot = stats.gameObject;
            string[] statIcons = { "ICON_FantasyWarrior_Status_Health01_Clean", "ICON_FantasyWarrior_Status_Attack01_Clean", "ICON_FantasyWarrior_Status_Defense01_Clean" };
            window.statValues = new TMP_Text[statIcons.Length];
            for (int i = 0; i < statIcons.Length; i++)
            {
                var icon = FW.Image(stats, "Icon" + i, statIcons[i], Color.white, 1f);
                icon.preserveAspect = true;
                FW.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -i * StatRow - 1f), new Vector2(19f, 19f), new Vector2(0f, 1f));
                window.statValues[i] = FW.NewLabel(stats, "Value" + i, "0", noto, 15f, FW.Cream, TextAlignmentOptions.MidlineLeft);
                FW.Place(window.statValues[i].rectTransform, new Vector2(0f, 1f), new Vector2(26f, -i * StatRow), new Vector2(120f, StatRow), new Vector2(0f, 1f));
            }
            window.detailBody = FW.NewLabel(content, "DetailBody", "", noto, 15f, FW.Cream, TextAlignmentOptions.TopLeft);
            window.detailBody.textWrappingMode = TextWrappingModes.Normal;
            window.detailBody.overflowMode = TextOverflowModes.Truncate;
            window.detailBody.lineSpacing = 4f;
            window.detailBody.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            window.notice = FW.NewLabel(right, "Notice", "", noto, 14f, FW.Parchment, TextAlignmentOptions.BottomLeft);
            window.notice.textWrappingMode = TextWrappingModes.Normal;
            window.notice.overflowMode = TextOverflowModes.Ellipsis;
            var noticeRect = window.notice.rectTransform;
            noticeRect.anchorMin = Vector2.zero; noticeRect.anchorMax = new Vector2(1f, 0f); noticeRect.pivot = new Vector2(0.5f, 0f);
            noticeRect.offsetMin = new Vector2(12f, 12f); noticeRect.offsetMax = new Vector2(-12f, 12f + NoticeHeight);

            // Bottom row (user 2026-11-24: the action sits next to close): reroll on the left; second action, action, close on
            // the right, like the organization window's delete / confirm pair.
            window.secondaryButton = Plate(panel, "Button_Second", "잠금", noto, 17f, out window.secondaryLabel);
            FW.Place((RectTransform)window.secondaryButton.transform, new Vector2(1f, 0f), new Vector2(-Pad - 2f * (CloseWidth + ButtonGap), Pad),
                new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));
            window.primaryButton = Plate(panel, "Button_Action", "구매", noto, 17f, out window.primaryLabel);
            FW.Place((RectTransform)window.primaryButton.transform, new Vector2(1f, 0f), new Vector2(-Pad - CloseWidth - ButtonGap, Pad),
                new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));
            window.rerollButton = Plate(panel, "Button_Reroll", "리롤", noto, 17f, out window.rerollLabel);
            FW.Place((RectTransform)window.rerollButton.transform, Vector2.zero, new Vector2(Pad, Pad), new Vector2(CloseWidth, CloseHeight), Vector2.zero);
            window.closeButton = Plate(panel, "Button_Close", "닫기", noto, 17f, out _); // Esc closes it too
            FW.Place((RectTransform)window.closeButton.transform, new Vector2(1f, 0f), new Vector2(-Pad, Pad), new Vector2(CloseWidth, CloseHeight), new Vector2(1f, 0f));

            window.tileTemplate = TileTemplate(panel, noto, "TileTemplate", TileWidth, TileHeight, Thumb);
            EditorUtility.SetDirty(window);
            facilities.Configure(start, phaseView, window);
            EditorUtility.SetDirty(facilities);
            panel.gameObject.SetActive(false);
        }

        static Button Plate(RectTransform parent, string name, string caption, TMP_FontAsset noto, float size, out TMP_Text label)
        {
            Button button = GameFacilitySetup.ButtonAt(parent, name, caption, Vector2.zero, Vector2.zero, new Vector2(100f, 30f), null, out _);
            label = FW.PlateButton((RectTransform)button.transform, caption, noto, size);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return button;
        }

        // Column box: dark fill and thin frame from ColumnsTop down to above the bottom row.
        static RectTransform Box(RectTransform panel, string name, float x, float width)
        {
            var box = FW.Node(panel, name);
            box.anchorMin = new Vector2(0f, 0f); box.anchorMax = new Vector2(0f, 1f); box.pivot = new Vector2(0f, 0.5f);
            box.offsetMin = new Vector2(x, ColumnsBottom); box.offsetMax = new Vector2(x + width, -ColumnsTop);
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", BoxFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 14f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            return box;
        }

        static void TopRow(RectTransform rect, float share, bool left)
        {
            rect.anchorMin = new Vector2(left ? 0f : 1f - share, 1f); rect.anchorMax = new Vector2(left ? share : 1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left ? 12f : 0f, -HeaderTop - HeaderHeight);
            rect.offsetMax = new Vector2(left ? 0f : -12f, -HeaderTop);
        }

        // Vertical scroll with a tile grid as content; returns the grid.
        static RectTransform Grid(RectTransform box, string name, out GameObject root)
        {
            var scroll = FW.Node(box, name);
            FW.Stretch(scroll, new Vector2(ListInset, ListInset), new Vector2(-ListInset, -ListTop));
            root = scroll.gameObject;
            var viewport = FW.Node(scroll, "Viewport");
            FW.Stretch(viewport, Vector2.zero, new Vector2(-10f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = FW.Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileWidth, TileHeight);
            grid.spacing = new Vector2(TileGap, TileGap);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = FW.Node(scroll, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-4f, 0f); bar.offsetMax = Vector2.zero;
            var track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.35f);
            var area = FW.Node(bar, "SlidingArea");
            FW.Stretch(area, Vector2.zero, Vector2.zero);
            var handle = FW.Node(area, "Handle");
            FW.Stretch(handle, Vector2.zero, Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = FW.Gold;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;

            var view = scroll.gameObject.AddComponent<ScrollRect>();
            view.viewport = viewport; view.content = content;
            view.horizontal = false; view.vertical = true;
            view.movementType = ScrollRect.MovementType.Clamped; view.scrollSensitivity = 30f;
            view.verticalScrollbar = scrollbar;
            view.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        // Navy thumbnail with the thin frame; returns the icon image inside (hidden until an item has a sprite).
        static Image Thumbnail(RectTransform box, float inset)
        {
            var fill = FW.Image(box, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", ThumbFill, 4f);
            FW.Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var icon = FW.Node(box, "Icon").gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
            FW.Stretch(icon.rectTransform, new Vector2(inset, inset), new Vector2(-inset, -inset));
            var frame = FW.Image(box, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box22_Variant01", ThinFrame, 20f);
            FW.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            return icon;
        }

        // Tile: row-colored plate (gold-tinted on hover), centered thumbnail, name, small sub line, gold tag, gold selection.
        // Also used by the construction bar's piece cards (ConstructionBarLayout, smaller size).
        internal static FacilityTileView TileTemplate(RectTransform panel, TMP_FontAsset noto, string name, float tileWidth, float tileHeight, float thumbSide)
        {
            float TileWidth = tileWidth, Thumb = thumbSide;
            var root = FW.Node(panel, name);
            root.sizeDelta = new Vector2(tileWidth, tileHeight);
            var background = root.gameObject.AddComponent<Image>();
            background.sprite = FW.Sprite("SPR_HUD_FantasyWarrior_Box_White01");
            background.type = Image.Type.Sliced; background.pixelsPerUnitMultiplier = 4f;
            background.color = Color.white;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = FW.Tint(TileFill, TileHover, new Color32(0x2A, 0x24, 0x18, 0xFF));
            colors.disabledColor = TileFill;
            button.colors = colors;
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var thumb = FW.Node(root, "Thumbnail");
            FW.Place(thumb, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(Thumb, Thumb), new Vector2(0.5f, 1f));
            Image icon = Thumbnail(thumb, 5f);

            var label = FW.NewLabel(root, "Name", "이름", noto, 14f, FW.Cream, TextAlignmentOptions.Center);
            label.overflowMode = TextOverflowModes.Ellipsis;
            FW.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f - Thumb), new Vector2(TileWidth - 8f, 26f), new Vector2(0.5f, 1f));
            var sub = FW.NewLabel(root, "Sub", "", noto, 11f, FW.Muted, TextAlignmentOptions.Center);
            sub.overflowMode = TextOverflowModes.Ellipsis;
            FW.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f - Thumb - 22f), new Vector2(TileWidth - 8f, 20f), new Vector2(0.5f, 1f));

            var tag = FW.Node(root, "Tag");
            FW.Place(tag, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(36f, 20f), new Vector2(1f, 1f));
            var tagFill = FW.Image(tag, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", FW.Gold, 6f);
            FW.Stretch(tagFill.rectTransform, Vector2.zero, Vector2.zero);
            var tagText = FW.NewLabel(tag, "Text", "잠금", noto, 11f, FW.Ink, TextAlignmentOptions.Center);
            FW.Stretch(tagText.rectTransform, Vector2.zero, Vector2.zero);

            var selection = FW.Image(root, "Selection", "SPR_FantasyWarrior_Frame_Box22_Variant01", FW.Gold, 14f);
            FW.Stretch(selection.rectTransform, Vector2.zero, Vector2.zero);

            var view = root.gameObject.AddComponent<FacilityTileView>();
            view.Configure(button, group, icon, label, sub, tag.gameObject, tagText, selection.gameObject);
            EditorUtility.SetDirty(view);
            root.gameObject.SetActive(false);
            return view;
        }
    }
}
