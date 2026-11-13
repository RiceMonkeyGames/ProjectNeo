using System;
using System.Collections.Generic;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    public static class WallBreakerSetup
    {
        const string Data = "Assets/_Project/Data/SiegePrototype/";
        const string Prefab = "Assets/_Project/Prefabs/SiegePrototype/enemy_wall_breaker.prefab";

        [MenuItem("ProjectNeo/Setup/Add Wall Breaker Assets")]
        public static void CreateAssets()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/SiegePrototype/enemy_gate_breaker.prefab");
            var siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(Data + "Siege.asset");
            if (source == null || siege == null) throw new InvalidOperationException("Create SiegePrototype first.");
            var grid = AssetDatabase.LoadAssetAtPath<BuildGridDefinition>(Data + "Grid.asset");
            if (grid == null) throw new InvalidOperationException("SiegePrototype grid is missing.");
            if (grid.MaxLevels < 3)
            {
                var gridData = new SerializedObject(grid);
                gridData.FindProperty("maxLevels").intValue = 3;
                gridData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(grid);
            }
            var strategy = AssetDatabase.LoadAssetAtPath<NearestWallTargetStrategy>(Data + "NearestWall.asset");
            if (strategy == null)
            {
                strategy = NearestWallTargetStrategy.Create();
                AssetDatabase.CreateAsset(strategy, Data + "NearestWall.asset");
            }
            var settings = AssetDatabase.LoadAssetAtPath<WallBreakerSettings>(Data + "WallBreaker.asset");
            if (settings == null)
            {
                settings = WallBreakerSettings.Create(strategy);
                AssetDatabase.CreateAsset(settings, Data + "WallBreaker.asset");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null)
            {
                var unit = UnityEngine.Object.Instantiate(source);
                try
                {
                    unit.name = "enemy_wall_breaker";
                    unit.SetActive(false);
                    unit.AddComponent<WallBreakerAttack>().Configure(settings);
                    unit.GetComponent<SiegeAttackerBrain>().Configure(SiegeAttackerRole.WallBreaker,
                        null, siege, new SiegeLadder[0], Vector2Int.up);
                    prefab = PrefabUtility.SaveAsPrefabAsset(unit, Prefab);
                }
                finally { UnityEngine.Object.DestroyImmediate(unit); }
            }
            // 기본 전투는 유지하고 Inspector에서 수만 늘려 시험할 수 있도록 등록한다.
            var groups = new List<SiegeUnitGroup>(siege.EnemyGroups);
            if (!groups.Exists(g => g.unitPrefab == prefab))
            {
                groups.Add(new SiegeUnitGroup { unitPrefab = prefab, count = 0 });
                siege.ConfigureRoster(new List<SiegeUnitGroup>(siege.AllySquads).ToArray(), groups.ToArray());
                EditorUtility.SetDirty(siege);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
