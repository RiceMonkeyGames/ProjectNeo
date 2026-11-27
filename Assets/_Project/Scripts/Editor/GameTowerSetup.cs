using System;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-24: 망루를 Game에 넣는다. 여러 번 실행해도 결과가 같다(있는 에셋은 그 자리에서 고쳐 GUID 유지).
    /// 망루 조각 에셋(2×2, 층마다 5m, 처음 2층, 지붕 무기 자리 넷, 지붕 위 아군 원거리 사거리·공격력 +20%)을 만들고,
    /// 건설 비용 표(층마다 60골드)와 저장 콘텐츠 목록에 넣고, Game 씬 건설 화면의 망루 칸과 지붕 효과를 단다.
    /// 성벽 줄 안 배치·성벽 바꿔 끼우기는 런타임 규칙(<see cref="TowerRules"/>)이 맡는다.
    /// </summary>
    public static class GameTowerSetup
    {
        public const string TowerPath = ConstructionPrototypeSetup.Data + "/Tower.asset";
        const string CostsPath = "Assets/_Project/Data/GameFlow/Build/BuildCosts.asset";
        // 임시 값(사용자 결정 2026-11-23·27). 플레이 시험 뒤 에셋에서 조정한다.
        public const float Height = Grid75Migration.Storey;
        public const float MaxHealth = 400f;
        public const int Gold = 60;
        public const int InitialTiers = 2;
        const float RangedBonus = 0.2f;
        // 지붕 무기 자리: 망루 가운데에서 ±1.3m(정규화 0.13, SIEGE-14 시험 씬과 같다). 계단 위 지붕 구멍에 대포가 걸치지 않는다.
        const float Mount = 0.13f;

        [MenuItem("ProjectNeo/Setup/Apply Towers To Game (SIEGE-24)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            BuildPieceDefinition tower = CreateOrUpdateTower();
            AddCost(tower);
            AssetDatabase.SaveAssets();
            RunSaveSetup.Apply();
            WireGameScene();
            AssetDatabase.SaveAssets();
            Debug.Log("SIEGE-24 towers applied to the Game scene.");
        }

        /// <summary>망루 조각 에셋을 만들거나 값을 맞춘다.</summary>
        public static BuildPieceDefinition CreateOrUpdateTower()
        {
            CombatPrototypeSetup.EnsureFolder(ConstructionPrototypeSetup.Data);
            Material stone = CombatPrototypeSetup.MaterialAsset("ConstructionTower", new Color(0.62f, 0.58f, 0.5f));
            GameObject prefab = ConstructionPrototypeSetup.PiecePrefab("Tower", BuildPieceKind.Tower, stone);
            var fresh = BuildPieceDefinition.Create(BuildPieceKind.Tower, "망루", new Vector2Int(2, 2), Height, MaxHealth, prefab,
                new BuildMountPoint("roof_sw", new Vector3(-Mount, 1f, -Mount)), new BuildMountPoint("roof_se", new Vector3(Mount, 1f, -Mount)),
                new BuildMountPoint("roof_nw", new Vector3(-Mount, 1f, Mount)), new BuildMountPoint("roof_ne", new Vector3(Mount, 1f, Mount)));
            fresh.SetOccupantModifiers(StatModifier.Percent(StatType.AttackRange, RangedBonus), StatModifier.Percent(StatType.Attack, RangedBonus));
            fresh.SetInitialTiers(InitialTiers);
            fresh.name = "Tower";
            var existing = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(TowerPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, TowerPath);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            UnityEngine.Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void AddCost(BuildPieceDefinition tower)
        {
            var costs = AssetDatabase.LoadAssetAtPath<BuildCostTable>(CostsPath);
            if (costs == null) { Debug.LogWarning("Build cost table is missing: " + CostsPath); return; }
            var data = new SerializedObject(costs);
            SerializedProperty pieces = data.FindProperty("pieces");
            SerializedProperty entry = null;
            for (int i = 0; i < pieces.arraySize; i++)
                if (pieces.GetArrayElementAtIndex(i).FindPropertyRelative("piece").objectReferenceValue == tower)
                    entry = pieces.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                pieces.InsertArrayElementAtIndex(pieces.arraySize);
                entry = pieces.GetArrayElementAtIndex(pieces.arraySize - 1);
                entry.FindPropertyRelative("piece").objectReferenceValue = tower;
            }
            entry.FindPropertyRelative("gold").intValue = Gold;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(costs);
        }

        static void WireGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            // 씬을 연 뒤에는 에셋을 경로로 다시 불러 잇는다.
            var tower = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(TowerPath);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ConstructionHud hud in root.GetComponentsInChildren<ConstructionHud>(true))
                {
                    var data = new SerializedObject(hud);
                    data.FindProperty("tower").objectReferenceValue = tower;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(hud);
                }
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene);
            var buff = world.GetComponent<TowerOccupantBuff>();
            if (buff == null) buff = world.gameObject.AddComponent<TowerOccupantBuff>();
            buff.Configure(world);
            EditorUtility.SetDirty(buff);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
