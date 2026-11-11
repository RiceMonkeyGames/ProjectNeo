using System;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>B-2 임시 씬과 에셋을 Unity API로 만든다. 기존 에셋은 보존한다.</summary>
    public static class CombatPrototypeSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/CombatPrototype.unity";
        const string Data = "Assets/_Project/Data/CombatPrototype";
        const string Art = "Assets/_Project/Art/Placeholder";
        const string Prefabs = "Assets/_Project/Prefabs/CombatPrototype";

        [MenuItem("ProjectNeo/Setup/Create Combat Prototype")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("CombatPrototype already exists; refusing to overwrite it.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before creating the prototype.");
            EnsureFolder(Data);
            EnsureFolder(Art);
            EnsureFolder(Prefabs);
            var hero = Save(HeroDefinition.Create("prototype_player", true, 3,
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 25f),
                new StatValue(StatType.AttackSpeed, 1.5f), new StatValue(StatType.MoveSpeed, 6f),
                new StatValue(StatType.AttackRange, 2.5f)), Data + "/Player.asset");
            var dummy = Save(HeroDefinition.Create("prototype_dummy", true, 0,
                new StatValue(StatType.MaxHealth, 100f)), Data + "/Dummy.asset");
            var profile = Save(CombatProfile.Create(), Data + "/CombatProfile.asset");
            var settings = Save(PlayerControlSettings.Create(), Data + "/PlayerControls.asset");
            Material blue = MaterialAsset("Player", new Color(0.12f, 0.48f, 0.9f));
            Material red = MaterialAsset("Dummy", new Color(0.85f, 0.25f, 0.16f));
            Material ground = MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.25f));
            Material stone = MaterialAsset("Stone", new Color(0.38f, 0.43f, 0.49f));
            Material green = MaterialAsset("Health", new Color(0.2f, 0.9f, 0.35f));
            Material yellow = MaterialAsset("Attack", new Color(1f, 0.75f, 0.1f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Box("Ground", new Vector3(0f, -0.5f, 5f), new Vector3(40f, 1f, 40f), ground);
            Box("NorthWall", new Vector3(0f, 1.5f, 25f), new Vector3(40f, 3f, 1f), stone);
            Box("SouthWall", new Vector3(0f, 1.5f, -15f), new Vector3(40f, 3f, 1f), stone);
            Box("WestWall", new Vector3(-20f, 1.5f, 5f), new Vector3(1f, 3f, 40f), stone);
            Box("EastWall", new Vector3(20f, 1.5f, 5f), new Vector3(1f, 3f, 40f), stone);
            Box("CameraAndAttackObstacle", new Vector3(6f, 1.5f, 6f), new Vector3(3f, 3f, 1f), stone);

            GameObject player = Actor("Player", hero, profile, blue, green, yellow, true);
            var cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.SetActive(false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 150f;
            cameraObject.AddComponent<AudioListener>();
            var view = cameraObject.AddComponent<FirstPersonCamera>();
            view.Configure(player.transform, settings);
            cameraObject.transform.SetPositionAndRotation(player.transform.position + Vector3.up * settings.CameraHeight,
                Quaternion.Euler(settings.InitialPitch, player.transform.eulerAngles.y, 0f));
            // 카메라는 씬 참조이므로 플레이어 프리팹에는 입력 컴포넌트를 넣지 않는다.
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Prefabs + "/Player.prefab");
            UnityEngine.Object.DestroyImmediate(player);
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            var input = player.AddComponent<PlayerInputController>();
            view.Configure(player.transform, settings);
            input.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Input/InputSystem_Actions.inputactions"), view, settings);
            player.SetActive(true);
            cameraObject.SetActive(true);

            GameObject training = Actor("TrainingDummy", dummy, profile, red, green, yellow, false);
            var dummyPrefab = PrefabUtility.SaveAsPrefabAsset(training, Prefabs + "/TrainingDummy.prefab");
            UnityEngine.Object.DestroyImmediate(training);
            Vector3[] positions = { new Vector3(0f, 0f, 3f), new Vector3(-4f, 0f, 7f), new Vector3(4f, 0f, 11f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                instance.name = "TrainingDummy_" + (i + 1);
                instance.transform.position = positions[i];
                instance.SetActive(true);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        internal static GameObject Actor(string name, HeroDefinition hero, CombatProfile profile,
            Material bodyMaterial, Material healthMaterial, Material attackMaterial, bool player)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(hero, profile, player ? CombatTeam.Allied : CombatTeam.Hostile);
            if (player)
            {
                var motor = root.AddComponent<CharacterController>();
                motor.height = profile.Height;
                motor.radius = profile.Radius;
                motor.center = Vector3.up * (profile.Height * 0.5f);
            }
            else
            {
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = profile.Height;
                collider.radius = profile.Radius;
                collider.center = Vector3.up * (profile.Height * 0.5f);
            }
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = Vector3.up * (profile.Height * 0.5f);
            body.transform.localScale = new Vector3(profile.Radius * 2f, profile.Height * 0.5f, profile.Radius * 2f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            var bar = Box("HealthBar", Vector3.zero, new Vector3(1f, 0.12f, 0.12f), healthMaterial);
            bar.transform.SetParent(visual.transform, false);
            bar.transform.localPosition = Vector3.up * (profile.Height + 0.35f);
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            var marker = Box("AttackMarker", Vector3.zero, new Vector3(0.18f, 0.18f, 1.5f), attackMaterial);
            marker.transform.SetParent(visual.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1f, 1f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            visual.AddComponent<PlaceholderCombatView>().Configure(actor, body.GetComponent<Renderer>(), bar.transform, marker);
            return root;
        }

        internal static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        internal static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { UnityEngine.Object.DestroyImmediate(value); return existing; }
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        internal static Material MaterialAsset(string name, Color color)
        {
            return Save(new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }, Art + "/" + name + ".mat");
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
