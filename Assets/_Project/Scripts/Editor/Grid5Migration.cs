using System;
using System.Linq;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-38: 성벽 격자를 킷 원래 크기(5m 칸, 5m 층)로 옮긴다. 조각은 늘리지 않는다(분지 지형 결정 2026-11-27).
    /// 공용 조각 정의(성벽·계단·보루·성문)는 GUID를 지킨 채 값만 바꾸고, 3m 시절에서 옮겨 온 프로토타입 씬(건설·성벽 지휘·수성전)의
    /// 미터 자리를 칸 비율(5/7.5)로 줄인다. Game 씬은 SIEGE-40(분지 지형, 북쪽 성벽 격자)에서 5m로 다시 만든다.
    /// 한 번만 실행한다(이미 5m면 멈춘다).
    /// </summary>
    public static class Grid5Migration
    {
        public const float Cell = 5f, Storey = 5f;
        // 5m 조각 한 칸의 체력·비용(SIEGE-14 킷 결정 값). 7.5m 칸 값(150, 15골드)의 2/3.
        public const float WallHealth = 100f;
        public const int WallGold = 10;
        public const int WallMaxStorey = Grid75Migration.WallMaxStorey, GateMaxStorey = Grid75Migration.GateMaxStorey;
        public const string GatePrefabPath = "Assets/_Project/Prefabs/ConstructionPrototype/MainGate.prefab";
        const float OldCell = Grid75Migration.Cell;
        const float Shrink = Cell / OldCell;
        const string Construction = "Assets/_Project/Data/ConstructionPrototype/";
        const string Siege = "Assets/_Project/Data/SiegePrototype/";
        const string NarrowMeshPath = Construction + "StairsRampNarrow.asset";
        const string CostsPath = "Assets/_Project/Data/GameFlow/Build/BuildCosts.asset";

        [MenuItem("ProjectNeo/Setup/Migrate Wall Grid To 5m Kit Size (SIEGE-38)")]
        public static void Migrate()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            var constructionGrid = Required<BuildGridDefinition>(Construction + "Grid.asset");
            if (Mathf.Approximately(constructionGrid.CellSize, Cell)) throw new InvalidOperationException("Already on the 5m grid.");

            ConfigurePieces();
            ConfigureGate(Required<BuildPieceDefinition>(Siege + "Gate.asset"));
            SetCell(constructionGrid);
            SetCell(Required<BuildGridDefinition>(Siege + "Grid.asset"));
            ScaleCamera(Required<BuildCameraSettings>(Construction + "Camera.asset"));
            ScaleCamera(Required<BuildCameraSettings>(Siege + "Camera.asset"));
            SetWallCost();
            AssetDatabase.SaveAssets();

            MigratePrototypeScene(ConstructionPrototypeSetup.ScenePath);
            MigratePrototypeScene(WallCommandSetup.ScenePath);
            MigratePrototypeScene(SiegePrototypeSetup.ScenePath);
            Grid75Migration.RebuildVerificationScenes();
            AssetDatabase.SaveAssets();
        }

        /// <summary>공용 조각 정의(Data/ConstructionPrototype)에 5m 격자 값을 쓴다. 새 프로토타입 씬을 만들 때도 부른다.</summary>
        internal static void ConfigurePieces()
        {
            Configure(Required<BuildPieceDefinition>(Construction + "Wall.asset"), Storey, WallHealth, WallMaxStorey);
            // 설치형 계단은 칸 폭(5m) 그대로 칸 전체를 45°로 오른다. 땅 위에만 놓으므로 1층이다.
            var stairs = Required<BuildPieceDefinition>(Construction + "Stairs.asset");
            Configure(stairs, Storey, stairs.MaxHealth, WallMaxStorey,
                SyntyGridSpikeSetup.NarrowStairs(Grid75Migration.NarrowStairsPath, NarrowMeshPath, Storey / Cell));
            var bastion = Required<BuildPieceDefinition>(Construction + "Bastion.asset");
            Configure(bastion, Storey, bastion.MaxHealth, WallMaxStorey);
            ConfigureGate(Required<BuildPieceDefinition>(Construction + "Gate.asset"));
        }

        /// <summary>성문은 한 칸, 5m. 킷 성문 틀(5m 모듈)이 칸에 그대로 맞는 5m 성문 프리팹을 쓴다.</summary>
        internal static void ConfigureGate(BuildPieceDefinition gate)
        {
            Configure(gate, Storey, gate.MaxHealth, GateMaxStorey, Required<GameObject>(GatePrefabPath));
            var data = new SerializedObject(gate);
            data.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gate);
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

        // 판이 칸 비율만큼 좁아지므로 건설 화면도 같은 비율로 좁힌다.
        internal static void ScaleCamera(BuildCameraSettings camera)
        {
            var data = new SerializedObject(camera);
            foreach (string name in new[] { "orthographicSize", "distance", "minZoom", "maxZoom" })
                data.FindProperty(name).floatValue *= Shrink;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(camera);
        }

        static void SetWallCost()
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

        /// <summary>
        /// 프로토타입 씬 하나를 5m 격자에 맞춘다: 성 판·바닥을 격자 크기로 줄이고, 미터로 놓인 루트 오브젝트(유닛·플레이어 등)를
        /// 판 가운데를 기준으로 줄인다. 칸 좌표로 정한 배치는 그대로다. 높이는 층이 5m 그대로라 바꾸지 않는다.
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
            EditorUtility.SetDirty(world);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == world.gameObject || root.GetComponent<Canvas>() != null || root.GetComponent<Light>() != null
                    || root.GetComponent<Camera>() != null || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                root.transform.position = Narrow(root.transform.position);
                if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            }
            var director = Find<SiegeDirector>(scene, false);
            if (director != null)
            {
                var data = new SerializedObject(director);
                SerializedProperty allies = data.FindProperty("allySpawnPoints");
                for (int i = 0; i < allies.arraySize; i++) allies.GetArrayElementAtIndex(i).vector3Value = Narrow(allies.GetArrayElementAtIndex(i).vector3Value);
                SerializedProperty enemy = data.FindProperty("enemySpawnPoint");
                enemy.vector3Value = Narrow(enemy.vector3Value);
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

        static Vector3 Narrow(Vector3 point) => new Vector3(point.x * Shrink, point.y, point.z * Shrink);

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
