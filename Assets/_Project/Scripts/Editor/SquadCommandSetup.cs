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

namespace ProjectNeo.Editor
{
    /// <summary>B-5 부대 지휘 시험 씬을 Unity API로 만든다. 유닛·플레이어 프리팹은 기존 시험 씬의 것을 쓴다.</summary>
    public static class SquadCommandSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/SquadCommandPrototype.unity";
        const string Data = "Assets/_Project/Data/SquadCommandPrototype";
        const string Units = "Assets/_Project/Prefabs/UnitBattlePrototype/";

        [MenuItem("ProjectNeo/Setup/Create Squad Command Prototype")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("SquadCommandPrototype already exists; refusing to overwrite it.");
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP essentials before setup.");
            GameObject allyMelee = Required<GameObject>(Units + "allied_melee.prefab");
            GameObject allyRanged = Required<GameObject>(Units + "allied_ranged.prefab");
            GameObject enemyMelee = Required<GameObject>(Units + "hostile_melee.prefab");
            GameObject enemyRanged = Required<GameObject>(Units + "hostile_ranged.prefab");
            GameObject playerPrefab = Required<GameObject>("Assets/_Project/Prefabs/CombatPrototype/Player.prefab");
            var controls = Required<PlayerControlSettings>("Assets/_Project/Data/CombatPrototype/PlayerControls.asset");
            var inputActions = Required<InputActionAsset>("Assets/_Project/Input/InputSystem_Actions.inputactions");
            CombatPrototypeSetup.EnsureFolder(Data);
            var squadConfig = CombatPrototypeSetup.Save(SquadConfig.Create(4, 10), Data + "/SquadConfig.asset");
            var command = CombatPrototypeSetup.Save(SquadCommandSettings.Create(), Data + "/Command.asset");
            Material ground = CombatPrototypeSetup.MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.25f));
            Material stone = CombatPrototypeSetup.MaterialAsset("Stone", new Color(0.38f, 0.43f, 0.49f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var geometry = new GameObject("NavigationGeometry");
            Geometry(geometry, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f), ground);
            Geometry(geometry, "Obstacle", new Vector3(0f, 1.5f, 6f), new Vector3(6f, 3f, 2f), stone);
            Geometry(geometry, "NorthWall", new Vector3(0f, 1.5f, 35f), new Vector3(70f, 3f, 1f), stone);
            Geometry(geometry, "SouthWall", new Vector3(0f, 1.5f, -35f), new Vector3(70f, 3f, 1f), stone);
            Geometry(geometry, "WestWall", new Vector3(-35f, 1.5f, 0f), new Vector3(1f, 3f, 70f), stone);
            Geometry(geometry, "EastWall", new Vector3(35f, 1.5f, 0f), new Vector3(1f, 3f, 70f), stone);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, Data + "/Navigation.asset");

            var rosters = new SquadRoster[3];
            rosters[0].members = Row(allyMelee, "Squad1_", 5, new Vector3(-8f, 0f, -12f), 0f);
            rosters[1].members = Row(allyMelee, "Squad2_", 5, new Vector3(8f, 0f, -12f), 0f);
            rosters[2].members = Row(allyRanged, "Squad3_", 4, new Vector3(0f, 0f, -16f), 0f);
            Row(enemyMelee, "EnemyMelee_", 6, new Vector3(0f, 0f, 24f), 180f);
            Row(enemyRanged, "EnemyRanged_", 2, new Vector3(0f, 0f, 28f), 180f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0f, -22f);
            var cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.SetActive(false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f; camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();
            var view = cameraObject.AddComponent<FirstPersonCamera>();
            view.Configure(player.transform, controls);
            player.AddComponent<PlayerInputController>().Configure(inputActions, view, controls);
            player.SetActive(true);
            cameraObject.SetActive(true);

            var commandObject = new GameObject("SquadCommand");
            commandObject.SetActive(false);
            var center = commandObject.AddComponent<SquadCommandCenter>();
            center.Configure(squadConfig, command, rosters);
            var input = commandObject.AddComponent<SquadCommandInput>();
            input.Configure(center, camera);
            CreateHud(center, input);
            AddMarkers(center);
            commandObject.SetActive(true);
            ApplyVision(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            // 빌드 첫 씬은 바꾸지 않고 목록 끝에 더한다.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        // 아군은 공격 지시로 이동하다 이 거리 안의 적을 발견하면 싸운다.
        // 적은 가까이 올 때까지 서 있게 해서 이동·중지·공격 지시를 차례로 시험할 수 있게 한다.
        const float AllyVision = 15f;
        const float EnemyVision = 12f;

        // 팀별 시야 에셋을 씬의 유닛에 연결한다. 프리팹 인스턴스의 변경이 씬에 저장되도록 SerializedObject로 쓴다.
        static void ApplyVision(Scene scene)
        {
            var allyAi = CombatPrototypeSetup.Save(UnitAISettings.Create(AllyVision), Data + "/AllyAI.asset");
            var enemyAi = CombatPrototypeSetup.Save(UnitAISettings.Create(EnemyVision), Data + "/EnemyAI.asset");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var ai = root.GetComponent<UnitCombatAI>();
                var actor = root.GetComponent<CharacterActor>();
                if (ai == null || actor == null) continue;
                var serialized = new SerializedObject(ai);
                serialized.FindProperty("settings").objectReferenceValue = actor.Team == CombatTeam.Allied ? allyAi : enemyAi;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 선택한 부대와 목적지를 보여주는 임시 표시. 지휘 오브젝트의 Visual 자식에 둔다.
        internal static void AddMarkers(SquadCommandCenter center)
        {
            if (center.GetComponentInChildren<PlaceholderSquadMarkers>(true) != null) return;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(center.transform, false);
            visual.AddComponent<PlaceholderSquadMarkers>().Configure(center,
                CombatPrototypeSetup.MaterialAsset("SquadSelection", new Color(1f, 0.85f, 0.15f)),
                CombatPrototypeSetup.MaterialAsset("SquadDestination", new Color(0.2f, 0.95f, 1f)));
        }

        /// <summary>
        /// 이미 만든 지휘 씬에 아군 시야 에셋과 선택·목적지 표시를 더한다. 여러 번 실행해도 같은 결과다.
        /// 관전 전투 씬과 공유하던 AI 설정은 건드리지 않는다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Upgrade Squad Command Prototype")]
        public static void Upgrade()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var commandCenter = root.GetComponent<SquadCommandCenter>();
                if (commandCenter != null) AddMarkers(commandCenter);
            }
            ApplyVision(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }

        static void Geometry(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            CombatPrototypeSetup.Box(name, position, scale, material).transform.SetParent(parent.transform, true);
        }

        static UnitCombatAI[] Row(GameObject prefab, string name, int count, Vector3 center, float yaw)
        {
            var units = new UnitCombatAI[count];
            for (int i = 0; i < count; i++)
            {
                var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                unit.name = name + (i + 1);
                unit.transform.SetPositionAndRotation(center + Vector3.right * ((i - (count - 1) * 0.5f) * 1.6f),
                    Quaternion.Euler(0f, yaw, 0f));
                units[i] = unit.GetComponent<UnitCombatAI>();
                unit.SetActive(true);
            }
            return units;
        }

        internal static void CreateHud(SquadCommandCenter center, SquadCommandInput input, string extraHelp = "")
        {
            var root = new GameObject("SquadCommandHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 0.5f;
            TMP_Text squads = Label(root.transform, "Squads", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(360f, 170f), 20f);
            squads.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text help = Label(root.transform, "Help", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(520f, 110f), 16f);
            help.alignment = TextAlignmentOptions.BottomLeft;
            help.text = "F1-F4 select, ` all, 1 move, 2 attack, 3 hold";
            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            dot.transform.SetParent(root.transform, false);
            dot.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 6f);
            var image = dot.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f); image.raycastTarget = false;
            root.AddComponent<SquadCommandHud>().Configure(center, input, squads, help,
                "F1-F4: select squad - `: select all\n1: move to aim point - 2: attack - 3: hold\nWASD: move - Mouse: look - Click: attack - Esc: release cursor" + extraHelp);
        }

        static TMP_Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = root.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize; label.color = new Color(0.95f, 0.97f, 1f); label.raycastTarget = false;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return label;
        }
    }
}
