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
            var font = root.AddComponent<RuntimeSystemFont>();
            font.Configure(labels.ToArray(), captions.ToArray());
            SaveSlots(panel, screen, font);
            EventSystem();
            EditorSceneManager.SaveScene(scene, TitlePath);
        }

        /// <summary>META-01 저장된 Title 씬에 저장 슬롯 3개와 삭제 버튼을 더한다. 다시 실행하면 같은 것을 새로 만든다.</summary>
        [MenuItem("ProjectNeo/Setup/Add Save Slots To Title Scene")]
        public static void ApplyTitleSaveSlots()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            TitleScreen screen = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                screen = screen != null ? screen : root.GetComponentInChildren<TitleScreen>(true);
            if (screen == null) throw new InvalidOperationException("Title scene has no TitleScreen.");
            Transform panel = screen.transform.Find("Panel") ?? throw new InvalidOperationException("TitleScreen has no Panel.");
            SaveSlots(panel, screen, screen.GetComponent<RuntimeSystemFont>());
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 제목 바로 아래에 슬롯 버튼 3개와 안내 글, 맨 아래 종료 버튼 위에 삭제 버튼을 둔다. 글자는 TitleScreen이 실행 중에 채운다.
        static void SaveSlots(Transform panel, TitleScreen screen, RuntimeSystemFont font)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.GetChild(i);
                if (child.name.StartsWith("Button_Slot ") || child.name == "Button_Delete slot" || child.name == "Label_SaveStatus"
                    || child.name == "Button_Continue")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var slotLabels = new TMP_Text[ProjectNeo.Meta.MetaSaveStore.SlotCount];
            for (int i = 0; i < slotLabels.Length; i++)
            {
                GameObject slot = ConstructionPrototypeSetup.Button(panel, "Slot " + (i + 1), null, null, null, null);
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(slot.GetComponent<Button>().onClick, screen.SelectSlot, i);
                slot.transform.SetSiblingIndex(1 + i);
                slotLabels[i] = slot.GetComponentInChildren<TMP_Text>();
            }
            TMP_Text status = Label(panel, string.Empty, 24f, 15f);
            status.name = "Label_SaveStatus";
            status.alignment = TextAlignmentOptions.Center;
            status.transform.SetSiblingIndex(1 + slotLabels.Length);

            GameObject delete = ConstructionPrototypeSetup.Button(panel, "Delete slot", null, screen.DeleteSelectedSlot, null, null);
            delete.GetComponent<Image>().color = new Color(0.32f, 0.16f, 0.16f);
            Transform quit = panel.Find("Button_Quit");
            if (quit != null) delete.transform.SetSiblingIndex(quit.GetSiblingIndex());

            screen.ConfigureSlots(slotLabels, delete.GetComponentInChildren<TMP_Text>(), status, font);

            // 이어 하기(FLOW-02)는 새 게임 바로 위에 둔다. 글자와 누를 수 있는지는 TitleScreen이 실행 중에 정한다.
            GameObject resume = ConstructionPrototypeSetup.Button(panel, "Continue", null, screen.ContinueGame, null, null);
            Transform newGame = panel.Find("Button_New Game");
            if (newGame != null) resume.transform.SetSiblingIndex(newGame.GetSiblingIndex());
            screen.ConfigureContinue(resume.GetComponent<Button>(), resume.GetComponentInChildren<TMP_Text>());
        }

        static void CreateGame(SceneCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
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

        // 지면 240×300m, 성 96×96m, 안뜰 90×90m. 북쪽 외벽에서 지면 끝까지 120m다.
        const int GridWidth = 80, GridDepth = 100, MaxLevels = 3;
        const float CellSize = 3f;
        const int WallWest = 24, WallEast = 55, WallSouth = 28, WallNorth = 59, GateX = 39;

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
            gridData.FindProperty("width").intValue = GridWidth;
            gridData.FindProperty("depth").intValue = GridDepth;
            gridData.FindProperty("cellSize").floatValue = CellSize;
            gridData.FindProperty("maxLevels").intValue = MaxLevels;
            gridData.ApplyModifiedPropertiesWithoutUndo();
            var cameraSettings = CombatPrototypeSetup.Save(BuildCameraSettings.Create(), Data + "/BuildCamera.asset");
            var cameraData = new SerializedObject(cameraSettings);
            cameraData.FindProperty("orthographicSize").floatValue = 55f;
            cameraData.FindProperty("distance").floatValue = 320f;
            cameraData.FindProperty("navigationEnabled").boolValue = true;
            cameraData.FindProperty("initialFocus").vector2Value = new Vector2(0.5f, 0.44f);
            cameraData.FindProperty("minZoom").floatValue = 20f;
            cameraData.FindProperty("maxZoom").floatValue = 180f;
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
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(WallWest + 3, WallNorth - 1) });
            seeds.Add(new InitialBuildPiece { definition = stairs, cell = new Vector2Int(WallEast - 3, WallNorth - 1) });
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
            ApplyCourtyard(geometry);

            var orbit = FindAll<ConstructionCameraController>(game)[0];
            orbit.Configure(world, cameraSettings);
            EditorUtility.SetDirty(orbit);
            var buildCamera = orbit.GetComponent<Camera>();
            buildCamera.orthographicSize = cameraSettings.OrthographicSize;
            buildCamera.farClipPlane = 1000f;
            buildCamera.transform.rotation = Quaternion.Euler(cameraSettings.Pitch, cameraSettings.InitialYaw, 0f);
            buildCamera.transform.position = world.transform.TransformPoint(new Vector3(width * 0.5f, 0f, depth * 0.44f))
                - buildCamera.transform.forward * cameraSettings.Distance;
            FindAll<FirstPersonCamera>(game)[0].GetComponent<Camera>().farClipPlane = 1000f;

            // 부대는 계단 아래와 안뜰 가운데, 플레이어는 안뜰 남쪽에서 북쪽을 본다. 성문 위 건설 공간을 비운다.
            Vector3[] squadCells = { new Vector3(35f, 0f, 54f), new Vector3(45f, 0f, 54f), new Vector3(40f, 0f, 49f) };
            foreach (GameObject root in game.GetRootGameObjects())
            {
                for (int squad = 0; squad < squadCells.Length; squad++)
                    for (int i = 0; i < 4; i++)
                        if (root.name == "Squad" + (squad + 1) + "_" + (i + 1))
                            Place(root.transform, origin + squadCells[squad] * CellSize
                                + new Vector3((i % 2 - 0.5f) * 1.1f, 0f, -(i / 2) * 1.1f), 0f);
                if (root.name == "Player")
                    Place(root.transform, origin + new Vector3(40f * CellSize, 0f, 34f * CellSize), 0f);
            }

            AddNavigationHelp(FindAll<ConstructionHud>(game)[0]);
            ConfigurePreparationInteraction(game);
            GameBattleSetup.ConfigureScene(game);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ProjectNeo/Setup/Enable Preparation Interaction")]
        public static void EnablePreparationInteraction()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            ConfigurePreparationInteraction(game);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePreparationInteraction(Scene game)
        {
            var world = FindAll<ConstructionWorld>(game)[0];
            var view = FindAll<GamePhaseView>(game)[0];
            var player = FindAll<PlayerInputController>(game)[0];
            var camera = FindAll<FirstPersonCamera>(game)[0];
            Transform building = world.transform.Find("Geometry/CourtyardLayout/Construction");
            if (building == null) throw new InvalidOperationException("Apply Default Castle first to add the construction station.");
            var settings = CombatPrototypeSetup.Save(PreparationInteractionSettings.Create(), Data + "/Interaction.asset");
            var interaction = view.GetComponent<PreparationInteraction>();
            if (interaction == null) interaction = view.gameObject.AddComponent<PreparationInteraction>();
            interaction.Configure(view, camera.GetComponent<Camera>(), player.GetComponent<ProjectNeo.Combat.CharacterActor>(), settings);
            EditorUtility.SetDirty(interaction);
            var station = building.GetComponent<ConstructionStation>();
            if (station == null) station = building.gameObject.AddComponent<ConstructionStation>();
            station.Configure(view);
            EditorUtility.SetDirty(station);

            var hud = FindAll<GameFlowHud>(game)[0];
            Transform prompt = hud.transform.Find("InteractionPrompt");
            TMP_Text hint;
            if (prompt == null)
            {
                var root = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                root.transform.SetParent(hud.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 64f); rect.sizeDelta = new Vector2(760f, 64f);
                var background = root.GetComponent<UnityEngine.UI.Image>();
                background.color = new Color(0.05f, 0.08f, 0.1f, 0.8f);
                hint = Label(root.transform, "WASD move - Visit the construction station and press F", 48f, 20f);
                hint.alignment = TextAlignmentOptions.Center;
                hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one;
                hint.rectTransform.offsetMin = new Vector2(12f, 4f); hint.rectTransform.offsetMax = new Vector2(-12f, -4f);
            }
            else hint = prompt.GetComponentInChildren<TMP_Text>(true);
            // 안내 패널 위에서도 건설 카메라 입력이 월드로 통과하지 않게 한다.
            hint.transform.parent.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Transform reticle = hud.transform.Find("ExplorationReticle");
            if (reticle == null)
            {
                TMP_Text dot = Label(hud.transform, "+", 28f, 22f);
                dot.name = "ExplorationReticle";
                dot.alignment = TextAlignmentOptions.Center;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.anchoredPosition = Vector2.zero; dot.rectTransform.sizeDelta = new Vector2(28f, 28f);
                reticle = dot.transform;
            }
            hud.ConfigureInteraction(view, interaction, hint, reticle.gameObject);
            EditorUtility.SetDirty(hud);
        }

        static void ApplyCourtyard(Transform geometry)
        {
            Transform old = geometry.Find("CourtyardLayout");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var layout = new GameObject("CourtyardLayout");
            layout.transform.SetParent(geometry, false);
            Material road = CombatPrototypeSetup.MaterialAsset("CourtyardRoad", new Color(0.43f, 0.39f, 0.31f));
            Material plaster = CombatPrototypeSetup.MaterialAsset("FacilityPlaster", new Color(0.73f, 0.65f, 0.48f));
            Material roof = CombatPrototypeSetup.MaterialAsset("FacilityRoof", new Color(0.28f, 0.32f, 0.43f));
            Material plaza = CombatPrototypeSetup.MaterialAsset("MusterGround", new Color(0.3f, 0.35f, 0.25f));
            Decoration(layout.transform, "MainAvenue", new Vector3(120f, 0.035f, 132f), new Vector3(9f, 0.04f, 90f), road);
            Decoration(layout.transform, "CrossAvenue", new Vector3(120f, 0.035f, 132f), new Vector3(90f, 0.04f, 6f), road);
            Decoration(layout.transform, "MusterGround", new Vector3(120f, 0.03f, 157.5f), new Vector3(54f, 0.04f, 27f), plaza);
            var labels = new List<TMP_Text>(); var captions = new List<string>();
            Facility(layout.transform, "Tavern", "주점", new Vector3(91.5f, 0f, 112.5f), new Vector3(15f, 7f, 12f), plaster, roof, labels, captions);
            Facility(layout.transform, "Shop", "상점", new Vector3(91.5f, 0f, 153f), new Vector3(12f, 6f, 12f), plaster, roof, labels, captions);
            Facility(layout.transform, "Training", "훈련장", new Vector3(91.5f, 0f, 94.5f), new Vector3(15f, 5f, 9f), plaster, roof, labels, captions);
            Facility(layout.transform, "Smithy", "대장간", new Vector3(148.5f, 0f, 112.5f), new Vector3(15f, 7f, 12f), plaster, roof, labels, captions);
            Facility(layout.transform, "Construction", "건설소", new Vector3(148.5f, 0f, 153f), new Vector3(12f, 6f, 12f), plaster, roof, labels, captions);
            layout.AddComponent<RuntimeSystemFont>().Configure(labels.ToArray(), captions.ToArray());
        }

        static void Decoration(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = CombatPrototypeSetup.Box(name, Vector3.zero, scale, material);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        static void Facility(Transform parent, string name, string caption, Vector3 position, Vector3 size,
            Material wallMaterial, Material roofMaterial, List<TMP_Text> labels, List<string> captions)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            // 충돌은 루트, 임시 외형은 Visual 자식. 상호작용·시설 기능은 후속 연결이다.
            var body = root.AddComponent<BoxCollider>();
            body.center = Vector3.up * (size.y * 0.5f); body.size = size;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Decoration(visual, "Walls", Vector3.up * (size.y * 0.5f), size, wallMaterial);
            Decoration(visual, "Roof", Vector3.up * (size.y + 0.6f), new Vector3(size.x + 1.5f, 1.2f, size.z + 1.5f), roofMaterial);
            for (int i = 0; i < 2; i++)
            {
                var label = new GameObject(i == 0 ? "FrontSign" : "RoofSign").AddComponent<TextMeshPro>();
                label.transform.SetParent(visual, false);
                label.transform.localPosition = i == 0 ? new Vector3(0f, size.y * 0.65f, -size.z * 0.5f - 0.05f)
                    : new Vector3(0f, size.y + 1.25f, 0f);
                label.transform.localRotation = i == 0 ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                label.rectTransform.sizeDelta = new Vector2(size.x + 2f, 3f);
                label.fontSize = 16f; label.alignment = TextAlignmentOptions.Center; label.text = name;
                labels.Add(label); captions.Add(caption);
            }
        }

        static void AddNavigationHelp(ConstructionHud hud)
        {
            var data = new SerializedObject(hud);
            var captions = data.FindProperty("captions");
            for (int i = 0; i < captions.arraySize; i++)
            {
                var caption = captions.GetArrayElementAtIndex(i);
                if (!caption.stringValue.StartsWith("준비 단계")) continue;
                caption.stringValue = "준비 단계 · 중클릭 이동 / 휠 확대·축소 / Home 초기화";
            }
            data.ApplyModifiedPropertiesWithoutUndo();
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
