using System;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// PREP-09(Q-57): Game 씬에 최종 부대 상한(10부대 × 잡졸 20명, 대장은 따로)과 부대 선택 키 F1~F10을 적용한다.
    /// 성벽 지휘·부대 지휘 시험 씬이 함께 쓰는 공용 에셋은 그대로 두고, Game 전용 에셋 두 개를 만들어 씬 지휘 본부에 연결한다.
    /// 왼쪽 위 부대 목록을 10줄 높이로 늘리고 그 아래 적 목록 창을 내리며, 편성 창 카드 영역을 세로로도 스크롤하게 한다.
    /// 여러 번 실행해도 결과가 같다(에셋은 값만 다시 맞춘다).
    /// </summary>
    public static class SquadCapsSetup
    {
        public const string ConfigPath = "Assets/_Project/Data/GameFlow/SquadConfig.asset";
        public const string CommandPath = "Assets/_Project/Data/GameFlow/Command.asset";
        const string CommandSource = "Assets/_Project/Data/WallCommandPrototype/Command.asset";
        public const int MaxSquads = 10;
        public const int MaxSoldiersPerSquad = 20;

        [MenuItem("ProjectNeo/Setup/Apply Final Squad Caps (PREP-09)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            EnsureConfig();
            EnsureCommand();
            // 처음 만든 에셋을 저장하고 경로에서 다시 읽어 연결한다. 메모리의 새 객체를 바로 연결하면 첫 실행에서 씬 참조가 비는 일이 있었다.
            AssetDatabase.SaveAssets();
            var config = AssetDatabase.LoadAssetAtPath<SquadConfig>(ConfigPath);
            var command = AssetDatabase.LoadAssetAtPath<SquadCommandSettings>(CommandPath);

            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var center = GameFacilitySetup.Find<SquadCommandCenter>(scene) ?? throw new InvalidOperationException("SquadCommandCenter is missing.");
            var data = new SerializedObject(center);
            data.FindProperty("config").objectReferenceValue = config;
            data.FindProperty("settings").objectReferenceValue = command;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(center);

            ApplySquadList(GameFacilitySetup.Find<SquadCommandHud>(scene));
            var hud = GameFacilitySetup.Find<GameFlowHud>(scene);
            var intel = hud != null ? hud.transform.Find("EnemyIntel") as RectTransform : null;
            if (intel != null) { intel.anchoredPosition = EnemyIntelHudSetup.Offset; EditorUtility.SetDirty(intel); }
            Transform organization = hud != null ? hud.transform.Find("OrganizationPanel") : null;
            if (organization == null) throw new InvalidOperationException("OrganizationPanel is missing. Run Connect Squad Organization first.");
            SquadOrganizationSetup.ApplyScrolling(organization);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static SquadConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SquadConfig>(ConfigPath);
            if (config == null)
            {
                config = SquadConfig.Create(MaxSquads, MaxSoldiersPerSquad);
                AssetDatabase.CreateAsset(config, ConfigPath);
                return config;
            }
            var data = new SerializedObject(config);
            data.FindProperty("maxSquads").intValue = MaxSquads;
            data.FindProperty("maxMembersPerSquad").intValue = MaxSoldiersPerSquad;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        // 성벽 지휘 시험과 같은 대형·지시 키를 복사하고 부대 선택 키만 F1~F10으로 늘린다.
        static SquadCommandSettings EnsureCommand()
        {
            var command = AssetDatabase.LoadAssetAtPath<SquadCommandSettings>(CommandPath);
            if (command == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<SquadCommandSettings>(CommandSource)
                    ?? throw new InvalidOperationException("Missing " + CommandSource);
                command = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(command, CommandPath);
            }
            var keys = new string[MaxSquads];
            for (int i = 0; i < keys.Length; i++) keys[i] = "<Keyboard>/f" + (i + 1);
            command.ConfigureSelectKeys(keys);
            EditorUtility.SetDirty(command);
            return command;
        }

        static void ApplySquadList(SquadCommandHud commandHud)
        {
            if (commandHud == null) throw new InvalidOperationException("SquadCommandHud is missing.");
            var data = new SerializedObject(commandHud);
            var squads = (TMP_Text)data.FindProperty("squads").objectReferenceValue;
            var help = (TMP_Text)data.FindProperty("help").objectReferenceValue;
            RectTransform rect = squads.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, GameBattleSetup.SquadListHeight);
            var backing = commandHud.transform.Find(squads.name + "Backing") as RectTransform;
            if (backing != null)
            {
                backing.sizeDelta = rect.sizeDelta + new Vector2(16f, 16f);
                EditorUtility.SetDirty(backing);
            }
            foreach (string old in new[] { "F1-F3", "F1-F4", "F1~F3", "F1~F4" })
                help.text = help.text.Replace(old, "F1-F10");
            EditorUtility.SetDirty(squads); EditorUtility.SetDirty(help);
        }
    }
}
