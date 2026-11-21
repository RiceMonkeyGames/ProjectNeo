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
    /// UI-10: restyles the first Title screen (the main menu <c>Panel</c>: logo, start, quit) with Synty Interface Fantasy
    /// Warrior HUD art, laid out like the pack's <c>00_Demo_FantasyWarrior_Title</c> sample: full-screen backdrop with a
    /// tracery frame, a banner plate logo wrapped by a dragon, a parchment start button and a small quit link in the
    /// bottom-right corner. Korean text (user, 2026-11-21) in Noto Sans KR, since the pack's Grenze font has no Hangul. Object names and button
    /// events stay the same. Re-runnable.
    /// </summary>
    public static class TitleMainMenuStyleSetup
    {
        [MenuItem("ProjectNeo/Setup/Restyle Title Main Menu (UI-10)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for an idle Editor before changing the Title.");
            TMP_FontAsset font = Noto;
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = TitleSlotWindowStyleSetup.FindScreen(scene);
            var panel = (RectTransform)(screen.transform.Find("Panel") ?? throw new InvalidOperationException("Title has no Panel."));
            var logo = (RectTransform)(panel.Find("Label") ?? throw new InvalidOperationException("Title has no logo Label."));
            var start = (RectTransform)(panel.Find("Button_Start") ?? throw new InvalidOperationException("Title has no Button_Start."));
            var quit = (RectTransform)(panel.Find("Button_Quit") ?? throw new InvalidOperationException("Title has no Button_Quit."));

            FullScreen(panel);
            RectTransform visual = FreshVisual(panel);
            Backdrop(visual);
            RectTransform plate = LogoPlate(visual);
            StyleLogo(logo, plate, font);

            Place(start, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(840f, 192f) * Scale);
            TMP_Text startLabel = ParchmentButton(start, "게임 시작", font, 28f);
            startLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);
            var arrow = Image(start, "ICON", "SPR_HUD_FantasyWarrior_Arrow02", Ink, 1f);
            arrow.preserveAspect = true;
            Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(-46f, 2f), new Vector2(20f, 20f));

            // Inside the frame like the sample (its Bottom bar lifts the link above the frame line).
            Place(quit, new Vector2(1f, 0f), new Vector2(-80f, 72f), new Vector2(120f, 28f), new Vector2(1f, 0f));
            TMP_Text quitLabel = TextButton(quit, "게임종료", font, 20f, Gold);

            logo.SetSiblingIndex(1);
            start.SetSiblingIndex(2);
            quit.SetSiblingIndex(3);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            foreach (TMP_Text label in new[] { logo.GetComponent<TMP_Text>(), startLabel, quitLabel }) RequireFont(label, font);
            return "Title main menu restyled";
        }

        // Sample "Pack_Title/Interface": teal plate in a Banner13 frame with a dragon coiled around it, enlarged 1.5x
        // because the game has no second branding line under it.
        static RectTransform LogoPlate(RectTransform visual)
        {
            var group = Node(visual, "Logo");
            Place(group, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 180f) * Scale);
            group.localScale = Vector3.one * 1.5f;

            Color stone = new Color32(0xEE, 0xEE, 0xEE, 0xFF);
            var dragon = Node(group, "Dragon");
            Stretch(dragon, new Vector2(-80f, -85f - 48f) * Scale, new Vector2(80f, 85f - 48f) * Scale);
            var top = Image(dragon, "SPR_Greeble_DragonBody_Top", "SPR_FantasyWarrior_Greeble_DragonBodyTop01", stone, 2f);
            top.preserveAspect = true;
            Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(120f, 0f) * Scale, new Vector2(500f, 250f) * Scale, new Vector2(0f, 0.5f));
            var bottom = Image(dragon, "SPR_Greeble_DragonBody_Bottom", "SPR_FantasyWarrior_Greeble_DragonBodyBottom01", stone, 2f);
            bottom.preserveAspect = true;
            Place(bottom.rectTransform, new Vector2(1f, 0f), new Vector2(-85f, 210f) * Scale, new Vector2(500f, 250f) * Scale, new Vector2(1f, 1f));
            var tail = Image(dragon, "SPR_Greeble_DragonTail", "SPR_FantasyWarrior_Greeble_DragonTail01", stone, 2f);
            tail.preserveAspect = true;
            Place(tail.rectTransform, new Vector2(0f, 0f), new Vector2(168f, 231f) * Scale, new Vector2(380f, 380f) * Scale, new Vector2(1f, 0.5f));

            var plate = Image(group, "SPR_Background", "SPR_FantasyWarrior_Branding_Background01", Color.white, 1.5f * 3f);
            Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            var frame = Image(group, "SPR_Frame", "SPR_FantasyWarrior_Banner13", new Color32(0x78, 0x9B, 0xA6, 0xFF), 2.4f * 3f);
            Stretch(frame.rectTransform, new Vector2(-60f, -10f) * Scale, new Vector2(60f, 10f) * Scale);
            return group;
        }

        // The existing logo Label is laid over the plate (same rect and scale) so other setups can still find it.
        static void StyleLogo(RectTransform logo, RectTransform plate, TMP_FontAsset font)
        {
            DestroyComponent<UnityEngine.UI.LayoutElement>(logo.gameObject);
            Place(logo, new Vector2(0.5f, 0.5f), plate.anchoredPosition, plate.sizeDelta * plate.localScale.x);
            var label = logo.GetComponent<TMP_Text>();
            label.text = "프로젝트 네오";
            Font(label, font, 46f, Ink, TextAlignmentOptions.Center);
            EditorUtility.SetDirty(label);
        }
    }
}
