using System;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// C-5 적 목록 창을 Game 씬 HUD 왼쪽(부대 목록 아래)에 넣는다. 제목 한 줄과 종류별 줄들이며, 줄 수에 맞춰 높이가 늘어난다.
    /// 다시 실행해도 같은 결과다.
    /// </summary>
    public static class EnemyIntelHudSetup
    {
        const string PanelName = "EnemyIntel";
        // 기준 해상도(1280×720) 기준 왼쪽 여백과 폭. 왼쪽 위 부대 목록(SquadCommandHUD/SquadsBacking, 위 8 + 높이 252 = 260, PREP-09)
        // 바로 아래에 둔다. 왼쪽 가운데의 건설 도구 상자는 건설 화면에서만 보이고, 그때 이 창은 숨는다.
        internal static readonly Vector2 Offset = new Vector2(16f, -272f);
        const float Width = 240f;

        [MenuItem("ProjectNeo/Setup/Add Enemy Intel HUD (C-5)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");

            var scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            var battle = UnityEngine.Object.FindAnyObjectByType<GameBattleController>();
            var view = UnityEngine.Object.FindAnyObjectByType<GamePhaseView>();
            var interaction = UnityEngine.Object.FindAnyObjectByType<PreparationInteraction>(FindObjectsInactive.Include);
            if (hud == null || battle == null || view == null || interaction == null)
                throw new InvalidOperationException("Game HUD, battle controller, phase view and preparation interaction are required.");
            var font = hud.GetComponentInChildren<TMP_Text>(true).font;

            Transform existing = hud.transform.Find(PanelName);
            var panel = existing != null ? (RectTransform)existing
                : new GameObject(PanelName, typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(hud.transform, false);
            // 준비 시계 줄(PREP-06)은 맨 마지막에 그려야 하므로 그 바로 앞에 둔다.
            Transform clock = hud.transform.Find("PrepClock");
            // 다시 실행해도 순서가 같도록 창을 맨 뒤로 보낸 다음 시계를 그 뒤로 보낸다.
            panel.SetAsLastSibling();
            if (clock != null) clock.SetAsLastSibling();
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = Offset;
            panel.sizeDelta = new Vector2(Width, 0f);
            var image = Get<Image>(panel.gameObject);
            image.color = new Color(0.035f, 0.045f, 0.06f, 0.88f);
            image.raycastTarget = false;
            var layout = Get<VerticalLayoutGroup>(panel.gameObject);
            layout.padding = new RectOffset(12, 12, 8, 10);
            layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = Get<ContentSizeFitter>(panel.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            TextMeshProUGUI title = Text(panel, "Title", font, 18f, new Color(1f, 0.86f, 0.55f, 1f), EnemyIntelHud.PreviewTitle);
            TextMeshProUGUI body = Text(panel, "Rows", font, 17f, Color.white, "? × 0");
            panel.gameObject.SetActive(false);

            var controller = hud.GetComponent<EnemyIntelHud>();
            if (controller == null) controller = hud.gameObject.AddComponent<EnemyIntelHud>();
            controller.Configure(battle, view, interaction, panel.gameObject, title, body);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static T Get<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color, string text)
        {
            Transform existing = parent.Find(name);
            var rect = existing != null ? (RectTransform)existing
                : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var label = Get<TextMeshProUGUI>(rect.gameObject);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
    }
}
