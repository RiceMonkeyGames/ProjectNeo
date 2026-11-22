using System;
using ProjectNeo.Meta;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ProjectNeo.Editor.FantasyHudStyle;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-06: rebuilds the Title unlock screen (<c>UnlockOverlay</c>) as a full-screen panel of its own over the expedition
    /// screen, in the UI-10 Fantasy Warrior look, as the user's mockup B: nameplate header (해금), unlock points top-right,
    /// track buttons on the left (name and opened count, gold frame on the selected one), the selected track's entries as
    /// cards on the right, one parchment "무작위 해금" button (opens a random candidate at once, user 2026-11-21), a status
    /// line and 뒤로 bottom-left. Re-runnable: the old overlay is destroyed and built again.
    /// </summary>
    public static class TitleUnlockScreenSetup
    {
        const float ListX = -430f, ListTop = 150f, TrackWidth = 250f, TrackHeight = 60f, TrackGap = 12f;
        const float RightX = 130f, RightWidth = 820f;
        const float CardWidth = 260f, CardHeight = 125f;
        const float ThumbSize = 92f, ThumbInset = 14f;

        [MenuItem("ProjectNeo/Setup/Restyle Title Unlock Screen (UI-06)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before changing the Title.");
            var catalog = AssetDatabase.LoadAssetAtPath<UnlockCatalog>(UnlockCatalogSetup.CatalogPath)
                ?? throw new InvalidOperationException("Unlock catalog is missing: " + UnlockCatalogSetup.CatalogPath);
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = TitleSlotWindowStyleSetup.FindScreen(scene);
            UnlockScreen unlocks = screen.Unlocks ?? screen.GetComponent<UnlockScreen>()
                ?? throw new InvalidOperationException("TitleScreen has no UnlockScreen.");
            TMP_Text[] labels = Build(screen, unlocks, catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            TMP_FontAsset noto = Noto;
            foreach (TMP_Text label in labels) RequireFont(label, noto);
            return "Title unlock screen rebuilt";
        }

        /// <summary>
        /// Destroys any <c>UnlockOverlay</c> under the Title screen and builds the new panel, drawn above the expedition screen.
        /// Wires <paramref name="unlocks"/> and the TitleScreen. Returns the labels so callers can check their font after saving.
        /// </summary>
        public static TMP_Text[] Build(TitleScreen screen, UnlockScreen unlocks, UnlockCatalog catalog)
        {
            TMP_FontAsset noto = Noto;
            Transform old = screen.transform.Find("UnlockOverlay");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var overlay = new GameObject("UnlockOverlay", typeof(RectTransform), typeof(Image));
            overlay.layer = screen.gameObject.layer;
            var root = (RectTransform)overlay.transform;
            root.SetParent(screen.transform, false);
            Stretch(root, Vector2.zero, Vector2.zero);
            // Clear but raycast-blocking so nothing under the panel can be clicked; the backdrop hides the expedition screen.
            overlay.GetComponent<Image>().color = Color.clear;
            Transform expedition = screen.transform.Find("AscensionOverlay");
            if (expedition != null) root.SetSiblingIndex(expedition.GetSiblingIndex() + 1);

            var labels = new System.Collections.Generic.List<TMP_Text>();
            RectTransform visual = FreshVisual(root);
            Backdrop(visual);
            TMP_Text title = NewLabel(root, "Label_UnlocksTitle", "해금", noto, 28f, Cream, TextAlignmentOptions.Center);
            labels.Add(Nameplate(visual, title.rectTransform, "해금", noto));

            TMP_Text points = NewLabel(root, "Label_UnlockPoints", "보유 해금 포인트  0", noto, 20f, Cream, TextAlignmentOptions.MidlineRight);
            // Top-right corner: same gap to the frame lines as the bottom corners.
            Place(points.rectTransform, Vector2.one, new Vector2(-CornerX, -CornerY), new Vector2(320f, CornerHeight), Vector2.one);
            labels.Add(points);

            // Left: one button per catalog track.
            int count = catalog.Tracks.Count;
            var trackButtons = new Button[count];
            var trackLabels = new TMP_Text[count];
            var highlights = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var button = (RectTransform)ConstructionPrototypeSetup.Button(root, "Track " + (i + 1), null, "분류").transform;
                Place(button, new Vector2(0.5f, 0.5f), new Vector2(ListX, ListTop - i * (TrackHeight + TrackGap)), new Vector2(TrackWidth, TrackHeight));
                TMP_Text label = PlateButton(button, catalog.Tracks[i] != null ? catalog.Tracks[i].DisplayName : "분류", noto, 19f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(18f, 0f);
                label.richText = true;
                label.lineSpacing = -6f;
                var gold = Image(button.Find("Visual"), "Highlight", "SPR_FantasyWarrior_Frame_Box06", Color.white, 2f * 3f);
                Stretch(gold.rectTransform, new Vector2(-8f, -8f), new Vector2(8f, 8f));
                gold.gameObject.SetActive(i == 0);
                var target = button.GetComponent<Button>();
                UnityEventTools.AddIntPersistentListener(target.onClick, unlocks.SelectTrack, i);
                EditorUtility.SetDirty(target);
                trackButtons[i] = target; trackLabels[i] = label; highlights[i] = gold.gameObject;
                labels.Add(label);
            }

            // Right: selected track title, entry cards, random unlock button, status.
            TMP_Text trackTitle = NewLabel(root, "Label_TrackTitle", "분류", noto, 24f, Gold, TextAlignmentOptions.MidlineLeft);
            trackTitle.richText = true;
            Place(trackTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightX, ListTop + 12f), new Vector2(RightWidth, 36f));
            labels.Add(trackTitle);
            var line = Node(visual, "Divider_Entries").gameObject.AddComponent<Image>();
            line.color = new Color32(0x9A, 0x74, 0x40, 0x80);
            line.raycastTarget = false;
            Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightX, ListTop - 12f), new Vector2(RightWidth, 1.5f));

            RectTransform grid = Node(root, "Entries");
            Place(grid, new Vector2(0.5f, 0.5f), new Vector2(RightX, ListTop - 26f), new Vector2(RightWidth, 2f * CardHeight + 18f), new Vector2(0.5f, 1f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CardWidth, CardHeight);
            layout.spacing = new Vector2(20f, 18f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperLeft;
            GameObject template = EntryTemplate(root, noto, labels);

            TMP_Text status = NewLabel(root, "Label_UnlockStatus", string.Empty, noto, 17f, Cream, TextAlignmentOptions.Center);
            Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightX, -165f), new Vector2(RightWidth, 26f));
            labels.Add(status);

            var draw = (RectTransform)ConstructionPrototypeSetup.Button(root, "Draw", unlocks.DrawFromButton, "무작위 해금").transform;
            // Right-aligned with the card grid's right edge (user, UI-06); the Banner07 frame juts out 65 (sample) past the
            // button, so the button stops that much short of the edge.
            Place(draw, new Vector2(0.5f, 0.5f), new Vector2(RightX + RightWidth * 0.5f - 65f * Scale, -222f), new Vector2(840f, 192f) * Scale, new Vector2(1f, 0.5f));
            TMP_Text drawLabel = ParchmentButton(draw, "무작위 해금", noto, 22f);
            drawLabel.richText = true;
            labels.Add(drawLabel);

            var back = (RectTransform)ConstructionPrototypeSetup.Button(root, "Back", unlocks.Close, "뒤로").transform;
            labels.Add(BackLink(back, noto));

            unlocks.Configure(catalog, overlay, points, status);
            unlocks.ConfigureTracks(trackButtons, trackLabels, highlights);
            unlocks.ConfigureEntries(trackTitle, grid, template, draw.GetComponent<Button>(), drawLabel);
            screen.ConfigureUnlocks(unlocks);
            overlay.SetActive(false);
            EditorUtility.SetDirty(unlocks);
            EditorUtility.SetDirty(screen);
            return labels.ToArray();
        }

        // Inactive card the screen copies into Entries: fill (tinted per state by UnlockScreen), dim bronze frame, gold
        // "just opened" highlight, thumbnail slot, name and state lines.
        static GameObject EntryTemplate(RectTransform root, TMP_FontAsset font, System.Collections.Generic.List<TMP_Text> labels)
        {
            RectTransform card = Node(root, "EntryTemplate");
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            RectTransform visual = FreshVisual(card);
            var fill = Image(visual, "Fill", "SPR_FantasyWarrior_Box_Parchment01", new Color32(0x1F, 0x3A, 0x52, 0xFF), 2f * 3f);
            Stretch(fill.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var frame = Image(visual, "Frame", "SPR_FantasyWarrior_Frame_Box12", CardFrame, 3f * 3f);
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            var gold = Image(visual, "Highlight", "SPR_FantasyWarrior_Frame_Box06", Color.white, 2f * 3f);
            Stretch(gold.rectTransform, new Vector2(-10f, -10f), new Vector2(10f, 10f));
            gold.gameObject.SetActive(false);

            // Thumbnail slot (user, UI-06): a dark framed square on the left. UnlockScreen puts UnlockEntry.Icon in
            // Thumbnail/Icon and hides the icon when an entry has none, so only the empty frame shows.
            RectTransform thumb = Node(visual, "Thumbnail");
            Place(thumb, new Vector2(0f, 0.5f), new Vector2(ThumbInset, 0f), new Vector2(ThumbSize, ThumbSize), new Vector2(0f, 0.5f));
            var slot = Image(thumb, "Background", "SPR_HUD_FantasyWarrior_Box_Background01", new Color32(0x0C, 0x16, 0x1F, 0xFF), 1f * 3f);
            Stretch(slot.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var icon = Node(thumb, "Icon").gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Stretch(icon.rectTransform, new Vector2(7f, 7f), new Vector2(-7f, -7f));
            icon.enabled = false;
            var slotFrame = Image(thumb, "Frame", "SPR_FantasyWarrior_Frame_Box12", CardFrame, 3f * 3f);
            Stretch(slotFrame.rectTransform, Vector2.zero, Vector2.zero);

            const float textLeft = ThumbInset + ThumbSize + 14f;
            TMP_Text name = NewLabel(card, "Label_Name", "항목", font, 21f, Cream, TextAlignmentOptions.TopLeft);
            Stretch(name.rectTransform, new Vector2(textLeft, 50f), new Vector2(-16f, -16f));
            name.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text state = NewLabel(card, "Label_State", "잠김", font, 16f, Gold, TextAlignmentOptions.BottomLeft);
            Stretch(state.rectTransform, new Vector2(textLeft, 14f), new Vector2(-16f, -70f));
            labels.Add(name); labels.Add(state);
            card.gameObject.SetActive(false);
            return card.gameObject;
        }
    }
}
