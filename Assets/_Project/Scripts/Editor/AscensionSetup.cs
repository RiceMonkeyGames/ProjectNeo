using System;
using ProjectNeo.Run;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// FLOW-03 승천 단계: 단계 표 에셋, RunStart 연결, 타이틀의 단계 선택 창, 게임 HUD의 'Finish run' 버튼을 Unity API로 만든다.
    /// 다시 실행하면 같은 것을 새로 만든다. 수치는 Q-78(2026-11-20 사용자 승인)을 따른다.
    /// </summary>
    public static class AscensionSetup
    {
        public const string TablePath = "Assets/_Project/Data/GameFlow/Ascension.asset";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("ProjectNeo/Setup/FLOW-03 Ascension (table, title picker, finish button)")]
        public static void ApplyAll()
        {
            CreateTable();
            ApplyTitlePicker();
            ApplyFinishRunButton();
        }

        /// <summary>승인된 10단계 규칙으로 표 에셋을 만들거나 덮어쓰고 RunStart.asset에 연결한다.</summary>
        public static AscensionTable CreateTable()
        {
            AscensionTable table = AssetDatabase.LoadAssetAtPath<AscensionTable>(TablePath);
            AscensionTable fresh = AscensionTable.Create(0.1f,
                Level("Enemy HP +15%", Rule(AscensionRuleKind.EnemyHealthPercent, 0.15f)),
                Level("Starting gold -25%", Rule(AscensionRuleKind.StartingGoldPercent, -0.25f)),
                Level("Enemy attack +15%", Rule(AscensionRuleKind.EnemyAttackPercent, 0.15f)),
                Level("Preparation time 12h -> 10h", Rule(AscensionRuleKind.PrepBudgetHours, -2f)),
                Level("Victory gold -20%", Rule(AscensionRuleKind.VictoryGoldPercent, -0.2f)),
                Level("Enemies per wave +20%", Rule(AscensionRuleKind.EnemyCountPercent, 0.2f)),
                Level("Squad starting morale 150 -> 120", Rule(AscensionRuleKind.StartingMorale, -30f)),
                Level("Boss HP +30%, attack +20%", Rule(AscensionRuleKind.BossHealthPercent, 0.3f), Rule(AscensionRuleKind.BossAttackPercent, 0.2f)),
                Level("Wall repair time x2", Rule(AscensionRuleKind.RepairTimePercent, 1f)),
                Level("Enemy move speed +15%", Rule(AscensionRuleKind.EnemyMoveSpeedPercent, 0.15f)));
            if (table == null)
            {
                fresh.name = "Ascension";
                AssetDatabase.CreateAsset(fresh, TablePath);
                table = fresh;
            }
            else
            {
                EditorUtility.CopySerialized(fresh, table);
                table.name = "Ascension";
                UnityEngine.Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(table);
            }

            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("RunStart is missing: " + StartPath);
            var data = new SerializedObject(start);
            data.FindProperty("ascension").objectReferenceValue = table;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(start);
            AssetDatabase.SaveAssets();
            return table;
        }

        /// <summary>Title 씬에 단계 선택 창(이전·다음·시작·취소)을 더하고 TitleScreen에 연결한다.</summary>
        public static void ApplyTitlePicker()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            AscensionTable table = AssetDatabase.LoadAssetAtPath<AscensionTable>(TablePath)
                ?? throw new InvalidOperationException("Ascension table is missing: " + TablePath);
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.TitlePath, OpenSceneMode.Single);
            TitleScreen screen = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                screen = screen != null ? screen : root.GetComponentInChildren<TitleScreen>(true);
            if (screen == null) throw new InvalidOperationException("Title scene has no TitleScreen.");

            Transform old = screen.transform.Find("AscensionOverlay");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            AscensionPicker picker = screen.GetComponent<AscensionPicker>();
            if (picker == null) picker = screen.gameObject.AddComponent<AscensionPicker>();

            // 타이틀 전체를 덮는 어두운 배경 + 가운데 창.
            var overlay = new GameObject("AscensionOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(screen.transform, false);
            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.85f);

            var window = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            window.transform.SetParent(overlay.transform, false);
            var windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(520f, 0f);
            window.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.12f, 0.97f);
            var layout = window.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20); layout.spacing = 10f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            window.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text title = Label(window.transform, "Choose Ascension", 36f, 24f);
            title.name = "Label_AscensionTitle";
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;

            var row = new GameObject("LevelRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(window.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 40f;
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f; rowLayout.childControlWidth = true; rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = true;
            GameObject previous = ConstructionPrototypeSetup.Button(row.transform, "<", picker.Previous);
            previous.GetComponent<LayoutElement>().preferredWidth = 56f;
            TMP_Text level = Label(row.transform, "Ascension 0", 40f, 20f);
            level.name = "Label_AscensionLevel";
            level.alignment = TextAlignmentOptions.Center;
            level.GetComponent<LayoutElement>().flexibleWidth = 1f;
            GameObject next = ConstructionPrototypeSetup.Button(row.transform, ">", picker.Next);
            next.GetComponent<LayoutElement>().preferredWidth = 56f;

            TMP_Text rules = Label(window.transform, string.Empty, 250f, 16f);
            rules.name = "Label_AscensionRules";
            rules.textWrappingMode = TextWrappingModes.Normal;
            rules.alignment = TextAlignmentOptions.TopLeft;

            ConstructionPrototypeSetup.Button(window.transform, "Start run", picker.StartSelected);
            ConstructionPrototypeSetup.Button(window.transform, "Cancel", picker.Close);

            picker.Configure(table, overlay, level, rules);
            overlay.SetActive(false);
            screen.ConfigureAscension(picker);
            EditorUtility.SetDirty(picker);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Game 씬의 흐름 HUD에 'Finish run' 버튼을 다음 스테이지 버튼 바로 뒤에 더하고 연결한다.</summary>
        public static void ApplyFinishRunButton()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            GameFlowHud hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                hud = hud != null ? hud : root.GetComponentInChildren<GameFlowHud>(true);
            if (hud == null) throw new InvalidOperationException("Game scene has no GameFlowHud.");
            var data = new SerializedObject(hud);
            var nextStage = data.FindProperty("nextStageButton").objectReferenceValue as GameObject
                ?? throw new InvalidOperationException("GameFlowHud has no next stage button.");
            Transform panel = nextStage.transform.parent;
            Transform old = panel.Find("Button_Finish run");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            GameObject finish = ConstructionPrototypeSetup.Button(panel, "Finish run", hud.FinishRun);
            finish.transform.SetSiblingIndex(nextStage.transform.GetSiblingIndex() + 1);
            var nextLayout = nextStage.GetComponent<LayoutElement>();
            if (nextLayout != null) finish.GetComponent<LayoutElement>().preferredHeight = nextLayout.preferredHeight;
            finish.SetActive(false);
            hud.ConfigureFinishRun(finish);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static AscensionLevel Level(string description, params AscensionRule[] rules) => new AscensionLevel(description, rules);
        static AscensionRule Rule(AscensionRuleKind kind, float value) => new AscensionRule(kind, value);

        static TMP_Text Label(Transform parent, string text, float height, float size)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredHeight = height;
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size;
            label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return label;
        }
    }
}
