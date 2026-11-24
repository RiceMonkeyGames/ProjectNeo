using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 실행 중에 코드로 만드는 HUD(전투 상황판·스킬 칸·보스 체력바, UI-13)의 공용 부품. 준비 HUD 사이드바와 같은 모양
    /// (어두운 판 + 두꺼운 금색 Box22 테두리)을 <see cref="HudStyle"/>의 창 그림으로 그린다. 모양 값이 없으면 평평한 판만 깐다.
    /// </summary>
    public static class HudPlate
    {
        /// <summary>준비 HUD 사이드바 판 색(PrepHudSidebarLayout.GoldPlate와 같다).</summary>
        public static readonly Color PlateColor = new Color32(0x10, 0x16, 0x1A, 0xD8);
        /// <summary>게이지 안쪽 어두운 바탕.</summary>
        public static readonly Color TrackColor = new Color32(0x05, 0x07, 0x08, 0xE6);
        /// <summary>체력 칸 빨강(플레이어 체력바와 같다).</summary>
        public static readonly Color HealthRed = new Color32(0xE8, 0x3A, 0x34, 0xFF);
        public static readonly Color Gold = new Color32(0xE3, 0xBB, 0x71, 0xFF);
        public static readonly Color Cream = new Color32(0xF2, 0xEA, 0xD8, 0xFF);
        static readonly Color FallbackFrame = new Color(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>같은 씬의 준비 HUD 공용 모양 값(분대 카드가 들고 있다). 없으면 null.</summary>
        public static HudStyle FindStyle(Scene scene)
        {
            foreach (PrepSquadCardsHud cards in Object.FindObjectsByType<PrepSquadCardsHud>(FindObjectsInactive.Include))
                if (cards.gameObject.scene == scene && cards.Style != null) return cards.Style;
            return null;
        }

        /// <summary>1280x720 기준 화면 덮개 캔버스. 준비 HUD 캔버스와 같은 비율로 맞춘다.</summary>
        public static RectTransform OverlayCanvas(Transform parent, string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)root.transform;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>단색 상자(그림 없음).</summary>
        public static Image Box(Transform parent, string name, Color color)
        {
            var image = Node(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// 판 하나: 어두운 판 그림(창 판, 배율 2)과 그 둘레 금색 테두리(창 테두리, 배율 frameScale, 2만큼 밖으로).
        /// 사이드바처럼 260 폭 판은 10, 작은 칸은 더 크게 주면 테두리가 가늘어진다. 테두리 그림을 돌려준다(없으면 null).
        /// </summary>
        public static Image Plate(RectTransform parent, HudStyle style, Color plateColor, float frameScale = 10f)
        {
            var plate = Box(parent, "SPR_Background", plateColor);
            Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            Skin(plate, style != null ? style.WindowPlateSprite : null, style != null ? style.WindowPlatePixelsPerUnit : 1f);
            if (style == null || style.WindowFrameSprite == null) return null;
            var frame = Box(parent, "SPR_Frame", style.WindowFrame);
            Stretch(frame.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            Skin(frame, style.WindowFrameSprite, frameScale);
            return frame;
        }

        /// <summary>테두리 색 기본값(모양 값이 없을 때).</summary>
        public static Color FrameColor(HudStyle style) => style != null ? style.WindowFrame : FallbackFrame;

        static void Skin(Image image, Sprite sprite, float pixelsPerUnit)
        {
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
        }

        /// <summary>한 줄 글. 모양 값의 창 글꼴(NotoSansKR)을 쓴다. 줄바꿈 없이.</summary>
        public static TMP_Text Label(Transform parent, string name, HudStyle style, float size, Color color, TextAlignmentOptions alignment)
        {
            var label = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            if (style != null && style.WindowFont != null) label.font = style.WindowFont;
            label.text = string.Empty;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>금색 가는 줄 가운데 작은 마름모(사이드바 머리 패널의 선).</summary>
        public static void Divider(RectTransform panel, float inset, float y, float width)
        {
            const float diamond = 7f, room = 10f;
            float half = (width - 2f * inset) * 0.5f - room;
            var line = new Color(Gold.r, Gold.g, Gold.b, 0.75f);
            for (int side = -1; side <= 1; side += 2)
            {
                var stroke = Box(panel, side < 0 ? "DividerLeft" : "DividerRight", line);
                Place(stroke.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * (room + half * 0.5f), -y), new Vector2(half, 1.5f), new Vector2(0.5f, 0.5f));
            }
            var gem = Box(panel, "DividerDiamond", Gold);
            Place(gem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(diamond, diamond), new Vector2(0.5f, 0.5f));
            gem.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }
    }
}
