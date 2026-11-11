using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// G-0 실제 게임의 타이틀 씬과 게임 씬 틀을 Unity API로 만든다.
    /// 프로토타입 씬과 빌드 첫 씬은 건드리지 않고 두 씬을 빌드 목록 끝에 더한다.
    /// </summary>
    public static class GameFlowSetup
    {
        public const string TitlePath = "Assets/_Project/Scenes/Title.unity";
        public const string GamePath = "Assets/_Project/Scenes/Game.unity";
        const string Data = "Assets/_Project/Data/GameFlow";

        [MenuItem("ProjectNeo/Setup/Create Game Flow Scenes")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath) != null || AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) != null)
                throw new InvalidOperationException("Title or Game scene already exists; refusing to overwrite it.");
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP essentials before setup.");
            CombatPrototypeSetup.EnsureFolder(Data);
            var catalog = CombatPrototypeSetup.Save(SceneCatalog.Create("Title", "Game"), Data + "/SceneCatalog.asset");

            CreateTitle(catalog);
            CreateGame(catalog);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(TitlePath, true));
            scenes.Add(new EditorBuildSettingsScene(GamePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static void CreateTitle(SceneCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = MainCamera(Vector3.zero, Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f);

            var labels = new List<TMP_Text>(); var captions = new List<string>();
            Transform panel = Panel("TitleScreen", out GameObject root);
            var screen = root.AddComponent<TitleScreen>();
            screen.Configure(catalog);
            Label(panel, "ProjectNeo", 80f, 44f).alignment = TextAlignmentOptions.Center;
            ConstructionPrototypeSetup.Button(panel, "New Game", "새 게임", screen.StartGame, labels, captions);
            ConstructionPrototypeSetup.Button(panel, "Quit", "종료", screen.Quit, labels, captions);
            root.AddComponent<RuntimeSystemFont>().Configure(labels.ToArray(), captions.ToArray());
            EventSystem();
            EditorSceneManager.SaveScene(scene, TitlePath);
        }

        static void CreateGame(SceneCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            MainCamera(new Vector3(0f, 14f, -22f), Quaternion.Euler(30f, 0f, 0f));
            // 성과 전장이 들어올 자리. 지금은 빈 바닥만 둔다.
            CombatPrototypeSetup.Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f),
                CombatPrototypeSetup.MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.25f)));

            new GameObject("GameDirector").AddComponent<GameDirector>().Configure(catalog);

            var labels = new List<TMP_Text>(); var captions = new List<string>();
            Transform panel = Panel("GameFlowHUD", out GameObject root);
            var hud = root.AddComponent<GameFlowHud>();
            TMP_Text phase = Label(panel, "Phase", 44f, 22f);
            GameObject start = ConstructionPrototypeSetup.Button(panel, "Start battle", "전투 시작", hud.StartBattle, labels, captions);
            GameObject win = ConstructionPrototypeSetup.Button(panel, "Win (placeholder)", "승리 (자리표시)", hud.Win, labels, captions);
            GameObject lose = ConstructionPrototypeSetup.Button(panel, "Lose (placeholder)", "패배 (자리표시)", hud.Lose, labels, captions);
            GameObject next = ConstructionPrototypeSetup.Button(panel, "Next stage", "보상 받고 다음 스테이지", hud.NextStage, labels, captions);
            GameObject title = ConstructionPrototypeSetup.Button(panel, "Back to title", "타이틀로", hud.ReturnToTitle, labels, captions);
            var font = root.AddComponent<RuntimeSystemFont>();
            font.Configure(labels.ToArray(), captions.ToArray());
            hud.Configure(font, phase, start, win, lose, next, title);
            EventSystem();
            EditorSceneManager.SaveScene(scene, GamePath);
        }

        /// <summary>
        /// 게임 씬에 성벽 지휘 프로토타입의 성·건설·플레이어·부대를 옮겨 넣고 흐름 단계에 연결한다.
        /// 프로토타입 씬은 읽기만 하고 저장하지 않는다. 이미 성이 들어 있으면 중단한다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Add Castle To Game Scene")]
        public static void AddCastle()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            if (FindAll<ConstructionWorld>(game).Count > 0) throw new InvalidOperationException("Game scene already has a castle.");
            Scene source = EditorSceneManager.OpenScene(WallCommandSetup.ScenePath, OpenSceneMode.Additive);

            SceneManager.SetActiveScene(source);
            var ambientMode = RenderSettings.ambientMode; Color ambientLight = RenderSettings.ambientLight;
            Material skybox = RenderSettings.skybox; float ambientIntensity = RenderSettings.ambientIntensity;
            SceneManager.SetActiveScene(game);
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambientLight;
            RenderSettings.skybox = skybox; RenderSettings.ambientIntensity = ambientIntensity;

            // 프로토타입이 자기 조명·카메라·EventSystem을 가져오므로 빈 틀의 것은 지운다.
            foreach (GameObject root in game.GetRootGameObjects())
                if (root.name == "Main Camera" || root.name == "Sun" || root.name == "Ground" || root.name == "EventSystem")
                    UnityEngine.Object.DestroyImmediate(root);

            var sandbox = FindAll<WallCommandSandbox>(source)[0];
            var sandboxData = new SerializedObject(sandbox);
            SerializedProperty actorList = sandboxData.FindProperty("actors");
            var actors = new GameObject[actorList.arraySize];
            for (int i = 0; i < actors.Length; i++) actors[i] = (GameObject)actorList.GetArrayElementAtIndex(i).objectReferenceValue;
            foreach (GameObject root in source.GetRootGameObjects())
                if (root != sandbox.gameObject) SceneManager.MoveGameObjectToScene(root, game);
            EditorSceneManager.CloseScene(source, true);

            var buildHud = FindAll<ConstructionHud>(game)[0];
            var commandHud = FindAll<SquadCommandHud>(game)[0];
            new GameObject("GamePhaseView").AddComponent<GamePhaseView>().Configure(
                FindAll<ConstructionCameraController>(game)[0].GetComponent<Camera>(), FindAll<FirstPersonCamera>(game)[0],
                FindAll<PlacementController>(game)[0], FindAll<PlayerInputController>(game)[0],
                FindAll<SquadCommandInput>(game)[0], buildHud.gameObject, commandHud.gameObject, actors);

            HideTestControls(buildHud);
            ReplaceCaption(buildHud, "Tab: first-person commands", "Preparation: build, then start the battle",
                "준비 단계 · 성을 짓고 전투 시작을 누르세요");
            var commandData = new SerializedObject(commandHud);
            SerializedProperty help = commandData.FindProperty("helpText");
            help.stringValue = help.stringValue.Replace("\nTab 건설 시점으로 전환", "\nEsc로 커서를 풀면 위쪽 임시 승리·패배 버튼을 누를 수 있습니다");
            commandData.ApplyModifiedPropertiesWithoutUndo();

            // 단계 화면이 건설 화면 가운데를 가리지 않게 위쪽으로 옮긴다.
            var panel = (RectTransform)FindAll<GameFlowHud>(game)[0].transform.Find("Panel");
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -12f);
            panel.sizeDelta = new Vector2(300f, 0f);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        // 기본 성(임시 배치). 격자 24×26칸 가운데에 14×14칸 외벽을 두르고 북쪽 가운데에 메인 성문을 둔다.
        // 안뜰 12×12칸은 나중에 시설 건물이 들어갈 자리로 비워 둔다. 북쪽 바깥이 적이 오는 들판이다.
        const int GridWidth = 24, GridDepth = 26, MaxLevels = 3;
        const float CellSize = 3f;
        const int WallWest = 5, WallEast = 18, WallSouth = 4, WallNorth = 17, GateX = 11;

        /// <summary>
        /// 게임 씬의 격자를 넓히고 기본 성 배치와 유닛 시작 위치를 적용한다. 여러 번 실행해도 같은 결과다.
        /// 프로토타입과 공유하지 않는 게임 전용 격자·건설 카메라 에셋을 쓴다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Apply Default Castle To Game Scene")]
        public static void ApplyDefaultCastle()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            if (FindAll<ConstructionWorld>(game).Count == 0) throw new InvalidOperationException("Run Add Castle To Game Scene first.");
            const string pieces = "Assets/_Project/Data/ConstructionPrototype/";
            var wall = Required<BuildPieceDefinition>(pieces + "Wall.asset");
            var stairs = Required<BuildPieceDefinition>(pieces + "Stairs.asset");
            var gate = Required<BuildPieceDefinition>(pieces + "Gate.asset");
            var grid = CombatPrototypeSetup.Save(BuildGridDefinition.Create(GridWidth, GridDepth, CellSize), Data + "/Grid.asset");
            // 게임 씬은 층 쌓기를 쓴다. 프로토타입 격자는 1층 그대로다.
            var gridData = new SerializedObject(grid);
            gridData.FindProperty("maxLevels").intValue = MaxLevels;
            gridData.ApplyModifiedPropertiesWithoutUndo();
            var cameraSettings = CombatPrototypeSetup.Save(BuildCameraSettings.Create(), Data + "/BuildCamera.asset");
            var cameraData = new SerializedObject(cameraSettings);
            cameraData.FindProperty("orthographicSize").floatValue = 32f;
            cameraData.FindProperty("distance").floatValue = 90f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();

            var seeds = new List<InitialBuildPiece>();
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(GateX, WallNorth), fixedGate = true });
            for (int x = WallWest; x <= WallEast; x++)
            {
                if (x != GateX && x != GateX + 1) seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallNorth) });
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallSouth) });
            }
            for (int z = WallSouth + 1; z < WallNorth; z++)
            {
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(WallWest, z) });
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(WallEast, z) });
            }
            // 계단은 안뜰 쪽에 둔다. 회전 0은 북쪽, 회전 2는 남쪽 성벽으로 올라간다.
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(8, WallNorth - 2) });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(15, WallNorth - 2) });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(GateX, WallSouth + 1), rotation = 2 });

            ConstructionWorld world = FindAll<ConstructionWorld>(game)[0];
            Transform geometry = world.transform.Find("Geometry");
            world.Configure(grid, geometry, geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), seeds.ToArray());
            EditorUtility.SetDirty(world);
            float width = GridWidth * CellSize, depth = GridDepth * CellSize;
            Vector3 origin = new Vector3(-width * 0.5f, 0f, -depth * 0.5f);
            world.transform.position = origin;
            Transform ground = geometry.Find("Ground");
            ground.localScale = new Vector3(width, 1f, depth);
            ground.localPosition = new Vector3(width * 0.5f, -0.5f, depth * 0.5f);

            var orbit = FindAll<ConstructionCameraController>(game)[0];
            orbit.Configure(world, cameraSettings);
            EditorUtility.SetDirty(orbit);
            var buildCamera = orbit.GetComponent<Camera>();
            buildCamera.orthographicSize = cameraSettings.OrthographicSize;
            buildCamera.transform.rotation = Quaternion.Euler(cameraSettings.Pitch, cameraSettings.InitialYaw, 0f);
            buildCamera.transform.position = -(buildCamera.transform.forward * cameraSettings.Distance);

            // 부대는 계단 아래와 안뜰 가운데, 플레이어는 성문 위에서 바깥(북쪽)을 본다.
            Vector3[] squadCells = { new Vector3(8.5f, 0f, 13.5f), new Vector3(15.5f, 0f, 13.5f), new Vector3(12f, 0f, 11.5f) };
            foreach (GameObject root in game.GetRootGameObjects())
            {
                for (int squad = 0; squad < squadCells.Length; squad++)
                    for (int i = 0; i < 4; i++)
                        if (root.name == "Squad" + (squad + 1) + "_" + (i + 1))
                            Place(root.transform, origin + squadCells[squad] * CellSize
                                + new Vector3((i % 2 - 0.5f) * 1.1f, 0f, -(i / 2) * 1.1f), 0f);
                if (root.name == "Player")
                    Place(root.transform, origin + new Vector3((GateX + 1) * CellSize, gate.Height, (WallNorth + 0.5f) * CellSize), 0f);
            }

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        static void Place(Transform target, Vector3 position, float yaw)
        {
            target.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static T Required<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);

        // 프로토타입 전용 시험 버튼(유닛 이동·발밑 파괴·무기 받침 파괴)은 실제 게임 화면에서 숨긴다.
        static void HideTestControls(ConstructionHud hud)
        {
            foreach (Transform child in hud.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name;
                var label = child.GetComponent<TMP_Text>();
                bool testButton = name == "Button_Walk onto wall" || name == "Button_Break support" || name == "Button_Break weapon support";
                bool testLabel = name == "Label" && label != null
                    && (label.text == "MOVEMENT / COLLAPSE TEST" || label.text == "Unit health");
                if (testButton || testLabel) child.gameObject.SetActive(false);
            }
        }

        static void ReplaceCaption(ConstructionHud hud, string oldText, string newText, string newCaption)
        {
            var data = new SerializedObject(hud);
            SerializedProperty labels = data.FindProperty("captionLabels");
            SerializedProperty captions = data.FindProperty("captions");
            for (int i = 0; i < labels.arraySize; i++)
            {
                var label = labels.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
                if (label == null || label.text != oldText) continue;
                label.text = newText;
                EditorUtility.SetDirty(label);
                captions.GetArrayElementAtIndex(i).stringValue = newCaption;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        static Camera MainCamera(Vector3 position, Quaternion rotation)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        // 화면 가운데에 세로로 쌓이는 패널을 가진 캔버스.
        static Transform Panel(string name, out GameObject root)
        {
            root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(380f, 0f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.12f, 0.85f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20); layout.spacing = 10f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel.transform;
        }

        static TMP_Text Label(Transform parent, string text, float height, float size)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredHeight = height;
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size;
            label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return label;
        }

        static void EventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
