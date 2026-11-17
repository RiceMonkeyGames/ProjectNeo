using System;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    public static class PlayerHealthHudSetup
    {
        [MenuItem("ProjectNeo/Setup/Add Player HP HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");
            var scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            var view = UnityEngine.Object.FindAnyObjectByType<GamePhaseView>();
            if (hud == null || player == null || view == null)
                throw new InvalidOperationException("Game HUD, player and phase view are required.");

            var controller = hud.GetComponent<PlayerHealthHud>();
            if (controller == null) controller = hud.gameObject.AddComponent<PlayerHealthHud>();
            RectTransform panel = Rect(hud.transform, "PlayerHealth", new Vector2(1f, 0f), new Vector2(1f, 0f));
            panel.pivot = new Vector2(1f, 0f);
            // 하단 준비 조작 안내(높이 96 + 여백 16) 바로 위를 비워 둔다.
            panel.anchoredPosition = new Vector2(-24f, 128f);
            panel.sizeDelta = new Vector2(240f, 64f);
            Image(panel, new Color(0.035f, 0.045f, 0.06f, 0.92f));

            RectTransform labelRect = Rect(panel, "Value", Vector2.zero, Vector2.one);
            labelRect.offsetMin = new Vector2(12f, 25f);
            labelRect.offsetMax = new Vector2(-12f, -5f);
            var label = labelRect.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            var source = hud.GetComponentInChildren<TMP_Text>(true);
            label.font = source.font;
            label.fontSize = 22f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.raycastTarget = false;
            label.text = "HP -- / --";

            RectTransform track = Rect(panel, "Track", Vector2.zero, new Vector2(1f, 0f));
            track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(0f, 10f);
            track.sizeDelta = new Vector2(-24f, 12f);
            Image(track, new Color(0.2f, 0.08f, 0.09f, 1f));
            RectTransform fill = Rect(track, "Fill", Vector2.zero, Vector2.one);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Image(fill, new Color(0.85f, 0.16f, 0.2f, 1f));

            controller.Configure(player.GetComponent<CharacterActor>(), view, panel.gameObject, label, fill);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var existing = parent.Find(name);
            var rect = existing != null ? (RectTransform)existing
                : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            return rect;
        }

        static void Image(RectTransform rect, Color color)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
