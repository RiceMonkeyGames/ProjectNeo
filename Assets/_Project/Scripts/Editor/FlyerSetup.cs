using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 비행 적(B-6.3) 시험용 자산을 만든다. 근접 사다리병을 복제해 길찾기·지상 판단을 떼고
    /// 비행 부품을 붙인다. SiegePrototype 편성에는 0명으로 등록해 기존 전투를 바꾸지 않는다.
    /// </summary>
    public static class FlyerSetup
    {
        const string Data = "Assets/_Project/Data/SiegePrototype/";
        public const string PrefabPath = "Assets/_Project/Prefabs/SiegePrototype/enemy_flyer.prefab";
        public const string SettingsPath = Data + "Flyer.asset";

        [MenuItem("ProjectNeo/Setup/Add Flyer Assets")]
        public static void CreateAssets()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/SiegePrototype/enemy_ladder.prefab");
            var siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(Data + "Siege.asset");
            if (source == null || siege == null) throw new InvalidOperationException("Create SiegePrototype first.");
            var strategy = LoadOrCreate(Data + "FlyerTargetWallTopFirst.asset", WallTopFirstFlyerTargetStrategy.Create);
            var movement = LoadOrCreate(Data + "FlyerDirectFlight.asset", () => DirectFlightMovement.Create());
            var engagement = LoadOrCreate(Data + "FlyerHoverBeside.asset", () => HoverBesideEngagement.Create());
            var settings = LoadOrCreate(SettingsPath, () => FlyerSettings.Create(strategy, movement, engagement));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(source, settings, siege);
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

        static T LoadOrCreate<T>(string path, Func<T> create) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = create();
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static GameObject CreatePrefab(GameObject source, FlyerSettings settings, SiegeSettings siege)
        {
            var unit = UnityEngine.Object.Instantiate(source);
            try
            {
                unit.name = "enemy_flyer";
                unit.SetActive(false);
                // 지상 판단은 길찾기 부품을 요구하므로 판단부터 뗀다.
                RemoveAll<SiegeAttackerBrain>(unit);
                RemoveAll<WallBreakerAttack>(unit);
                RemoveAll<UnitCombatAI>(unit);
                RemoveAll<NavMeshAgent>(unit);
                RemoveAll<UnitFallController>(unit);
                unit.AddComponent<AirborneBody>();
                unit.AddComponent<FlyerBrain>().Configure(settings, siege, Vector2Int.up);
                AddWings(unit);
                return PrefabUtility.SaveAsPrefabAsset(unit, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(unit); }
        }

        static void RemoveAll<T>(GameObject unit) where T : Component
        {
            foreach (T component in unit.GetComponents<T>()) UnityEngine.Object.DestroyImmediate(component);
        }

        // 종류를 눈으로 구분하는 임시 표시. 사다리병의 널빤지 대신 양옆으로 펼친 날개. 죽으면 숨긴다.
        static void AddWings(GameObject unit)
        {
            Transform visual = unit.transform.Find("Visual");
            if (visual == null) return;
            Transform old = visual.Find("RoleMarker");
            Material material = old != null ? old.GetComponent<Renderer>().sharedMaterial : null;
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (material == null) return;
            var root = new GameObject("RoleMarker");
            root.transform.SetParent(visual, false);
            root.AddComponent<PlaceholderDeathHider>();
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject wing = CombatPrototypeSetup.Box("Wing", Vector3.zero, new Vector3(1.1f, 0.06f, 0.5f), material);
                UnityEngine.Object.DestroyImmediate(wing.GetComponent<Collider>());
                wing.transform.SetParent(root.transform, false);
                wing.transform.localPosition = new Vector3(side * 0.85f, 1.45f, -0.2f);
                wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * -15f);
            }
        }
    }
}
