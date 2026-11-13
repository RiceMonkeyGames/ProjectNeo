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
    /// 비행 적(B-6.3)을 바로 눈으로 확인하는 씬을 만든다. SiegePrototype을 복사해
    /// 성벽 빈칸을 막고, 적은 비행 적 2마리만, 1번 부대(근접)는 성벽 위에서 시작하게 한다.
    /// 원래 씬과 Siege.asset은 바꾸지 않는다. 다시 실행하면 새로 만든다.
    /// </summary>
    public static class FlyerVerificationSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/FlyerVerification.unity";
        public const string SettingsPath = "Assets/_Project/Data/SiegePrototype/SiegeFlyerVerification.asset";
        const string SiegePath = "Assets/_Project/Data/SiegePrototype/Siege.asset";
        const string WallPath = "Assets/_Project/Data/ConstructionPrototype/Wall.asset";
        const int WallRow = 8;
        const int FlyerCount = 2;
        static readonly int[] Gaps = { 4, 11, 12 };
        // 성벽 위에서 시작할 부대 자리(격자 x). 13~15칸 성벽 위 가운데.
        const float WallTopColumn = 14f;

        [MenuItem("ProjectNeo/Setup/Create Flyer Verification Scene")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            var siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SiegePath);
            var wall = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(WallPath);
            var flyer = AssetDatabase.LoadAssetAtPath<GameObject>(FlyerSetup.PrefabPath);
            if (siege == null || wall == null || flyer == null)
                throw new InvalidOperationException("Run ProjectNeo/Setup/Add Flyer Assets first.");

            // 확인용 편성: 아군은 그대로, 적은 비행 적만.
            AssetDatabase.DeleteAsset(SettingsPath);
            if (!AssetDatabase.CopyAsset(SiegePath, SettingsPath)) throw new InvalidOperationException("Copy failed: " + SettingsPath);
            var settings = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SettingsPath);
            var enemies = new List<SiegeUnitGroup>();
            foreach (SiegeUnitGroup group in siege.EnemyGroups)
                enemies.Add(new SiegeUnitGroup { unitPrefab = group.unitPrefab, count = group.unitPrefab == flyer ? FlyerCount : 0 });
            if (!enemies.Exists(g => g.unitPrefab == flyer)) enemies.Add(new SiegeUnitGroup { unitPrefab = flyer, count = FlyerCount });
            settings.ConfigureRoster(new List<SiegeUnitGroup>(siege.AllySquads).ToArray(), enemies.ToArray());
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SiegePrototypeSetup.ScenePath, ScenePath)) throw new InvalidOperationException("Copy failed: " + ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 앞서 읽은 자산이 내려갈 수 있어 다시 읽는다.
            siege = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SiegePath);
            settings = AssetDatabase.LoadAssetAtPath<SiegeSettings>(SettingsPath);
            wall = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(WallPath);
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

            // 1번 부대(근접 4명)는 성벽 위에서 시작한다. 나머지 부대는 원래 자리.
            float cell = world.Grid.CellSize;
            Vector3 wallTop = world.transform.TransformPoint(new Vector3(WallTopColumn * cell, wall.Height, (WallRow + 0.5f) * cell));
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
