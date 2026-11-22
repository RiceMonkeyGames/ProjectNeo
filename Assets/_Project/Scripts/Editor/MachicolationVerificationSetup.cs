using System;
using System.Collections.Generic;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-12 마치콜레이션을 바로 눈으로 확인하는 씬을 만든다. SiegePrototype을 복사해 성벽 빈칸을 막고,
    /// 아군은 원거리 부대 하나만 성문 옆 성벽 위(성문 구멍에서 6~9m)에서 중지(Hold)로 시작하게 하고, 적은 성문 파괴병만 둔다.
    /// 성(ConstructionWorld)에 성문 마치콜레이션을 붙인다. 원래 씬과 Siege.asset은 바꾸지 않는다. 다시 실행하면 새로 만든다.
    /// </summary>
    public static class MachicolationVerificationSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/MachicolationVerification.unity";
        public const string SettingsPath = "Assets/_Project/Data/SiegePrototype/SiegeMachicolationVerification.asset";
        const string SiegePath = "Assets/_Project/Data/SiegePrototype/Siege.asset";
        const string WallPath = "Assets/_Project/Data/ConstructionPrototype/Wall.asset";
        const string GateBreakerName = "enemy_gate_breaker";
        const string RangedName = "allied_ranged";
        const int WallRow = 8;
        const int GateBreakerCount = 3;
        static readonly int[] Gaps = { 4, 11, 12 };
        // 원거리 부대가 시작할 성벽 위 칸(격자 x). 성문(7~8칸) 동쪽 옆 9~10칸 성벽 위.
        const float WallTopColumn = 10f;

        [MenuItem("ProjectNeo/Setup/Create Machicolation Verification Scene")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            var siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SiegePath);
            var wall = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(WallPath);
            if (siege == null || wall == null) throw new InvalidOperationException("Missing " + SiegePath + " or " + WallPath);

            // 확인용 편성: 아군은 원거리 부대 하나, 적은 성문 파괴병만.
            GameObject ranged = null, gateBreaker = null;
            foreach (SiegeUnitGroup group in siege.AllySquads)
                if (ranged == null && group.unitPrefab != null && group.unitPrefab.name == RangedName) ranged = group.unitPrefab;
            foreach (SiegeUnitGroup group in siege.EnemyGroups)
                if (gateBreaker == null && group.unitPrefab != null && group.unitPrefab.name == GateBreakerName) gateBreaker = group.unitPrefab;
            if (ranged == null || gateBreaker == null)
                throw new InvalidOperationException("Siege.asset needs " + RangedName + " and " + GateBreakerName + " groups.");
            int rangedCount = 4;
            foreach (SiegeUnitGroup group in siege.AllySquads)
                if (group.unitPrefab == ranged) { rangedCount = group.count; break; }

            AssetDatabase.DeleteAsset(SettingsPath);
            if (!AssetDatabase.CopyAsset(SiegePath, SettingsPath)) throw new InvalidOperationException("Copy failed: " + SettingsPath);
            var settings = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SettingsPath);
            var enemies = new List<SiegeUnitGroup>();
            // 다른 적 그룹은 0명으로 남기고, 성문 파괴병은 한 그룹에만 GateBreakerCount명을 둔다.
            bool placed = false;
            foreach (SiegeUnitGroup group in siege.EnemyGroups)
            {
                bool breaker = !placed && group.unitPrefab == gateBreaker;
                placed |= breaker;
                enemies.Add(new SiegeUnitGroup { unitPrefab = group.unitPrefab, count = breaker ? GateBreakerCount : 0 });
            }
            settings.ConfigureRoster(new[] { new SiegeUnitGroup { unitPrefab = ranged, count = rangedCount } }, enemies.ToArray());
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            MachicolationSetup.CreateAssets();

            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SiegePrototypeSetup.ScenePath, ScenePath)) throw new InvalidOperationException("Copy failed: " + ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 앞서 읽은 자산이 내려갈 수 있어 다시 읽는다.
            siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SiegePath);
            settings = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SettingsPath);
            wall = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(WallPath);
            var machicolations = AssetDatabase.LoadAssetAtPath<MachicolationSettings>(MachicolationSetup.SettingsPath);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null) Replace(component, siege, settings);

            ConstructionWorld world = Single<ConstructionWorld>(scene);
            var pieces = new SerializedObject(world).FindProperty("initialPieces");
            foreach (int x in Gaps)
            {
                int index = pieces.arraySize;
                pieces.InsertArrayElementAtIndex(index);
                SerializedProperty piece = pieces.GetArrayElementAtIndex(index);
                piece.FindPropertyRelative("definition").objectReferenceValue = wall;
                piece.FindPropertyRelative("cell").vector2IntValue = new Vector2Int(x, WallRow);
                piece.FindPropertyRelative("rotation").intValue = 0;
                piece.FindPropertyRelative("fixedGate").boolValue = false;
            }
            pieces.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var gate = world.GetComponent<GateMachicolations>();
            if (gate == null) gate = world.gameObject.AddComponent<GateMachicolations>();
            gate.Configure(world, machicolations);
            EditorUtility.SetDirty(gate);

            // 원거리 부대(첫 부대)는 성문 옆 성벽 위에서 시작한다. 아군 부대는 처음에 중지(Hold)다.
            float cell = world.Grid.CellSize;
            Vector3 wallTop = world.transform.TransformPoint(new Vector3((WallTopColumn + 0.5f) * cell, wall.Height, (WallRow + 0.5f) * cell));
            var spawns = new SerializedObject(Single<SiegeDirector>(scene)).FindProperty("allySpawnPoints");
            if (spawns.arraySize == 0) spawns.InsertArrayElementAtIndex(0);
            spawns.GetArrayElementAtIndex(0).vector3Value = wallTop;
            spawns.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void Replace(UnityEngine.Object target, UnityEngine.Object from, UnityEngine.Object to)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == from)
                {
                    property.objectReferenceValue = to;
                    changed = true;
                }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Single<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (found != null) throw new InvalidOperationException("More than one " + typeof(T).Name);
                    found = component;
                }
            if (found == null) throw new InvalidOperationException("Missing " + typeof(T).Name);
            return found;
        }
    }
}
