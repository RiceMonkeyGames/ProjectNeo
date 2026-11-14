using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    public static class ConstructionPrototypeSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/ConstructionPrototype.unity";
        const string Data = "Assets/_Project/Data/ConstructionPrototype";
        const string Prefabs = "Assets/_Project/Prefabs/ConstructionPrototype";
        const string Art = "Assets/_Project/Art/Placeholder";

        [MenuItem("ProjectNeo/Setup/Create Construction Prototype")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("ConstructionPrototype already exists.");
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP essentials before setup.");
            CombatPrototypeSetup.EnsureFolder(Data); CombatPrototypeSetup.EnsureFolder(Prefabs);
            var grid = CombatPrototypeSetup.Save(BuildGridDefinition.Create(), Data + "/Grid.asset");
            var fall = CombatPrototypeSetup.Save(FallSettings.Create(), Data + "/Fall.asset");
            var cameraSettings = CombatPrototypeSetup.Save(BuildCameraSettings.Create(), Data + "/Camera.asset");
            Material stone = CombatPrototypeSetup.MaterialAsset("ConstructionStone", new Color(0.58f, 0.66f, 0.75f));
            Material gateMaterial = CombatPrototypeSetup.MaterialAsset("ConstructionGate", new Color(0.63f, 0.44f, 0.24f));
            Material groundMaterial = CombatPrototypeSetup.MaterialAsset("ConstructionGround", new Color(0.23f, 0.34f, 0.26f));
            Material gridMaterial = CombatPrototypeSetup.MaterialAsset("ConstructionGrid", new Color(0.48f, 0.65f, 0.52f));
            Material ghostMaterial = GhostMaterial();
            var preview = CombatPrototypeSetup.Save(PlacementViewSettings.Create(ghostMaterial), Data + "/Preview.asset");
            var wall = CombatPrototypeSetup.Save(BuildPieceDefinition.Create(BuildPieceKind.Wall, "성벽", Vector2Int.one,
                prefab: PiecePrefab("Wall", BuildPieceKind.Wall, stone),
                mountPoints: new[] { new BuildMountPoint("top", Vector3.up) }), Data + "/Wall.asset");
            var stairs = CombatPrototypeSetup.Save(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "계단", Vector2Int.one,
                prefab: PiecePrefab("Stairs", BuildPieceKind.Stairs, stone)), Data + "/Stairs.asset");
            var gate = CombatPrototypeSetup.Save(BuildPieceDefinition.Create(BuildPieceKind.Gate, "메인 성문", new Vector2Int(2, 1),
                maxHealth: 300f, prefab: PiecePrefab("MainGate", BuildPieceKind.Gate, gateMaterial)), Data + "/Gate.asset");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.7f, 0.75f, 0.85f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var worldObject = new GameObject("ConstructionWorld");
            worldObject.SetActive(false);
            worldObject.transform.position = new Vector3(-grid.Width * grid.CellSize * 0.5f, 0f, -grid.Depth * grid.CellSize * 0.5f);
            var world = worldObject.AddComponent<ConstructionWorld>();
            var geometry = new GameObject("Geometry"); geometry.transform.SetParent(worldObject.transform, false);
            var ground = CombatPrototypeSetup.Box("Ground", Vector3.zero,
                new Vector3(grid.Width * grid.CellSize, 1f, grid.Depth * grid.CellSize), groundMaterial);
            ground.transform.SetParent(geometry.transform, false);
            ground.transform.localPosition = new Vector3(grid.Width * grid.CellSize * 0.5f, -0.5f, grid.Depth * grid.CellSize * 0.5f);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            var seeds = new List<InitialBuildPiece>();
            foreach (int x in new[] { 4, 5, 6, 9, 10 }) seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, 8) });
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(7, 8), fixedGate = true });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(5, 7) });
            world.Configure(grid, geometry.transform, surface, seeds.ToArray());
            var lines = new GameObject("GridLines"); lines.transform.SetParent(worldObject.transform, false);
            lines.AddComponent<BuildGridView>().Configure(world, gridMaterial);

            var camera = new GameObject("ConstructionCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = cameraSettings.OrthographicSize;
            camera.transform.rotation = Quaternion.Euler(cameraSettings.Pitch, cameraSettings.InitialYaw, 0f);
            camera.transform.position = -(camera.transform.forward * cameraSettings.Distance);
            camera.gameObject.AddComponent<ConstructionCameraController>().Configure(world, cameraSettings);
            camera.gameObject.AddComponent<AudioListener>();
            var controller = new GameObject("PlacementController").AddComponent<PlacementController>();
            controller.Configure(world, camera, preview);
            var hero = CombatPrototypeSetup.Save(HeroDefinition.Create("construction_test_unit", true, 0,
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.MoveSpeed, 4f)), Data + "/TestUnit.asset");
            var profile = CombatPrototypeSetup.Save(CombatProfile.Create(), Data + "/UnitBody.asset");
            var unitObject = CombatPrototypeSetup.Actor("TestUnit", hero, profile,
                CombatPrototypeSetup.MaterialAsset("Player", new Color(0.12f, 0.48f, 0.9f)),
                CombatPrototypeSetup.MaterialAsset("Health", new Color(0.2f, 0.9f, 0.35f)),
                CombatPrototypeSetup.MaterialAsset("Attack", new Color(1f, 0.75f, 0.1f)), false);
            unitObject.GetComponent<CharacterActor>().SetTeam(CombatTeam.Allied);
            unitObject.transform.position = worldObject.transform.position + new Vector3(5.5f * grid.CellSize, 0.1f, 4.5f * grid.CellSize);
            var agent = unitObject.AddComponent<NavMeshAgent>();
            agent.radius = profile.Radius; agent.height = profile.Height; agent.speed = 4f; agent.acceleration = 24f;
            unitObject.AddComponent<UnitFallController>().Configure(world, fall);
            var sandbox = new GameObject("ConstructionSandbox").AddComponent<ConstructionSandbox>();
            sandbox.Configure(world, unitObject.GetComponent<CharacterActor>());
            CreateHud(controller, sandbox, wall, stairs);
            SiegeWeaponSetup.AddToOpenScene(scene);
            worldObject.SetActive(true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static Material GhostMaterial()
        {
            string path = Art + "/ConstructionGhost.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.12f, 1f, 0.55f, 0.45f));
            return CombatPrototypeSetup.Save(material, path);
        }

        static GameObject PiecePrefab(string name, BuildPieceKind kind, Material material)
        {
            string path = Prefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name); var placed = root.AddComponent<PlacedStructure>();
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
            if (kind == BuildPieceKind.Stairs)
            {
                Mesh mesh = RampMesh();
                root.AddComponent<MeshCollider>().sharedMesh = mesh;
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            else if (kind == BuildPieceKind.Gate)
                BuildGateParts(root, placed, visual.transform,
                    CombatPrototypeSetup.MaterialAsset("ConstructionStone", new Color(0.58f, 0.66f, 0.75f)), material);
            else Part(root.transform, visual.transform, "Body", Vector3.up * 0.5f, Vector3.one, material);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        public static void BuildGateParts(GameObject root, PlacedStructure placed, Transform visual,
            Material archMaterial, Material doorMaterial)
        {
            // Stone arch colliders (indestructible frame + walkable top surface at y = 1.0).
            AddBoxCollider(root, new Vector3(-0.425f, 0.5f, 0f), new Vector3(0.15f, 1f, 1f));
            AddBoxCollider(root, new Vector3(0.425f, 0.5f, 0f), new Vector3(0.15f, 1f, 1f));
            AddBoxCollider(root, new Vector3(0f, 0.91f, 0f), new Vector3(0.7f, 0.18f, 1f));

            var arch = new GameObject("StoneArch");
            arch.transform.SetParent(visual, false);
            arch.AddComponent<MeshFilter>().sharedMesh = ConstructionGateArchMesh.EnsureAsset();
            arch.AddComponent<MeshRenderer>().sharedMaterial = archMaterial;

            // Wooden door (only this part breaks on gate destruction).
            BoxCollider doorCollider = AddBoxCollider(root, new Vector3(0f, 0.41f, 0f), new Vector3(0.7f, 0.82f, 0.18f));
            GameObject doorVisual = CombatPrototypeSetup.Box("Door", Vector3.zero, new Vector3(0.7f, 0.82f, 0.18f), doorMaterial);
            UnityEngine.Object.DestroyImmediate(doorVisual.GetComponent<Collider>());
            doorVisual.transform.SetParent(visual, false);
            doorVisual.transform.localPosition = new Vector3(0f, 0.41f, 0f);
            placed.SetDoorParts(doorCollider, doorVisual);
        }

        [MenuItem("ProjectNeo/Setup/Split Gate Arch And Door")]
        public static string RebuildMainGatePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string path = Prefabs + "/MainGate.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null) throw new InvalidOperationException("MainGate prefab not found: " + path);
            Material archMaterial = CombatPrototypeSetup.MaterialAsset("ConstructionStone", new Color(0.58f, 0.66f, 0.75f));
            Material doorMaterial = CombatPrototypeSetup.MaterialAsset("ConstructionGate", new Color(0.63f, 0.44f, 0.24f));

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                GameObject root = scope.prefabContentsRoot;
                foreach (Collider col in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(col);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var placed = root.GetComponent<PlacedStructure>() ?? root.AddComponent<PlacedStructure>();
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                BuildGateParts(root, placed, visual.transform, archMaterial, doorMaterial);
            }
            AssetDatabase.SaveAssets();
            return "Updated MainGate.prefab with StoneArch + Door.";
        }

        static BoxCollider AddBoxCollider(GameObject root, Vector3 center, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            return collider;
        }

        static void Part(Transform root, Transform visual, string name, Vector3 center, Vector3 size, Material material)
        {
            AddBoxCollider(root.gameObject, center, size);
            var body = CombatPrototypeSetup.Box(name, Vector3.zero, size, material);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false); body.transform.localPosition = center;
        }

        static Mesh RampMesh()
        {
            string path = ConstructionRampMesh.AssetPath;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
            var mesh = new Mesh();
            ConstructionRampMesh.Fill(mesh);
            return CombatPrototypeSetup.Save(mesh, path);
        }

        static void CreateHud(PlacementController controller, ConstructionSandbox sandbox,
            BuildPieceDefinition wall, BuildPieceDefinition stairs)
        {
            var root = new GameObject("ConstructionHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<ConstructionHud>();
            var panel = new GameObject("BuildPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 0.5f); rect.anchoredPosition = new Vector2(18f, 0f); rect.sizeDelta = new Vector2(248f, 650f);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.055f, 0.08f, 0.11f, 0.95f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 18, 18); layout.spacing = 9f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var labels = new List<TMP_Text>(); var captions = new List<string>();
            Label(panel.transform, "WALL BUILDER", 36f, 24f, "성벽 배치", labels, captions);
            Label(panel.transform, "Q/E: view, R: piece", 25f, 14f, "Q/E 시점 회전 · R 조각 회전", labels, captions);
            Button(panel.transform, "Wall", "성벽", hud.SelectWall, labels, captions);
            Button(panel.transform, "Stairs", "계단", hud.SelectStairs, labels, captions);
            Button(panel.transform, "Rotate (R)", "회전  R", hud.Rotate, labels, captions);
            Button(panel.transform, "Cancel", "배치 취소", hud.Cancel, labels, captions);
            TMP_Text selection = Label(panel.transform, "Choose a piece", 86f, 16f);
            Label(panel.transform, "Click: place / Right click: cancel", 42f, 14f,
                "좌클릭 설치 · 우클릭 취소\n초록: 설치 가능 / 빨강: 설치 불가", labels, captions);
            Label(panel.transform, "MOVEMENT / COLLAPSE TEST", 28f, 15f, "이동·붕괴 시험", labels, captions);
            Button(panel.transform, "Walk onto wall", "성벽 위로 이동", hud.WalkToWall, labels, captions);
            Button(panel.transform, "Break support", "발밑 조각 파괴", hud.CollapseUnderUnit, labels, captions);
            TMP_Text health = Label(panel.transform, "Unit health", 28f, 15f);
            TMP_Text status = Label(panel.transform, "Build walls and stairs.", 70f, 14f);
            hud.Configure(controller, sandbox, wall, stairs, selection, status, health, labels.ToArray(), captions.ToArray());
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static TMP_Text Label(Transform parent, string text, float height, float size,
            string caption = null, List<TMP_Text> labels = null, List<string> captions = null)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var label = root.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (caption != null) { labels.Add(label); captions.Add(caption); }
            return label;
        }

        internal static GameObject Button(Transform parent, string title, string caption, UnityAction action,
            List<TMP_Text> labels, List<string> captions)
        {
            var root = new GameObject("Button_" + title, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38f;
            var image = root.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(0.15f, 0.24f, 0.29f);
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            TMP_Text text = Label(root.transform, title, 38f, 17f, caption, labels, captions);
            var rect = text.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 0f); rect.offsetMax = new Vector2(-10f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return root;
        }
    }
}
