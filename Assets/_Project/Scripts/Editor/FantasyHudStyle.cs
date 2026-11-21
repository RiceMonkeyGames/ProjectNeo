using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-10: shared building blocks for Title screens drawn with Synty Interface Fantasy Warrior HUD art.
    /// Sizes follow the pack samples (3840x2160 canvas) divided by <see cref="Scale"/> for the Title canvas (1280x720
    /// reference); sliced sprites get a 3x pixels-per-unit multiplier so border widths match the samples.
    /// </summary>
    public static class FantasyHudStyle
    {
        public const string PackRoot = "Assets/ThirdParty/Synty/InterfaceFantasyWarriorHUD";
        public const string GrenzePath = PackRoot + "/Fonts/Grenze/Grenze-SemiBold SDF.asset";
        public const string NotoPath = "Assets/_Project/Art/Fonts/NotoSansKR SDF.asset";
        public const float Scale = 1f / 3f;

        public static readonly Color Ink = new Color32(0x1F, 0x2B, 0x2E, 0xFF);
        public static readonly Color Gold = new Color32(0xE3, 0xBB, 0x71, 0xFF);
        public static readonly Color Parchment = new Color32(0xF4, 0xC6, 0x6C, 0xFF);
        public static readonly Color Cream = new Color32(0xF2, 0xEA, 0xD8, 0xFF);
        public static readonly Color Muted = new Color32(0xA9, 0xB8, 0xC2, 0xFF);
        public static readonly Color Danger = new Color32(0xD9, 0x8A, 0x7A, 0xFF);

        public static TMP_FontAsset Grenze => Load<TMP_FontAsset>(GrenzePath);
        public static TMP_FontAsset Noto => Load<TMP_FontAsset>(NotoPath);

        static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        /// <summary>Sample "Background01": painted backdrop, tracery frame inset from the edges, curlicue gems top and bottom.</summary>
        public static void Backdrop(RectTransform visual)
        {
            var backdrop = Image(visual, "Background", "SPR_HUD_FantasyWarrior_Example_Background", Color.white, 1f);
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.zero);

            var frame = Node(visual, "Frame");
            Stretch(frame, new Vector2(60f, 90f + 30f) * Scale, new Vector2(-60f, -90f + 30f) * Scale);
            var tracery = Image(frame, "SPR_Frame_Simple", "SPR_HUD_FantasyWarrior_Tracery_FrameDetail01",
                new Color32(0xA4, 0x78, 0x3F, 0x74), 1.5f * 3f);
            Stretch(tracery.rectTransform, Vector2.zero, Vector2.zero);
            Gem(frame, "SPR_Greeble_Curlicue_Top", 1f, -40f, 0f);
            Gem(frame, "SPR_Greeble_Curlicue_Bottom", 0f, 40f, 180f);
        }

        static void Gem(RectTransform frame, string name, float anchorY, float y, float rotation)
        {
            var gem = Image(frame, name, "SPR_FantasyWarrior_Greeble_Curlicue17", new Color32(0xE0, 0xDA, 0xC5, 0xFF), 1f);
            gem.preserveAspect = true;
            Place(gem.rectTransform, new Vector2(0.5f, anchorY), new Vector2(0f, y) * Scale, new Vector2(130f, 130f) * Scale);
            gem.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        /// <summary>Replaces the target's art layer: destroys an existing <c>Visual</c> child and adds a fresh full-size one first.</summary>
        public static RectTransform FreshVisual(Transform target)
        {
            Transform old = target.Find("Visual");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var visual = Node(target, "Visual");
            Stretch(visual, Vector2.zero, Vector2.zero);
            visual.SetAsFirstSibling();
            return visual;
        }

        /// <summary>Turns a layout-group panel into a full-screen screen of its own (no background image or layout).</summary>
        public static void FullScreen(RectTransform panel)
        {
            DestroyComponent<ContentSizeFitter>(panel.gameObject);
            DestroyComponent<VerticalLayoutGroup>(panel.gameObject);
            DestroyComponent<UnityEngine.UI.Image>(panel.gameObject);
            Stretch(panel, Vector2.zero, Vector2.zero);
        }

        /// <summary>
        /// Parchment button in a Banner07 frame (sample "Button_Start"). The button's own image becomes a transparent hit
        /// area and the parchment takes the color tint. Keeps the child named <c>Label</c>, drops other children.
        /// </summary>
        public static TMP_Text ParchmentButton(RectTransform button, string caption, TMP_FontAsset font, float size)
        {
            RectTransform visual = PrepareButton(button);
            var parchment = Image(visual, "SPR_Background", "SPR_FantasyWarrior_Box_Parchment01", Parchment, 2f * 3f);
            Stretch(parchment.rectTransform, Vector2.zero, Vector2.zero);
            var frame = Image(visual, "SPR_Frame", "SPR_FantasyWarrior_Banner07", Color.white, 3f * 3f);
            Stretch(frame.rectTransform, new Vector2(-65f, 0f) * Scale, new Vector2(65f, 0f) * Scale);
            Tinted(button, parchment, new Color(0.92f, 0.92f, 0.92f), Color.white);
            return Caption(button, caption, font, size, Ink);
        }

        /// <summary>A small caption-only button (sample "Button_Quit"): transparent hit area, tint on the caption.</summary>
        public static TMP_Text TextButton(RectTransform button, string caption, TMP_FontAsset font, float size, Color color)
        {
            ClearChildrenExcept(button, "Label");
            DestroyComponent<LayoutElement>(button.gameObject);
            var hit = button.GetComponent<UnityEngine.UI.Image>();
            hit.sprite = null; hit.color = Color.clear;
            TMP_Text label = Caption(button, caption, font, size, color);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            var target = button.GetComponent<Button>();
            target.transition = Selectable.Transition.ColorTint;
            target.targetGraphic = label;
            target.colors = Tint(Color.white, new Color32(0xFF, 0xF0, 0x8A, 0xFF), new Color(0.78f, 0.78f, 0.78f));
            EditorUtility.SetDirty(target);
            return label;
        }

        static RectTransform PrepareButton(RectTransform button)
        {
            ClearChildrenExcept(button, "Label");
            DestroyComponent<LayoutElement>(button.gameObject);
            var hit = button.GetComponent<UnityEngine.UI.Image>();
            hit.sprite = null; hit.color = Color.clear;
            return FreshVisual(button);
        }

        static void Tinted(RectTransform button, Graphic target, Color normal, Color highlighted)
        {
            var selectable = button.GetComponent<Button>();
            selectable.transition = Selectable.Transition.ColorTint;
            selectable.targetGraphic = target;
            ColorBlock colors = Tint(normal, highlighted, new Color(0.78f, 0.78f, 0.78f));
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            selectable.colors = colors;
            EditorUtility.SetDirty(selectable);
        }

        static TMP_Text Caption(RectTransform button, string caption, TMP_FontAsset font, float size, Color color)
        {
            var label = button.Find("Label").GetComponent<TMP_Text>();
            DestroyComponent<LayoutElement>(label.gameObject);
            Stretch(label.rectTransform, new Vector2(0f, 2f), Vector2.zero);
            label.text = caption;
            Font(label, font, size, color, TextAlignmentOptions.Center);
            EditorUtility.SetDirty(label);
            return label;
        }

        public static ColorBlock Tint(Color normal, Color highlighted, Color pressed)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.selectedColor = normal;
            colors.pressedColor = pressed;
            return colors;
        }

        /// <summary>
        /// Sets font and look. Font and material are written through the serialized fields: right after a domain reload
        /// the TMP <c>font</c> setter sometimes kept the old font for the dynamic Grenze asset and the scene was saved with
        /// LiberationSans.
        /// </summary>
        public static void Font(TMP_Text label, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var serialized = new SerializedObject(label);
            serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterSpacing = font.name.StartsWith("Grenze") ? 2f : 0f;
            label.raycastTarget = true;
        }

        public static TMP_Text NewLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment)
        {
            var label = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            Font(label, font, size, color, alignment);
            label.raycastTarget = false;
            return label;
        }

        public static Image Image(Transform parent, string name, string spriteName, Color color, float pixelsPerUnit)
        {
            var image = Node(parent, name).gameObject.AddComponent<Image>();
            image.sprite = Sprite(spriteName);
            image.color = color;
            image.raycastTarget = false;
            image.type = image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            return image;
        }

        public static Sprite Sprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { PackRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            throw new InvalidOperationException("Fantasy Warrior HUD sprite is missing: " + name);
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) =>
            Place(rect, anchor, position, size, new Vector2(0.5f, 0.5f));

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        public static void ClearChildrenExcept(Transform parent, params string[] keep)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (Array.IndexOf(keep, parent.GetChild(i).name) < 0) UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        public static void DestroyComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        /// <summary>Throws when a label lost its font on save (see <see cref="Font"/>).</summary>
        public static void RequireFont(TMP_Text label, TMP_FontAsset font)
        {
            if (label.font != font) throw new InvalidOperationException(label.transform.parent.name + "/" + label.name + " did not keep " + font.name + ".");
        }
    }
}
