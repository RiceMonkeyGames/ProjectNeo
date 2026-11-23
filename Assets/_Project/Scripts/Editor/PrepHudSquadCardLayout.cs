using ProjectNeo.Morale;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FW = ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-12 squad card, option A of three (user pick 2026-11-22): the layout A card kept, with an empty portrait box (a squad
    /// leader portrait goes there later), the squad number on the portrait's top-left corner, the order as colored status text right
    /// of the name, headcount as one segment per member (filled while alive) and the morale gauge and line below.
    /// Rebuilds the hidden card template only; <see cref="PrepHudSidebarLayout.Apply"/> calls it after the squad panel.
    /// Running it again gives the same result; running layout A alone gives the old card back.
    /// </summary>
    public static class PrepHudSquadCardLayout
    {
        // 1280x720 Game canvas units. Width follows the sidebar card column (270).
        const float CardHeight = 70f, PortraitSize = 56f, TextLeft = 68f, BadgeWidth = 46f, CountWidth = 44f;
        const float BadgeSize = 24f;
        // Selection outline extents. UI-07 (user 2026-11-23): the outline keeps one even gap to the squad panel's inner frame
        // edge on the left, top and right (about 3.5 canvas units, 8 px on a 3840-wide capture; was 8/9/13 px).
        static readonly Vector2 SelectMin = new Vector2(-11.5f, -6.9f), SelectMax = new Vector2(5.42f, 4.33f);
        internal const string NumberName = "Number", OrderName = "Order", SegmentsName = "HeadcountSegments";
        static readonly Color SegmentOn = new Color32(0x4C, 0x9A, 0xE6, 0xFF);
        static readonly Color SegmentOff = new Color32(0x2A, 0x31, 0x36, 0xE6);
        static readonly Color PlateTint = new Color32(0x10, 0x16, 0x1A, 0xD8);

        internal static void Apply(GameFlowHud hud, HudStyle style, TMP_FontAsset noto)
        {
            var column = (RectTransform)hud.transform.Find(PrepHudSetup.CardsName);
            var card = (RectTransform)column.Find("CardTemplate");
            float width = column.rect.width > 0f ? column.rect.width : 270f;
            card.GetComponent<LayoutElement>().preferredHeight = CardHeight;
            FW.ClearChildrenExcept(card);
            RectTransform visual = FW.FreshVisual(card);

            // Empty portrait box: dark plate, vignette and the layout A frame; no squad-type icon (user 2026-11-22).
            var portrait = FW.Node(visual, "Portrait");
            FW.Place(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(PortraitSize, PortraitSize), new Vector2(0f, 0.5f));
            Stretch(FW.Image(portrait, "SPR_Background", "SPR_HUD_FantasyWarrior_Gradient_Vertical_Solid01", new Color32(0x00, 0x13, 0x35, 0xFF), 1f));
            Stretch(FW.Image(portrait, "SPR_Vignette", "SPR_HUD_FantasyWarrior_Vignette_Box_Large01", new Color32(0x01, 0x0E, 0x25, 0xB2), 9f));
            var frame = FW.Image(portrait, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box_Medium01_Variant01", Color.white, 9f);
            FW.Stretch(frame.rectTransform, new Vector2(-10f, -10f) * FW.Scale, new Vector2(10f, 10f) * FW.Scale);

            // Selection (user 2026-11-22): a gold outline around the whole card with a faint gold wash inside, not just the
            // portrait. Drawn first in Visual so the portrait and text stay on top; on only while selected.
            var outline = FW.Node(visual, "SPR_SelectOutline");
            outline.SetAsFirstSibling();
            // Even margin around the visible content (number badge on the left, badge and title on top, order and count on
            // the right, morale line at the bottom), measured on captures (user 2026-11-22: the badge poked out of the old
            // -4/+4 outline and the gaps differed per side).
            FW.Stretch(outline, SelectMin, SelectMax);
            Stretch(FW.Image(outline, "SPR_Wash", "SPR_HUD_FantasyWarrior_Box_White01", new Color32(0xFF, 0xD8, 0x7A, 0x1F), 4f));
            Stretch(FW.Image(outline, "SPR_Frame", "SPR_HUD_FantasyWarrior_Frame_Box_Small02", new Color32(0xFF, 0xD8, 0x7A, 0xFF), 4f));
            outline.gameObject.SetActive(false);

            // Squad number (the select key) on the portrait's top-left corner: dark disc in the gold ring.
            var badge = FW.Node(card, "NumberBadge");
            float portraitTop = (CardHeight - PortraitSize) * 0.5f;
            FW.Place(badge, new Vector2(0f, 1f), new Vector2(-5f, -portraitTop + 5f), new Vector2(BadgeSize, BadgeSize), new Vector2(0f, 1f));
            Round(badge, "SPR_Background", "SPR_HUD_FantasyWarrior_Circle_Medium01_Clean", new Color32(0x12, 0x2A, 0x5A, 0xFF), 3f);
            Round(badge, "SPR_Frame", "SPR_FantasyWarrior_Ring_Large06", Color.white, 0f);
            var number = PrepHudLayoutA.Label(badge, NumberName, "1", FW.Grenze, 16f, FW.Cream, TextAlignmentOptions.Center);
            FW.Stretch(number.rectTransform, Vector2.zero, new Vector2(0f, 1f));

            var title = PrepHudLayoutA.Label(card, "Title", "부대 1", noto, 15f, Color.white, TextAlignmentOptions.TopLeft);
            PrepHudLayoutA.Outline(title);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            FW.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(TextLeft, -2f), new Vector2(width - TextLeft - BadgeWidth - 4f, 24f), new Vector2(0f, 1f));

            // Order as status text right of the name, no plate (user 2026-11-22: the framed badge read as a button). The color
            // follows the order (PrepSquadCard: hold gray, move blue, attack red).
            var order = PrepHudLayoutA.Label(card, OrderName, "정지", noto, 13f, FW.Muted, TextAlignmentOptions.TopRight);
            PrepHudLayoutA.Outline(order);
            order.textWrappingMode = TextWrappingModes.NoWrap;
            FW.Place(order.rectTransform, new Vector2(1f, 1f), new Vector2(-2f, -3f), new Vector2(BadgeWidth, 22f), new Vector2(1f, 1f));

            // Headcount: one segment per member on a dark plate, count on the right.
            float rowWidth = width - TextLeft - 2f - CountWidth - 4f;
            var row = FW.Node(card, SegmentsName);
            FW.Place(row, new Vector2(0f, 1f), new Vector2(TextLeft + 2f, -28f), new Vector2(rowWidth, 10f), new Vector2(0f, 1f));
            var plate = FW.Image(row, "SPR_Background", "SPR_HUD_FantasyWarrior_Box_White01", PlateTint, 4f);
            FW.Stretch(plate.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(1, 1, 1, 1);
            line.spacing = 2f;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = true;
            var segment = FW.Image(row, "SegmentTemplate", "SPR_HUD_FantasyWarrior_Box_White01", SegmentOn, 8f);
            segment.gameObject.SetActive(false);
            var people = PrepHudLayoutA.Label(card, "Headcount", "0/0명", noto, 12f, FW.Cream, TextAlignmentOptions.MidlineRight);
            PrepHudLayoutA.Outline(people);
            FW.Place(people.rectTransform, new Vector2(1f, 1f), new Vector2(-2f, -24f), new Vector2(CountWidth, 18f), new Vector2(1f, 1f));

            // Morale: layout A gauge and line, moved down under the headcount row.
            var moraleTrack = FW.Node(card, "MoraleTrack");
            FW.Place(moraleTrack, new Vector2(0f, 1f), new Vector2(TextLeft + 2f, -46f), new Vector2(width - TextLeft - 8f, 7f), new Vector2(0f, 1f));
            Image moraleImage = PrepHudLayoutA.Gauge(moraleTrack, style.MoraleFill(MoraleBand.Normal));
            var moraleFrame = FW.Image(card, "MoraleFrame", "SPR_FantasyWarrior_Bar_Horizontal02", Color.white, 9f);
            FW.Place(moraleFrame.rectTransform, new Vector2(0f, 1f), new Vector2(TextLeft - 4f, -41f), new Vector2(width - TextLeft + 4f, 17f), new Vector2(0f, 1f));
            var morale = PrepHudLayoutA.Label(card, "Morale", "사기 -", noto, 12f, Color.white, TextAlignmentOptions.TopLeft);
            PrepHudLayoutA.Outline(morale);
            FW.Place(morale.rectTransform, new Vector2(0f, 1f), new Vector2(TextLeft + 2f, -54f), new Vector2(width - TextLeft - 4f, 16f), new Vector2(0f, 1f));

            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var template = card.GetComponent<PrepSquadCard>();
            template.Configure(null, title, people, null, morale, (RectTransform)moraleImage.transform, moraleImage);
            template.ConfigureFrame(null);
            template.ConfigureSelectionOutline(outline.gameObject);
            template.ConfigureDetails(number, order, segment, SegmentOn, SegmentOff);
            EditorUtility.SetDirty(template);
            PageMarks(hud, column, noto);
            FitColumn(column, style);
        }

        const float BottomRow = 22f, PageMarksWidth = 80f;

        // The column (and the squad panel drawn around it) is exactly one page of cards plus a bottom row for the message
        // line and the page marks (user 2026-11-22: a full page left too much empty panel above the HP). Top-anchored, so
        // the panel ends there and the gap down to the HP stays empty.
        static void FitColumn(RectTransform column, HudStyle style)
        {
            var layout = column.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 8f, padTop = layout != null ? layout.padding.top : 0f;
            int perPage = style.SquadCardsPerPage;
            float height = padTop + perPage * CardHeight + perPage * spacing + BottomRow;
            float left = column.offsetMin.x, width = column.offsetMax.x - column.offsetMin.x, top = -column.offsetMax.y;
            column.anchorMin = column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.sizeDelta = new Vector2(width, height);
            column.anchoredPosition = new Vector2(left, -top);
            Transform message = column.Find("Message");
            if (message != null) message.GetComponent<TMP_Text>().margin = new Vector4(0f, 0f, PageMarksWidth, 0f);
        }

        internal const string PageMarksName = "PageIndicator";

        // Page indicator at the bottom-right of the card column (user pick 2026-11-22: pages of HudStyle.SquadCardsPerPage
        // cards, turned with [ and ]): a red dot in the panel's bottom-right corner while a squad on another page has low
        // morale, and "[  1/2  ]" (the brackets are the keys) right beside it (user 2026-11-22). Both stay hidden while every squad fits on one page.
        static void PageMarks(GameFlowHud hud, RectTransform column, TMP_FontAsset noto)
        {
            Transform old = column.Find(PageMarksName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var marks = FW.Node(column, PageMarksName);
            marks.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            FW.Place(marks, new Vector2(1f, 0f), new Vector2(-2f, 0f), new Vector2(PageMarksWidth, 20f), new Vector2(1f, 0f));
            var label = PrepHudLayoutA.Label(marks, "PageLabel", "[  1/2  ]", noto, 14f, FW.Cream, TextAlignmentOptions.MidlineRight);
            PrepHudLayoutA.Outline(label);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            FW.Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(PageMarksWidth - 16f, 20f), new Vector2(1f, 0.5f));
            label.gameObject.SetActive(false);
            var dot = FW.Image(marks, "RiskDot", "SPR_HUD_FantasyWarrior_Circle_Medium01_Clean", new Color32(0xE2, 0x4B, 0x4A, 0xFF), 1f);
            dot.type = Image.Type.Simple;
            dot.preserveAspect = true;
            FW.Place(dot.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(1f, 0.5f));
            dot.gameObject.SetActive(false);
            foreach (var graphic in marks.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var controller = hud.GetComponent<PrepSquadCardsHud>();
            if (controller == null) return;
            controller.ConfigurePages(label, dot.gameObject);
            EditorUtility.SetDirty(controller);
        }

        static void Stretch(Image image) => FW.Stretch(image.rectTransform, Vector2.zero, Vector2.zero);

        static void Round(RectTransform parent, string name, string sprite, Color color, float inset)
        {
            var image = FW.Image(parent, name, sprite, color, 1f);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            FW.Stretch(image.rectTransform, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }
    }
}
