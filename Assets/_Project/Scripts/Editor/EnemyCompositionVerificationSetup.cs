using System;
using System.Linq;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>B-6.4 전용 혼합 편성. 기존 씬/편성 및 비행 모듈은 변경하지 않는다.</summary>
    public static class EnemyCompositionVerificationSetup
    {
        const string Data = "Assets/_Project/Data/SiegePrototype/";
        const string Prefabs = "Assets/_Project/Prefabs/SiegePrototype/";
        public const string ScenePath = "Assets/_Project/Scenes/EnemyCompositionVerification.unity";
        public const string SettingsPath = Data + "SiegeEnemyCompositionVerification.asset";
        public const string VariantPath = Prefabs + "enemy_flyer_high_archer.prefab";

        [MenuItem("ProjectNeo/Setup/Create Enemy Composition Verification Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the active scene first.");
            var source = Load<SiegeSettings>(Data + "Siege.asset");
            var baseFlyer = Load<GameObject>(FlyerSetup.PrefabPath);
            var archer = Load<GameObject>(Prefabs + "enemy_archer.prefab").GetComponent<CharacterActor>();
            var baseSettings = baseFlyer.GetComponent<FlyerBrain>().Settings;

            // 시험용 변종: 기존 목표/이동 모듈 + 높은 체공 데이터 + 기존 궁수 전투 데이터.
            var hover = Save(HoverBesideEngagement.Create(hoverHeight: 3f, standoffRatio: 0.1f), Data + "FlyerHighHoverVerification.asset");
            var variantSettings = Save(FlyerSettings.Create(baseSettings.TargetStrategy, baseSettings.Movement, hover,
                baseSettings.CruiseClearance, baseSettings.MeleeExposureAltitude, baseSettings.WallTopMinimumHeight,
                baseSettings.GroundProbeDistance, baseSettings.CorpseFallAcceleration, baseSettings.HoldInset,
                baseSettings.TargetsSiegeWeapons, baseSettings.HoverSeparation, baseSettings.RoofProbeDistance),
                Data + "FlyerHighArcherVerification.asset");
            GameObject instance = UnityEngine.Object.Instantiate(baseFlyer);
            GameObject variant;
            try
            {
                instance.name = "enemy_flyer_high_archer";
                instance.GetComponent<CharacterActor>().Configure(archer.SoldierDefinition, archer.Profile, CombatTeam.Hostile);
                instance.GetComponent<FlyerBrain>().Configure(variantSettings, source, Vector2Int.up);
                // 눈으로 구분하기 위한 날개 표시만 늘린다. 몸통 판정은 그대로다.
                Transform marker = instance.transform.Find("Visual/RoleMarker");
                if (marker != null) marker.localScale = new Vector3(1.5f, 1f, 1f);
                variant = PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }

            var settings = Save(UnityEngine.Object.Instantiate(source), SettingsPath);
            settings.ConfigureRoster(source.AllySquads.ToArray(), new[]
            {
                Group("enemy_ladder", 2), Group("enemy_wall_breaker", 1),
                new SiegeUnitGroup { unitPrefab = baseFlyer, count = 1 },
                new SiegeUnitGroup { unitPrefab = variant, count = 1 }
            });
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // 원본의 빈칸을 닫고 첫 근접 부대만 성벽 위에 둔다.
            // 재실행 때도 원본으로부터 다시 구성하되 기존 씬 GUID는 유지한다.
            var scene = EditorSceneManager.OpenScene(SiegePrototypeSetup.ScenePath, OpenSceneMode.Single);
            settings = Load<SiegeSettings>(SettingsPath);
            source = Load<SiegeSettings>(Data + "Siege.asset");
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            foreach (MonoBehaviour component in components)
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == source)
                        property.objectReferenceValue = settings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var world = components.OfType<ConstructionWorld>().Single();
            var wall = Load<BuildPieceDefinition>("Assets/_Project/Data/ConstructionPrototype/Wall.asset");
            var worldObject = new SerializedObject(world);
            var pieces = worldObject.FindProperty("initialPieces");
            foreach (int x in new[] { 4, 11, 12 })
            {
                int index = pieces.arraySize++;
                var piece = pieces.GetArrayElementAtIndex(index);
                piece.FindPropertyRelative("definition").objectReferenceValue = wall;
                piece.FindPropertyRelative("cell").vector2IntValue = new Vector2Int(x, 8);
                piece.FindPropertyRelative("rotation").intValue = 0;
                piece.FindPropertyRelative("fixedGate").boolValue = false;
            }
            worldObject.ApplyModifiedPropertiesWithoutUndo();
            var director = new SerializedObject(components.OfType<SiegeDirector>().Single());
            var spawns = director.FindProperty("allySpawnPoints");
            spawns.GetArrayElementAtIndex(0).vector3Value = world.transform.TransformPoint(new Vector3(
                14f * world.Grid.CellSize, wall.Height, 8.5f * world.Grid.CellSize));
            director.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static SiegeUnitGroup Group(string name, int count) => new SiegeUnitGroup { unitPrefab = Load<GameObject>(Prefabs + name + ".prefab"), count = count };
        static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);

        static T Save<T>(T value, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
