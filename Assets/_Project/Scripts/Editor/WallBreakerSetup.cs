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
                settings = WallBreakerSettings.Create(LoadOrCreateFocusedBreach());
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

        const string FocusedBreachPath = Data + "FocusedBreach.asset";

        /// <summary>
        /// 집중 돌파 전략(SIEGE-34)을 만들고, 모든 장면이 함께 쓰는 <c>WallBreaker.asset</c>의 기본 전략으로 건다. 여러 번 실행해도 같다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Apply Focused Breach Wall Strategy (SIEGE-34)")]
        public static void ApplyFocusedBreach()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WallBreakerSettings>(Data + "WallBreaker.asset");
            if (settings == null) throw new InvalidOperationException("Run Add Wall Breaker Assets first.");
            var data = new SerializedObject(settings);
            var property = data.FindProperty("targetStrategy");
            var focused = LoadOrCreateFocusedBreach();
            if (property.objectReferenceValue != focused)
            {
                property.objectReferenceValue = focused;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }

        static FocusedBreachWallTargetStrategy LoadOrCreateFocusedBreach()
        {
            var focused = AssetDatabase.LoadAssetAtPath<FocusedBreachWallTargetStrategy>(FocusedBreachPath);
            if (focused != null) return focused;
            focused = FocusedBreachWallTargetStrategy.Create();
            AssetDatabase.CreateAsset(focused, FocusedBreachPath);
            return focused;
        }
    }
}
