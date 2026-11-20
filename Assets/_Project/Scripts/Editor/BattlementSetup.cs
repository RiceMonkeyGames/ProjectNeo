using System;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using ProjectNeo.Walls;
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
    /// SIEGE-10 성가퀴. 치수 에셋을 만들고, 방어 시설을 2레벨(1: 낮은 난간, 2: 성가퀴)로 맞추고,
    /// Game 씬 성에 자동 성가퀴와 방어 시설 레벨 연결을 붙이고, 건설 모드 패널에 성가퀴 업그레이드 버튼을 둔다.
    /// 건설소 건물의 F는 건설 모드 그대로다(방어 시설 상호작용을 겹쳐 붙이지 않는다).
    /// 여러 번 실행해도 컴포넌트를 중복으로 만들지 않는다.
    /// </summary>
    public static class BattlementSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/Build/Battlements.asset";
        const string DefensePath = "Assets/_Project/Data/GameFlow/Facilities/Facility_Defense.asset";
        const string StonePath = "Assets/_Project/Art/Placeholder/ConstructionStone.mat";
        const string BuildingPath = "Geometry/CourtyardLayout/Construction";

        [MenuItem("ProjectNeo/Setup/Add Battlements To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            BattlementSettings settings = CreateSettings();
            ConfigureDefenseLevels();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            settings = AssetDatabase.LoadAssetAtPath<BattlementSettings>(SettingsPath);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene);
            var castle = world.GetComponent<CastleBattlements>();
            if (castle == null) castle = world.gameObject.AddComponent<CastleBattlements>();
            castle.Configure(world, settings, BattlementStyle.Parapet);
            EditorUtility.SetDirty(castle);

            GameBattleController battle = GameFacilitySetup.Find<GameBattleController>(scene);
            var link = battle.GetComponent<GameBattlements>();
            if (link == null) link = battle.gameObject.AddComponent<GameBattlements>();
            link.Configure(battle, castle, BattlementStyle.Parapet);
            ConnectUpgradeButton(scene, battle, link);
            EditorUtility.SetDirty(link);

            // 건설소 건물은 이미 건설 모드 상호작용(ConstructionStation)을 가진다. 방어 시설 상호작용을 겹쳐 붙이지 않는다.
            Transform building = world.transform.Find(BuildingPath);
            var stray = building != null ? building.GetComponent<FacilityStation>() : null;
            if (stray != null) UnityEngine.Object.DestroyImmediate(stray, true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>건설 모드 패널(건설소에서 들어가는 화면)의 수리 버튼 아래에 성가퀴 업그레이드 버튼을 둔다(사용자 결정 2026-11-20).</summary>
        static void ConnectUpgradeButton(Scene scene, GameBattleController battle, GameBattlements link)
        {
            ConstructionHud hud = GameFacilitySetup.Find<ConstructionHud>(scene);
            Transform panel = hud.transform.Find("BuildPanel")
                ?? throw new InvalidOperationException("ConstructionHud has no BuildPanel.");
            Transform root = panel.Find("Button_Battlements");
            if (root == null) root = ConstructionPrototypeSetup.Button(panel, "Battlements", link.TryUpgradeFromButton).transform;
            else
            {
                var existing = root.GetComponent<Button>();
                while (existing.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(existing.onClick, 0);
                UnityEventTools.AddPersistentListener(existing.onClick, link.TryUpgradeFromButton);
            }
            Transform repair = panel.Find("Button_Repair all");
            if (repair != null) root.SetSiblingIndex(repair.GetSiblingIndex() + 1);
            link.ConfigureUpgrade(battle.GetComponent<GameFacilities>()
                    ?? throw new InvalidOperationException("Run Connect Facilities To Game Scene first."),
                root.GetComponent<Button>(), root.GetComponentInChildren<TMP_Text>(true));
        }

        static BattlementSettings CreateSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BattlementSettings>(SettingsPath);
            if (existing != null) return existing;
            // 임시 값(SIEGE-10, 2026-11-20). 난간 0.8m는 유닛 조준점(키 2m의 절반)보다 낮아 상체가 드러난다.
            var created = BattlementSettings.Create(material: AssetDatabase.LoadAssetAtPath<Material>(StonePath));
            AssetDatabase.CreateAsset(created, SettingsPath);
            return created;
        }

        /// <summary>방어 시설을 2레벨로 맞추고 레벨마다 성가퀴 모양을 적는다. 다른 칸 값과 2레벨 비용은 그대로 둔다.</summary>
        static void ConfigureDefenseLevels()
        {
            var defense = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(DefensePath)
                ?? throw new InvalidOperationException("Run Connect Facilities To Game Scene first: " + DefensePath);
            var serialized = new SerializedObject(defense);
            SerializedProperty levels = serialized.FindProperty("levels");
            if (levels.arraySize < 2) throw new InvalidOperationException("Defense facility needs at least two levels.");
            levels.arraySize = 2;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("battlementStyle").enumValueIndex = (int)BattlementStyle.Parapet;
            levels.GetArrayElementAtIndex(1).FindPropertyRelative("battlementStyle").enumValueIndex = (int)BattlementStyle.Crenellated;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(defense);
        }
    }
}
