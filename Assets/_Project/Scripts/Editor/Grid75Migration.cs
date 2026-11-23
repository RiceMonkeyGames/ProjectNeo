using System;
using System.Linq;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Flow;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-18: Game과 3m 프로토타입 씬(건설·성벽 지휘·수성전)을 7.5m 칸, 5m 층 격자로 옮긴다.
    /// 공용 조각 정의(성벽·계단·보루·성문)는 GUID를 지킨 채 값만 바꾸므로 저장 콘텐츠 목록·비용 표 참조가 그대로 이어진다.
    /// 프로토타입 씬은 칸 좌표로 정한 배치는 그대로 두고, 미터로 놓인 루트 오브젝트 자리를 칸 비율(×2.5)·층 비율(×5/3)로 늘린다.
    /// Game 씬은 <see cref="GameFlowSetup.ApplyDefaultCastle"/>을 다시 실행해 새 기본 성·안뜰·사다리를 놓는다.
    /// 한 번만 실행한다(이미 7.5m면 멈춘다). 새로 만드는 프로토타입 씬도 같은 값을 쓴다(<see cref="ConfigurePieces"/>).
    /// </summary>
    public static class Grid75Migration
    {
        public const float Cell = 7.5f, Storey = 5f;
        // 성벽 한 칸 체력·비용은 5m 조각 값(100, 10골드)의 1.5배(7.5m 길이, 사용자 결정 2026-11-23).
        public const float WallHealth = 150f;
        public const int WallGold = 15;
        // Q-59: 성벽·보루·계단·성문 칸은 3층(15m)까지. 성문 조각 자체는 땅 위 한 층이다.
        public const int WallMaxStorey = 3, GateMaxStorey = 1;
        // 한 층(5m)에서 떨어지면 3m 칸 시절 한 층(3m)과 같은 24 피해, 세 층(15m)은 84. 1m 넘는 낙하(사다리 중간)는 그대로 아프다.
        public const float FallSafeHeight = 1f, FallDamagePerMeter = 6f;
        const float OldCell = 3f, OldStorey = 3f;
        const float Spread = Cell / OldCell, Rise = Storey / OldStorey;
        const string Construction = "Assets/_Project/Data/ConstructionPrototype/";
        const string Siege = "Assets/_Project/Data/SiegePrototype/";
        public const string NarrowStairsPath = "Assets/_Project/Prefabs/ConstructionPrototype/StairsNarrow.prefab";
        const string NarrowMeshPath = Construction + "StairsRampNarrow.asset";
        const string CostsPath = "Assets/_Project/Data/GameFlow/Build/BuildCosts.asset";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";

        [MenuItem("ProjectNeo/Setup/Migrate To 7.5m Grid (SIEGE-18)")]
        public static void Migrate()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            var constructionGrid = Required<BuildGridDefinition>(Construction + "Grid.asset");
            if (Mathf.Approximately(constructionGrid.CellSize, Cell)) throw new InvalidOperationException("Already on the 7.5m grid.");

            ConfigurePieces();
            ConfigureGate(Required<BuildPieceDefinition>(Siege + "Gate.asset"));
            ConfigureFall(Required<FallSettings>(Construction + "Fall.asset"));
            SetCell(constructionGrid);
            SetCell(Required<BuildGridDefinition>(Siege + "Grid.asset"));
            WidenCamera(Required<BuildCameraSettings>(Construction + "Camera.asset"));
            WidenCamera(Required<BuildCameraSettings>(Siege + "Camera.asset"));
            ScaleWallCost();
            ScaleSpawnCells();
            AssetDatabase.SaveAssets();

            MigratePrototypeScene(ConstructionPrototypeSetup.ScenePath);
            MigratePrototypeScene(WallCommandSetup.ScenePath);
            MigratePrototypeScene(SiegePrototypeSetup.ScenePath);
            RebuildVerificationScenes();
            GameFlowSetup.ApplyDefaultCastle();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 수성전 프로토타입 씬을 복사해 만드는 검증 씬(적 편성·비행 적·머더홀)을 옮긴 수성전 씬에서 다시 만든다.
        /// 세 설정 메뉴 모두 원본에서 다시 구성하고 칸 크기로 자리를 정한다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Rebuild Verification Scenes From Siege Prototype (SIEGE-18)")]
        public static void RebuildVerificationScenes()
        {
            EnemyCompositionVerificationSetup.Create();
            FlyerVerificationSetup.Create();
            MachicolationVerificationSetup.Create();
        }

        /// <summary>공용 조각 정의(Data/ConstructionPrototype)에 7.5m 격자 값을 쓴다. 새 프로토타입 씬을 만들 때도 부른다.</summary>
        internal static void ConfigurePieces()
        {
            Configure(Required<BuildPieceDefinition>(Construction + "Wall.asset"), Storey, WallHealth, WallMaxStorey);
            // 설치형 계단은 5m 폭으로 칸 가운데에 서고 칸 전체를 오른다(SIEGE-35). 땅 위에만 놓으므로 1층이다.
            var stairs = Required<BuildPieceDefinition>(Construction + "Stairs.asset");
            Configure(stairs, Storey, stairs.MaxHealth, WallMaxStorey,
                SyntyGridSpikeSetup.NarrowStairs(NarrowStairsPath, NarrowMeshPath, Storey / Cell));
            var bastion = Required<BuildPieceDefinition>(Construction + "Bastion.asset");
            Configure(bastion, Storey, bastion.MaxHealth, WallMaxStorey);
            ConfigureGate(Required<BuildPieceDefinition>(Construction + "Gate.asset"));
        }

        /// <summary>성문은 한 칸, 5m. 위층은 고정 성벽으로 얹는다(2층 성문탑).</summary>
        internal static void ConfigureGate(BuildPieceDefinition gate)
        {
            Configure(gate, Storey, gate.MaxHealth, GateMaxStorey);
            var data = new SerializedObject(gate);
            data.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gate);
        }

        internal static void ConfigureFall(FallSettings fall)
        {
            var data = new SerializedObject(fall);
            data.FindProperty("safeHeight").floatValue = FallSafeHeight;
            data.FindProperty("damagePerMeter").floatValue = FallDamagePerMeter;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fall);
        }

        static void Configure(BuildPieceDefinition piece, float height, float health, int maxStorey, GameObject prefab = null)
        {
            var data = new SerializedObject(piece);
            data.FindProperty("height").floatValue = height;
            data.FindProperty("maxHealth").floatValue = health;
            data.FindProperty("maxStorey").intValue = maxStorey;
            if (prefab != null) data.FindProperty("prefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(piece);
        }

        static void SetCell(BuildGridDefinition grid)
        {
            var data = new SerializedObject(grid);
            data.FindProperty("cellSize").floatValue = Cell;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(grid);
        }

        // 판이 칸 비율만큼 넓어지므로 건설 화면도 같은 비율로 넓힌다.
        internal static void WidenCamera(BuildCameraSettings camera)
        {
            var data = new SerializedObject(camera);
            foreach (string name in new[] { "orthographicSize", "distance", "minZoom", "maxZoom" })
                data.FindProperty(name).floatValue *= Spread;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(camera);
        }

        static void ScaleWallCost()
        {
            var costs = AssetDatabase.LoadAssetAtPath<BuildCostTable>(CostsPath);
            if (costs == null) return;
            var wall = Required<BuildPieceDefinition>(Construction + "Wall.asset");
            var data = new SerializedObject(costs);
            SerializedProperty pieces = data.FindProperty("pieces");
            for (int i = 0; i < pieces.arraySize; i++)
            {
                SerializedProperty entry = pieces.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("piece").objectReferenceValue == wall)
                    entry.FindPropertyRelative("gold").intValue = WallGold;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(costs);
        }

        // 스폰 칸은 3m 칸 좌표였다. 같은 미터 자리가 되도록 칸 수를 3/7.5로 줄인다.
        static void ScaleSpawnCells()
        {
            const float ratio = OldCell / Cell;
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle != null) ScaleVector2(battle, "spawnCell", ratio);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SpawnPointDefinition)))
                ScaleVector2(AssetDatabase.LoadAssetAtPath<SpawnPointDefinition>(AssetDatabase.GUIDToAssetPath(guid)), "cell", ratio);
        }

        static void ScaleVector2(Object target, string property, float ratio)
        {
            var data = new SerializedObject(target);
            SerializedProperty value = data.FindProperty(property);
            value.vector2Value *= ratio;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 프로토타입 씬 하나를 7.5m 격자에 맞춘다: 성 판·바닥을 격자 크기로, 두 칸 성문 옆 칸은 성벽으로 메우고,
        /// 미터로 놓인 루트 오브젝트(유닛·플레이어 등)는 판 가운데를 기준으로 늘린다.
        /// </summary>
        static void MigratePrototypeScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConstructionWorld world = Find<ConstructionWorld>(scene);
            BuildGridDefinition grid = world.Grid;
            float width = grid.Width * grid.CellSize, depth = grid.Depth * grid.CellSize;
            world.transform.position = new Vector3(-width * 0.5f, 0f, -depth * 0.5f);
            Transform ground = world.transform.Find("Geometry/Ground");
            ground.localScale = new Vector3(width, 1f, depth);
            ground.localPosition = new Vector3(width * 0.5f, -0.5f, depth * 0.5f);
            FillBesideGate(world);
            EditorUtility.SetDirty(world);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == world.gameObject || root.GetComponent<Canvas>() != null || root.GetComponent<Light>() != null
                    || root.GetComponent<Camera>() != null || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                Vector3 p = root.transform.position;
                root.transform.position = new Vector3(p.x * Spread, p.y * Rise, p.z * Spread);
                if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            }
            var director = Find<SiegeDirector>(scene, false);
            if (director != null)
            {
                var data = new SerializedObject(director);
                SerializedProperty allies = data.FindProperty("allySpawnPoints");
                for (int i = 0; i < allies.arraySize; i++) allies.GetArrayElementAtIndex(i).vector3Value = Widen(allies.GetArrayElementAtIndex(i).vector3Value);
                SerializedProperty enemy = data.FindProperty("enemySpawnPoint");
                enemy.vector3Value = Widen(enemy.vector3Value);
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
            }
            var orbit = Find<ConstructionCameraController>(scene, false);
            if (orbit != null)
            {
                var settings = (BuildCameraSettings)new SerializedObject(orbit).FindProperty("settings").objectReferenceValue;
                var camera = orbit.GetComponent<Camera>();
                if (settings != null)
                {
                    camera.orthographicSize = settings.OrthographicSize;
                    camera.transform.position = -(camera.transform.forward * settings.Distance);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Vector3 Widen(Vector3 point) => new Vector3(point.x * Spread, point.y * Rise, point.z * Spread);

        // 3m 칸 성문은 2×1이었다. 한 칸이 된 성문 동쪽 칸이 비지 않게 같은 줄 성벽으로 메운다.
        static void FillBesideGate(ConstructionWorld world)
        {
            var data = new SerializedObject(world);
            SerializedProperty seeds = data.FindProperty("initialPieces");
            Vector2Int gate = new Vector2Int(-1, -1);
            Object wall = null;
            for (int i = 0; i < seeds.arraySize; i++)
            {
                SerializedProperty seed = seeds.GetArrayElementAtIndex(i);
                var piece = (BuildPieceDefinition)seed.FindPropertyRelative("definition").objectReferenceValue;
                if (piece == null) continue;
                if (piece.Kind == BuildPieceKind.Gate) gate = seed.FindPropertyRelative("cell").vector2IntValue;
                if (piece.Kind == BuildPieceKind.Wall && wall == null) wall = piece;
            }
            if (gate.x < 0 || wall == null) return;
            Vector2Int beside = gate + Vector2Int.right;
            for (int i = 0; i < seeds.arraySize; i++)
                if (seeds.GetArrayElementAtIndex(i).FindPropertyRelative("cell").vector2IntValue == beside) return;
            seeds.InsertArrayElementAtIndex(seeds.arraySize);
            SerializedProperty added = seeds.GetArrayElementAtIndex(seeds.arraySize - 1);
            added.FindPropertyRelative("definition").objectReferenceValue = wall;
            added.FindPropertyRelative("cell").vector2IntValue = beside;
            added.FindPropertyRelative("rotation").intValue = 0;
            added.FindPropertyRelative("fixedGate").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Find<T>(Scene scene, bool required = true) where T : Component
        {
            T found = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
            if (found == null && required) throw new InvalidOperationException(scene.path + " has no " + typeof(T).Name);
            return found;
        }

        static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
    }
}
