using System;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-10: restyles the Title slot window (<c>SlotPanel</c>) to match the restyled main menu (user picked the card
    /// mockup): same backdrop and frame, a steel banner header, the three slots as cards with a gold frame on the selected
    /// one, the same parchment button for "이어 하기" and "새 게임" (user), "뒤로" in the bottom-left and "초기화" (delete slot) in the
    /// bottom-right corner. Korean text in Noto Sans KR. Object names and button events stay the same. Re-runnable.
    /// </summary>
    public static class TitleSlotWindowStyleSetup
    {
        const float CardWidth = 280f, CardHeight = 230f, CardGap = 30f, CardY = 20f;

        [MenuItem("ProjectNeo/Setup/Restyle Title Slot Window (UI-10)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before changing the Title.");
            TMP_FontAsset noto = Noto;
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = FindScreen(scene);
            var panel = (RectTransform)(screen.transform.Find("SlotPanel") ?? throw new InvalidOperationException("Title has no SlotPanel."));

            FullScreen(panel);
            RectTransform visual = FreshVisual(panel);
            Backdrop(visual);
            TMP_Text header = Header(panel, visual, noto);

            var details = new TMP_Text[ProjectNeo.Meta.MetaSaveStore.SlotCount];
            var highlights = new GameObject[details.Length];
            for (int i = 0; i < details.Length; i++)
            {
                var card = (RectTransform)Child(panel, "Button_Slot " + (i + 1));
                float x = (i - (details.Length - 1) * 0.5f) * (CardWidth + CardGap);
                Place(card, new Vector2(0.5f, 0.5f), new Vector2(x, CardY), new Vector2(CardWidth, CardHeight));
                details[i] = Card(card, i, noto, out highlights[i]);
            }

            var status = (RectTransform)Child(panel, "Label_SaveStatus");
            var statusLabel = status.GetComponent<TMP_Text>();
            DestroyComponent<UnityEngine.UI.LayoutElement>(status.gameObject);
            Place(status, new Vector2(0.5f, 0.5f), new Vector2(0f, CardY - CardHeight * 0.5f - 28f), new Vector2(900f, 28f));
            Font(statusLabel, noto, 16f, Cream, TextAlignmentOptions.Center);
            statusLabel.raycastTarget = false;

            var resume = (RectTransform)Child(panel, "Button_Continue");
            Place(resume, new Vector2(0.5f, 0.5f), new Vector2(-160f, -200f), new Vector2(230f, 60f));
            TMP_Text resumeLabel = ParchmentButton(resume, "이어 하기", noto, 22f);
            var fresh = (RectTransform)Child(panel, "Button_New Game");
            Place(fresh, new Vector2(0.5f, 0.5f), new Vector2(160f, -200f), new Vector2(230f, 60f));
            TMP_Text freshLabel = ParchmentButton(fresh, "새 게임", noto, 22f);

            var back = (RectTransform)Child(panel, "Button_Back");
            Place(back, new Vector2(0f, 0f), new Vector2(70f, 72f), new Vector2(110f, 28f), new Vector2(0f, 0f));
            TMP_Text backLabel = TextButton(back, "뒤로", noto, 20f, Gold);
            backLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            backLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var arrow = Image(back, "ICON", "SPR_HUD_FantasyWarrior_Arrow02", Gold, 1f);
            arrow.preserveAspect = true;
            Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 1f), new Vector2(16f, 16f));
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

            // 초기화 deletes the selected slot on the first click (user, UI-10); the result shows in the status line.
            var reset = (RectTransform)Child(panel, "Button_Delete slot");
            Place(reset, new Vector2(1f, 0f), new Vector2(-80f, 72f), new Vector2(120f, 28f), new Vector2(1f, 0f));
            TMP_Text resetLabel = TextButton(reset, "초기화", noto, 20f, Danger);
            resetLabel.alignment = TextAlignmentOptions.MidlineRight;

            screen.ConfigureSlots(details, resetLabel, statusLabel);
            screen.ConfigureSlotHighlights(highlights);
            screen.ConfigureContinue(resume.GetComponent<UnityEngine.UI.Button>(), resumeLabel);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (TMP_Text label in new[] { header, resumeLabel, freshLabel, backLabel, resetLabel, statusLabel }) RequireFont(label, noto);
            foreach (TMP_Text label in details) RequireFont(label, noto);
            return "Title slot window restyled";
        }

        public static TitleScreen FindScreen(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var screen = root.GetComponentInChildren<TitleScreen>(true);
                if (screen != null) return screen;
            }
            throw new InvalidOperationException("Title scene has no TitleScreen.");
        }

        static Transform Child(Transform panel, string name) =>
            panel.Find(name) ?? throw new InvalidOperationException("SlotPanel is missing " + name);

        // The nameplate header is shared with the expedition screen (FantasyHudStyle.Nameplate).
        static TMP_Text Header(RectTransform panel, RectTransform visual, TMP_FontAsset font) =>
            Nameplate(visual, (RectTransform)Child(panel, "Label_SlotsHeader"), "슬롯 선택", font);

        // A slot card: dark parchment fill in a dim bronze frame, gold highlight frame (off until selected), the static
        // slot name and the detail text that TitleScreen fills. The fill takes the button's color tint.
        static TMP_Text Card(RectTransform card, int index, TMP_FontAsset font, out GameObject highlight)
        {
            ClearChildrenExcept(card, "Label");
            DestroyComponent<UnityEngine.UI.LayoutElement>(card.gameObject);
            var hit = card.GetComponent<UnityEngine.UI.Image>();
            hit.sprite = null; hit.color = Color.clear;

            RectTransform visual = FreshVisual(card);
            var fill = Image(visual, "SPR_Background", "SPR_FantasyWarrior_Box_Parchment01", new Color32(0x16, 0x2C, 0x3D, 0xFF), 2f * 3f);
            Stretch(fill.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var frame = Image(visual, "SPR_Frame", "SPR_FantasyWarrior_Frame_Box12", new Color32(0x9A, 0x74, 0x40, 0xB0), 3f * 3f);
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            var gold = Image(visual, "Highlight", "SPR_FantasyWarrior_Frame_Box06", Color.white, 2f * 3f);
            Stretch(gold.rectTransform, new Vector2(-12f, -12f), new Vector2(12f, 12f));
            highlight = gold.gameObject;
            highlight.SetActive(index == 0);

            TMP_Text name = NewLabel(card, "Label_SlotName", "슬롯 " + (index + 1), font, 24f, Gold, TextAlignmentOptions.TopLeft);
            Stretch(name.rectTransform, new Vector2(22f, CardHeight - 56f), new Vector2(-22f, -18f));
            var line = Node(card, "Divider").gameObject.AddComponent<UnityEngine.UI.Image>();
            line.color = new Color32(0x9A, 0x74, 0x40, 0x80);
            line.raycastTarget = false;
            Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(CardWidth - 44f, 1.5f));

            var details = card.Find("Label").GetComponent<TMP_Text>();
            DestroyComponent<UnityEngine.UI.LayoutElement>(details.gameObject);
            Stretch(details.rectTransform, new Vector2(22f, 18f), new Vector2(-22f, -70f));
            details.text = "비어 있음";
            Font(details, font, 16f, Cream, TextAlignmentOptions.TopLeft);
            details.textWrappingMode = TextWrappingModes.Normal;
            details.lineSpacing = 8f;
            details.raycastTarget = false;
            EditorUtility.SetDirty(details);

            var button = card.GetComponent<UnityEngine.UI.Button>();
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            button.targetGraphic = fill;
            button.colors = Tint(new Color(0.9f, 0.9f, 0.9f), Color.white, new Color(0.75f, 0.75f, 0.75f));
            EditorUtility.SetDirty(button);
            return details;
        }
    }
}
