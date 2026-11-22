using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 200 vs 200(총 400명) 대규모 전투 및 3단계 AI LOD를 직접 플레이하며 눈으로 확인할 수 있는
    /// 전용 검증 씬(`Assets/_Project/Scenes/LargeBattleVerification.unity`)을 생성한다.
    /// 기존 프로토타입·게임 씬과 데이터는 수정하지 않는다.
    /// </summary>
    public static class LargeBattleVerificationSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/LargeBattleVerification.unity";
        const string DataDir = "Assets/_Project/Data/LargeBattleVerification";
        const string UnitsDir = "Assets/_Project/Prefabs/UnitBattlePrototype/";

        [MenuItem("ProjectNeo/Setup/Create 200v200 AI LOD Verification Scene")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");

            GameObject allyMelee = Required<GameObject>(UnitsDir + "allied_melee.prefab");
            GameObject allyRanged = Required<GameObject>(UnitsDir + "allied_ranged.prefab");
            GameObject enemyMelee = Required<GameObject>(UnitsDir + " 적 근접병.prefab", UnitsDir + "hostile_melee.prefab", UnitsDir + "적 근접병.prefab");
            GameObject enemyRanged = Required<GameObject>(UnitsDir + " 적 원거리병.prefab", UnitsDir + "hostile_ranged.prefab", UnitsDir + "적 원거리병.prefab");
            GameObject playerPrefab = Required<GameObject>("Assets/_Project/Prefabs/CombatPrototype/Player.prefab");
            var controls = Required<PlayerControlSettings>("Assets/_Project/Data/CombatPrototype/PlayerControls.asset");
            var inputActions = Required<InputActionAsset>("Assets/_Project/Input/InputSystem_Actions.inputactions");

            CombatPrototypeSetup.EnsureFolder(DataDir);
            AssetDatabase.DeleteAsset(DataDir + "/SquadConfig200.asset");
            AssetDatabase.DeleteAsset(DataDir + "/Command200.asset");
            AssetDatabase.DeleteAsset(DataDir + "/AllyAI200.asset");
            AssetDatabase.DeleteAsset(DataDir + "/EnemyAI200.asset");
            var squadConfig = CombatPrototypeSetup.Save(SquadConfig.Create(10, 20), DataDir + "/SquadConfig200.asset");
            var command = CombatPrototypeSetup.Save(SquadCommandSettings.Create(), DataDir + "/Command200.asset");
            var allyAi = CombatPrototypeSetup.Save(UnitAISettings.Create(detectionRange: 90f, maxPathCandidatesPerTick: 4,
                maxPathQueriesPerFrame: 0, decisionStaggerBuckets: 8, enableAiLod: true, lod0Distance: 25f, lod1Distance: 60f),
                DataDir + "/AllyAI200.asset");
            var enemyAi = CombatPrototypeSetup.Save(UnitAISettings.Create(detectionRange: 90f, maxPathCandidatesPerTick: 4,
                maxPathQueriesPerFrame: 0, decisionStaggerBuckets: 8, enableAiLod: true, lod0Distance: 25f, lod1Distance: 60f),
                DataDir + "/EnemyAI200.asset");

            Material ground = CombatPrototypeSetup.MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.25f));
            Material stone = CombatPrototypeSetup.MaterialAsset("Stone", new Color(0.38f, 0.43f, 0.49f));
            Material lod0Mat = CombatPrototypeSetup.MaterialAsset("Lod0Green", new Color(0.15f, 0.95f, 0.3f));
            Material lod1Mat = CombatPrototypeSetup.MaterialAsset("Lod1Yellow", new Color(1f, 0.78f, 0.15f));
            Material lod2Mat = CombatPrototypeSetup.MaterialAsset("Lod2Gray", new Color(0.55f, 0.6f, 0.68f));
            Material sleepMat = CombatPrototypeSetup.MaterialAsset("LodSleepCyan", new Color(0.2f, 0.85f, 1f));

            AssetDatabase.DeleteAsset(ScenePath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // 180m x 180m 대형 평원 전장 + 중앙 엄폐물 3개
            var geometry = new GameObject("NavigationGeometry");
            Geometry(geometry, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(180f, 1f, 180f), ground);
            Geometry(geometry, "CenterObstacle", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 3f), stone);
            Geometry(geometry, "WestObstacle", new Vector3(-36f, 1.5f, 0f), new Vector3(6f, 3f, 3f), stone);
            Geometry(geometry, "EastObstacle", new Vector3(36f, 1.5f, 0f), new Vector3(6f, 3f, 3f), stone);
            Geometry(geometry, "NorthWall", new Vector3(0f, 1.5f, 90f), new Vector3(180f, 3f, 1f), stone);
            Geometry(geometry, "SouthWall", new Vector3(0f, 1.5f, -90f), new Vector3(180f, 3f, 1f), stone);
            Geometry(geometry, "WestWall", new Vector3(-90f, 1.5f, 0f), new Vector3(1f, 3f, 180f), stone);
            Geometry(geometry, "EastWall", new Vector3(90f, 1.5f, 0f), new Vector3(1f, 3f, 180f), stone);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(DataDir + "/Navigation200.asset");
            AssetDatabase.CreateAsset(surface.navMeshData, DataDir + "/Navigation200.asset");

            var unitsParent = new GameObject("Units_400").transform;

            // 아군 200명: 10개 분대 × 20명 (전열 6개 근접 분대 120명, 후열 4개 궁수 분대 80명)
            var rosters = new SquadRoster[10];
            for (int s = 0; s < 10; s++)
            {
                bool ranged = s >= 6;
                GameObject prefab = ranged ? allyRanged : allyMelee;
                float xCenter = ranged ? (s - 7.5f) * 24f : (s - 2.5f) * 18f;
                float zCenter = ranged ? -34f : -22f;
                rosters[s].members = SpawnBlock(prefab, $"Ally_S{s + 1}_", 20, 5,
                    new Vector3(xCenter, 0f, zCenter), 0f, allyAi, unitsParent);
            }

            // 적군 200명: 10개 그룹 × 20명 (전열 6개 근접 120명, 후열 4개 궁수 80명)
            for (int g = 0; g < 10; g++)
            {
                bool ranged = g >= 6;
                GameObject prefab = ranged ? enemyRanged : enemyMelee;
                float xCenter = ranged ? (g - 7.5f) * 24f : (g - 2.5f) * 18f;
                float zCenter = ranged ? 34f : 22f;
                SpawnBlock(prefab, $"Enemy_G{g + 1}_", 20, 5,
                    new Vector3(xCenter, 0f, zCenter), 180f, enemyAi, unitsParent);
            }

            // 1인칭 플레이어 + 탑다운 오버뷰 카메라
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0f, -15f);
            var fpCameraObj = new GameObject("FirstPersonCamera");
            fpCameraObj.SetActive(false);
            fpCameraObj.tag = "MainCamera";
            var fpCamera = fpCameraObj.AddComponent<Camera>();
            fpCamera.nearClipPlane = 0.05f;
            fpCamera.farClipPlane = 250f;
            fpCameraObj.AddComponent<AudioListener>();
            var view = fpCameraObj.AddComponent<FirstPersonCamera>();
            view.Configure(player.transform, controls);
            player.AddComponent<PlayerInputController>().Configure(inputActions, view, controls);
            player.SetActive(true);
            fpCameraObj.SetActive(true);

            var tdCameraObj = new GameObject("TopDownOverviewCamera");
            tdCameraObj.tag = "MainCamera";
            var tdCamera = tdCameraObj.AddComponent<Camera>();
            tdCamera.orthographic = true;
            tdCamera.orthographicSize = 55f;
            tdCamera.nearClipPlane = 0.1f;
            tdCamera.farClipPlane = 250f;
            tdCameraObj.transform.SetPositionAndRotation(new Vector3(0f, 80f, -20f), Quaternion.Euler(70f, 0f, 0f));
            tdCameraObj.SetActive(false);

            var commandObject = new GameObject("SquadCommand");
            commandObject.SetActive(false);
            var center = commandObject.AddComponent<SquadCommandCenter>();
            center.Configure(squadConfig, command, rosters);
            var input = commandObject.AddComponent<SquadCommandInput>();
            input.Configure(center, fpCamera);
            SquadCommandSetup.CreateHud(center, input,
                "\n[200v200 전용] M: 전군 돌격 | H: 전군 대기 | Tab: 탑다운 뷰 | V: LOD 색상 링 | L: LOD 토글");
            SquadCommandSetup.AddMarkers(center);
            commandObject.SetActive(true);

            CreateDebugMonitor(center, new[] { allyAi, enemyAi }, fpCamera, tdCamera, lod0Mat, lod1Mat, lod2Mat, sleepMat);

            // 아군과 적군 유닛에 새로 저장된 AI 에셋을 다시 한 번 확실히 연결한다.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (UnitCombatAI ai in root.GetComponentsInChildren<UnitCombatAI>(true))
                {
                    var actor = ai.GetComponent<CharacterActor>();
                    if (actor == null) continue;
                    ai.Configure(actor.Team == CombatTeam.Allied ? allyAi : enemyAi);
                    EditorUtility.SetDirty(ai);
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        static void CreateDebugMonitor(
            SquadCommandCenter center,
            UnitAISettings[] aiSettings,
            Camera fpCamera,
            Camera tdCamera,
            Material lod0,
            Material lod1,
            Material lod2,
            Material sleep)
        {
            var canvasObj = new GameObject("LargeBattleDebugHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObj.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-16f, -16f);
            panelRect.sizeDelta = new Vector2(680f, 145f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.82f);
            bg.raycastTarget = false;

            var textObj = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panel.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);
            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 15f;
            tmp.color = new Color(0.95f, 0.97f, 1f);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/NotoSansKR SDF.asset");
            if (font == null)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            tmp.font = font;

            var monitor = canvasObj.AddComponent<LargeBattleDebugHud>();
            monitor.Configure(tmp, center, aiSettings, fpCamera, tdCamera, lod0, lod1, lod2, sleep);
        }

        static UnitCombatAI[] SpawnBlock(
            GameObject prefab,
            string prefix,
            int count,
            int columns,
            Vector3 center,
            float yaw,
            UnitAISettings settings,
            Transform parent)
        {
            var units = new UnitCombatAI[count];
            int rows = Mathf.CeilToInt((float)count / columns);
            const float spacing = 1.6f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rot * Vector3.right;
            Vector3 back = rot * Vector3.back;

            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float ox = (col - (columns - 1) * 0.5f) * spacing;
                float oz = (row - (rows - 1) * 0.5f) * spacing;
                Vector3 pos = center + right * ox + back * oz;

                var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                unit.name = prefix + (i + 1);
                unit.transform.SetPositionAndRotation(pos, rot);
                var ai = unit.GetComponent<UnitCombatAI>();
                var serialized = new SerializedObject(ai);
                serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                unit.SetActive(true);
                units[i] = ai;
            }
            return units;
        }

        static T Required<T>(params string[] paths) where T : UnityEngine.Object
        {
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
            throw new InvalidOperationException("Missing asset: " + string.Join(" or ", paths));
        }

        static void Geometry(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            CombatPrototypeSetup.Box(name, position, scale, material).transform.SetParent(parent.transform, true);
        }
    }
}
