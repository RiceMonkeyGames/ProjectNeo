using System;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-06: restyles the Title expedition screen (<c>AscensionOverlay/Window</c>) in the UI-10 Fantasy Warrior look, as the
    /// user's mockup C: same backdrop, the nameplate header (출정 준비), two cards side by side — 난이도 (level row with
    /// arrow buttons, unlocked line, accumulated rules) and 해금 (unlock points of the slot, hint, 해금 열기 plate button) —
    /// the parchment 출정 button below and 뒤로 in the bottom-left corner. Existing object names, parents and button events
    /// stay the same (tests look up <c>Window/Button_Unlocks</c> and <c>Window/Button_Cancel</c>). Re-runnable.
    /// </summary>
    public static class TitleExpeditionStyleSetup
    {
        const float CardWidth = 400f, CardHeight = 300f, CardY = -5f, CardGap = 40f;
        const float Inner = CardWidth - 44f;
        static float LeftX => -(CardWidth + CardGap) * 0.5f;
        static float RightX => (CardWidth + CardGap) * 0.5f;
        static float CardTop => CardY + CardHeight * 0.5f;

        [MenuItem("ProjectNeo/Setup/Restyle Title Expedition Screen (UI-06)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before changing the Title.");
            TMP_FontAsset noto = Noto;
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = TitleSlotWindowStyleSetup.FindScreen(scene);
            AscensionPicker picker = screen.Ascension ?? throw new InvalidOperationException("TitleScreen has no AscensionPicker.");
            Transform overlay = screen.transform.Find("AscensionOverlay") ?? throw new InvalidOperationException("Title has no AscensionOverlay.");
            var window = (RectTransform)(overlay.Find("Window") ?? throw new InvalidOperationException("AscensionOverlay has no Window."));

            FullScreen(window);
            RectTransform visual = FreshVisual(window);
            Backdrop(visual);
            TMP_Text header = Nameplate(visual, (RectTransform)Child(window, "Label_AscensionTitle"), "출정 준비", noto);
            header.fontStyle = FontStyles.Normal;

            RectTransform left = CardArt(visual, "Card_Ascension", new Vector2(LeftX, CardY), new Vector2(CardWidth, CardHeight));
            Divider(left, "Divider_Title", 52f, 22f);
            Divider(left, "Divider_Rules", 140f, 22f);
            RectTransform right = CardArt(visual, "Card_Unlocks", new Vector2(RightX, CardY), new Vector2(CardWidth, CardHeight));
            Divider(right, "Divider_Title", 52f, 22f);

            var labels = new System.Collections.Generic.List<TMP_Text> { header };
            labels.Add(CardTitle(window, "Label_AscensionCardTitle", "난이도", LeftX, noto));
            labels.AddRange(LevelRow(window, noto));
            TMP_Text unlocked = Fresh(window, "Label_AscensionUnlocked", string.Empty, noto, 15f, Muted, TextAlignmentOptions.Center);
            Place(unlocked.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(LeftX, CardTop - 117f), new Vector2(Inner, 22f));
            labels.Add(unlocked);
            labels.Add(Rules(window, noto));

            labels.Add(CardTitle(window, "Label_UnlocksCardTitle", "해금", RightX, noto));
            TMP_Text points = Fresh(window, "Label_UnlockPoints", "보유 해금 포인트", noto, 20f, Cream, TextAlignmentOptions.MidlineLeft);
            points.richText = true;
            Place(points.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightX, CardTop - 83f), new Vector2(Inner, 32f));
            labels.Add(points);
            TMP_Text hint = Fresh(window, "Label_UnlocksHint", "런 사이 해금으로 다음 런을 미리 강화합니다.", noto, 15f, Muted,
                TextAlignmentOptions.TopLeft);
            hint.textWrappingMode = TextWrappingModes.Normal;
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightX, CardTop - 135f), new Vector2(Inner, 48f));
            labels.Add(hint);

            var unlocks = (RectTransform)Child(window, "Button_Unlocks");
            Place(unlocks, new Vector2(0.5f, 0.5f), new Vector2(RightX, CardY - 85f), new Vector2(230f, 52f));
            labels.Add(PlateButton(unlocks, "해금 열기", noto, 20f));

            var depart = (RectTransform)Child(window, "Button_Start run");
            Place(depart, new Vector2(0.5f, 0.5f), new Vector2(0f, -222f), new Vector2(840f, 192f) * Scale);
            TMP_Text departLabel = ParchmentButton(depart, "출정", noto, 28f);
            departLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);
            var arrow = Image(depart, "ICON", "SPR_HUD_FantasyWarrior_Arrow02", Ink, 1f);
            arrow.preserveAspect = true;
            Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(-46f, 2f), new Vector2(20f, 20f));
            labels.Add(departLabel);
            labels.Add(BackLink((RectTransform)Child(window, "Button_Cancel"), noto));

            picker.ConfigureUnlocked(unlocked);
            screen.ConfigureExpeditionPoints(points);
            EditorUtility.SetDirty(picker);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (TMP_Text label in labels) RequireFont(label, noto);
            return "Title expedition screen restyled";
        }

        static Transform Child(Transform window, string name) =>
            window.Find(name) ?? throw new InvalidOperationException("Expedition window is missing " + name);

        // New labels are destroyed and rebuilt on every run so the setup stays re-runnable.
        static TMP_Text Fresh(Transform window, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment)
        {
            Transform old = window.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            return NewLabel(window, name, text, font, size, color, alignment);
        }

        static TMP_Text CardTitle(Transform window, string name, string text, float x, TMP_FontAsset font)
        {
            TMP_Text title = Fresh(window, name, text, font, 22f, Gold, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, CardTop - 28f), new Vector2(Inner, 30f));
            return title;
        }

        // LevelRow keeps its previous/next buttons and level label; the layout group is replaced by fixed places and the
        // "<" ">" captions by the pack's arrow icon.
        static TMP_Text[] LevelRow(RectTransform window, TMP_FontAsset font)
        {
            var row = (RectTransform)Child(window, "LevelRow");
            DestroyComponent<HorizontalLayoutGroup>(row.gameObject);
            DestroyComponent<LayoutElement>(row.gameObject);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(LeftX, CardTop - 83f), new Vector2(Inner, 44f));

            Button[] buttons = row.GetComponentsInChildren<Button>(true);
            if (buttons.Length != 2) throw new InvalidOperationException("LevelRow should have exactly two buttons.");
            TMP_Text previous = Arrow((RectTransform)buttons[0].transform, 0f, 180f, font);
            TMP_Text next = Arrow((RectTransform)buttons[1].transform, 1f, 0f, font);

            var levelRect = (RectTransform)Child(row, "Label_AscensionLevel");
            DestroyComponent<LayoutElement>(levelRect.gameObject);
            Stretch(levelRect, new Vector2(48f, 0f), new Vector2(-48f, 0f));
            var level = levelRect.GetComponent<TMP_Text>();
            Font(level, font, 26f, Cream, TextAlignmentOptions.Center);
            level.fontStyle = FontStyles.Normal;
            level.raycastTarget = false;
            EditorUtility.SetDirty(level);
            return new[] { previous, next, level };
        }

        static TMP_Text Arrow(RectTransform button, float anchorX, float rotation, TMP_FontAsset font)
        {
            Place(button, new Vector2(anchorX, 0.5f), Vector2.zero, new Vector2(40f, 40f), new Vector2(anchorX, 0.5f));
            TMP_Text caption = TextButton(button, string.Empty, font, 20f, Gold);
            var icon = Image(button, "ICON", "SPR_HUD_FantasyWarrior_Arrow02", Gold, 1f);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
            var target = button.GetComponent<Button>();
            target.targetGraphic = icon;
            EditorUtility.SetDirty(target);
            return caption;
        }

        static TMP_Text Rules(RectTransform window, TMP_FontAsset font)
        {
            var rect = (RectTransform)Child(window, "Label_AscensionRules");
            DestroyComponent<LayoutElement>(rect.gameObject);
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(LeftX, CardTop - 152f - 68f), new Vector2(Inner, 136f));
            var rules = rect.GetComponent<TMP_Text>();
            Font(rules, font, 16f, Cream, TextAlignmentOptions.TopLeft);
            rules.textWrappingMode = TextWrappingModes.Normal;
            rules.lineSpacing = 4f;
            // High levels stack up to ten rules; shrink rather than spill out of the card.
            rules.enableAutoSizing = true;
            rules.fontSizeMin = 11f;
            rules.fontSizeMax = 16f;
            rules.raycastTarget = false;
            EditorUtility.SetDirty(rules);
            return rules;
        }
    }
}
