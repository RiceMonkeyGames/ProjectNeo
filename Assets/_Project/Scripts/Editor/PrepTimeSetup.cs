using System;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// PREP-06 준비 시간 데이터와 화면을 연결한다. 시간 수치 에셋을 만들어 RunStart에 잇고(이미 있으면 그대로),
    /// Game 씬 위 가운데 단계 표시 상자 안 단계 글자 바로 아래에 시계 한 줄을 넣는다. 다시 실행해도 같은 결과다.
    /// </summary>
    public static class PrepTimeSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/PrepTime.asset";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        const string LabelName = "PrepClock";
        // 기준 해상도(1280×720) 기준. 단계 상자는 원래 자리(위 16)에 그대로 두고, 시계 줄은 화면 맨 위에서 단계 상자의 위 여백(12)
        // 일부와 겹쳐 붙는다. 단계 글자(위 28부터)와는 겹치지 않는다. 시설 창 위쪽(위 약 70)에도 닿지 않는다.
        const float ClockTop = 0f, ClockHeight = 22f, PanelTop = 16f;

        [MenuItem("ProjectNeo/Setup/Configure Preparation Time (PREP-06)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");

            RunConfig start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("Missing RunStart: " + StartPath);
            var settings = AssetDatabase.LoadAssetAtPath<PrepTimeSettings>(SettingsPath);
            if (settings == null)
            {
                settings = PrepTimeSettings.Create();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var startData = new SerializedObject(start);
            SerializedProperty reference = startData.FindProperty("prepTime");
            if (reference.objectReferenceValue == null)
            {
                reference.objectReferenceValue = settings;
                startData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(start);
            }
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>();
            var battle = UnityEngine.Object.FindAnyObjectByType<GameBattleController>();
            if (hud == null || battle == null) throw new InvalidOperationException("Game HUD and battle controller are required.");
            var panel = (RectTransform)(hud.transform.Find("Panel") ?? throw new InvalidOperationException("Game HUD top Panel is missing."));
            Transform phase = panel.Find("Label") ?? throw new InvalidOperationException("Game HUD phase Label is missing.");
            var phaseText = phase.GetComponent<TMP_Text>();
            var panelImage = panel.GetComponent<UnityEngine.UI.Image>();

            // 예전 실행이 단계 상자 안에 넣은 줄은 지운다(시설 창 위쪽에 가려졌다).
            Transform old = panel.Find(LabelName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            // 시계 줄: 단계 상자와 같은 폭으로 화면 맨 위에 두고, 시설 창보다 위에 그리도록 마지막 자식으로 둔다.
            Transform existing = hud.transform.Find(LabelName);
            var line = existing != null ? (RectTransform)existing : new GameObject(LabelName, typeof(RectTransform)).GetComponent<RectTransform>();
            line.SetParent(hud.transform, false);
            line.SetAsLastSibling();
            line.anchorMin = new Vector2(panel.anchorMin.x, 1f);
            line.anchorMax = new Vector2(panel.anchorMax.x, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.anchoredPosition = new Vector2(0f, -ClockTop);
            line.sizeDelta = new Vector2(panel.sizeDelta.x, ClockHeight);
            var backing = line.GetComponent<UnityEngine.UI.Image>();
            if (backing == null) backing = line.gameObject.AddComponent<UnityEngine.UI.Image>();
            backing.color = panelImage != null ? panelImage.color : new Color(0.06f, 0.09f, 0.12f, 0.85f);
            backing.raycastTarget = false;

            Transform textTransform = line.Find("Text");
            var textRect = textTransform != null ? (RectTransform)textTransform : new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            textRect.SetParent(line, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
            var label = textRect.GetComponent<TextMeshProUGUI>();
            if (label == null) label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = phaseText.font;
            label.fontSize = 17f;
            label.color = new Color(1f, 0.86f, 0.55f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = "Prep time -- / --";
            line.gameObject.SetActive(false);

            // 단계 상자는 원래 자리. 이 메뉴의 예전 실행이 내렸으면 되돌린다.
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, -PanelTop);

            var controller = hud.GetComponent<PrepClockHud>();
            if (controller == null) controller = hud.gameObject.AddComponent<PrepClockHud>();
            // 부대 편성 창은 거의 화면 전체라 시계 줄이 제목 상자에 닿는다. 시간을 쓰지 않는 창이라 열려 있는 동안 시계를 숨긴다.
            Transform organization = hud.transform.Find("OrganizationPanel");
            controller.Configure(battle, line.gameObject, label, organization != null ? organization.gameObject : null);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
