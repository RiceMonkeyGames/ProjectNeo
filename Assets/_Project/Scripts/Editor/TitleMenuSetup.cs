using System;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// UI-05: splits the Title into a main menu shown first (logo, start, quit) and a slot window opened from it
    /// (header, three slots, status, continue, new game, delete, back). New game opens the expedition screen (the former
    /// Ascension window: level choice, unlocks, depart, back), so between-run unlocks are bought for the chosen slot
    /// right before a run. Existing objects keep their names and serialized references. Re-runnable.
    /// </summary>
    public static class TitleMenuSetup
    {
        const float SlotPanelWidth = 640f;

        static readonly string[] SlotChildren =
        {
            "Button_Slot 1", "Button_Slot 2", "Button_Slot 3", "Label_SaveStatus",
            "Button_Continue", "Button_New Game", "Button_Delete slot",
        };

        [MenuItem("ProjectNeo/Setup/Split Title Main Menu And Slot Window (UI-05)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before changing the Title.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                screen = screen != null ? screen : root.GetComponentInChildren<TitleScreen>(true);
            if (screen == null) throw new InvalidOperationException("Title scene has no TitleScreen.");
            Transform panel = screen.transform.Find("Panel") ?? throw new InvalidOperationException("Title has no Panel.");

            GameObject slotPanel = SlotPanel(screen.transform, panel);
            for (int i = 0; i < SlotChildren.Length; i++)
            {
                Transform child = panel.Find(SlotChildren[i]) ?? slotPanel.transform.Find(SlotChildren[i])
                    ?? throw new InvalidOperationException("Title is missing " + SlotChildren[i]);
                child.SetParent(slotPanel.transform, false);
                child.SetSiblingIndex(i + 1);
            }

            if (panel.Find("Button_Start") == null)
            {
                GameObject start = ConstructionPrototypeSetup.Button(panel, "Start", screen.OpenSlotMenu, "게임 시작");
                start.transform.SetSiblingIndex(1);
            }
            if (slotPanel.transform.Find("Button_Back") == null)
                ConstructionPrototypeSetup.Button(slotPanel.transform, "Back", screen.BackToMain, "뒤로");
            slotPanel.transform.Find("Button_Back").SetAsLastSibling();
            panel.Find("Button_Quit").SetAsLastSibling();
            Expedition(screen, panel, slotPanel.transform);

            screen.ConfigureMenus(panel.gameObject, slotPanel);
            slotPanel.SetActive(false);
            panel.gameObject.SetActive(true);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Title split into main menu and slot window";
        }

        // The Ascension window becomes the expedition screen: header, level row, rules, unlocks, depart, back.
        // It is a step of its own, not a popup: the overlay is clear (main and slot screens are hidden while it is
        // open), the window is opaque like the other panels, and back returns to the slot window.
        // The unlock overlay is drawn above it so unlocks open on top of the expedition screen.
        static void Expedition(TitleScreen screen, Transform panel, Transform slotPanel)
        {
            Transform screenRoot = screen.transform;
            Transform overlay = screenRoot.Find("AscensionOverlay") ?? throw new InvalidOperationException("Title has no AscensionOverlay.");
            Transform window = overlay.Find("Window") ?? throw new InvalidOperationException("AscensionOverlay has no Window.");
            Caption(window.Find("Label_AscensionTitle"), "출정 준비");
            Transform unlocks = panel.Find("Button_Unlocks") ?? slotPanel.Find("Button_Unlocks") ?? window.Find("Button_Unlocks")
                ?? throw new InvalidOperationException("Title is missing Button_Unlocks");
            Transform depart = window.Find("Button_Start run") ?? throw new InvalidOperationException("Window has no Button_Start run.");
            if (unlocks.parent != window || unlocks.GetSiblingIndex() > depart.GetSiblingIndex())
            {
                unlocks.SetParent(window, false);
                unlocks.SetSiblingIndex(depart.GetSiblingIndex());
            }
            Caption(depart, "출정");

            Transform back = window.Find("Button_Cancel") ?? throw new InvalidOperationException("Window has no Button_Cancel.");
            Caption(back, "뒤로");
            var backButton = back.GetComponent<Button>();
            for (int i = backButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(backButton.onClick, i);
            UnityEventTools.AddPersistentListener(backButton.onClick, screen.CloseExpedition);
            EditorUtility.SetDirty(backButton);

            var dim = overlay.GetComponent<Image>();
            if (dim != null) { dim.color = Color.clear; EditorUtility.SetDirty(dim); }
            var windowImage = window.GetComponent<Image>();
            var panelImage = panel.GetComponent<Image>();
            if (windowImage != null && panelImage != null) { windowImage.color = panelImage.color; EditorUtility.SetDirty(windowImage); }
            Transform unlockOverlay = screenRoot.Find("UnlockOverlay");
            if (unlockOverlay != null && unlockOverlay.GetSiblingIndex() < overlay.GetSiblingIndex())
                unlockOverlay.SetSiblingIndex(overlay.GetSiblingIndex());
        }

        static void Caption(Transform target, string text)
        {
            if (target == null) throw new InvalidOperationException("Title is missing a caption target for " + text);
            var label = target.GetComponent<TMP_Text>() ?? target.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            EditorUtility.SetDirty(label);
        }

        // Same look and layout as the main Panel column, wider for the slot summaries, with a header label.
        static GameObject SlotPanel(Transform screenRoot, Transform panel)
        {
            Transform existing = screenRoot.Find("SlotPanel");
            if (existing != null) return existing.gameObject;

            var slotPanel = new GameObject("SlotPanel", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            slotPanel.transform.SetParent(screenRoot, false);
            slotPanel.transform.SetSiblingIndex(panel.GetSiblingIndex() + 1);
            EditorUtility.CopySerialized(panel.GetComponent<RectTransform>(), slotPanel.GetComponent<RectTransform>());
            EditorUtility.CopySerialized(panel.GetComponent<Image>(), slotPanel.GetComponent<Image>());
            EditorUtility.CopySerialized(panel.GetComponent<VerticalLayoutGroup>(), slotPanel.GetComponent<VerticalLayoutGroup>());
            EditorUtility.CopySerialized(panel.GetComponent<ContentSizeFitter>(), slotPanel.GetComponent<ContentSizeFitter>());
            var rect = (RectTransform)slotPanel.transform;
            rect.sizeDelta = new Vector2(SlotPanelWidth, rect.sizeDelta.y);

            Transform title = panel.Find("Label") ?? throw new InvalidOperationException("Title has no logo Label.");
            GameObject header = UnityEngine.Object.Instantiate(title.gameObject, slotPanel.transform, false);
            header.name = "Label_SlotsHeader";
            var label = header.GetComponent<TMP_Text>();
            label.text = "슬롯 선택";
            label.fontSize = Mathf.Round(label.fontSize * 0.6f);
            var layout = header.GetComponent<LayoutElement>();
            if (layout != null) layout.preferredHeight = Mathf.Round(layout.preferredHeight * 0.7f);
            header.transform.SetSiblingIndex(0);
            return slotPanel;
        }
    }
}
