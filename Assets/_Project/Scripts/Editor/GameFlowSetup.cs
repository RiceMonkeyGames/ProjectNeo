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

            Transform panel = Panel("TitleScreen", out GameObject root);
            var screen = root.AddComponent<TitleScreen>();
            screen.Configure(catalog);
            Label(panel, "ProjectNeo", 80f, 44f).alignment = TextAlignmentOptions.Center;
            ConstructionPrototypeSetup.Button(panel, "New Game", screen.StartGame, "새 게임");
            ConstructionPrototypeSetup.Button(panel, "Quit", screen.Quit, "종료");
            SaveSlots(panel, screen);
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
            SaveSlots(panel, screen);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// META-06 저장된 Title 씬에 'Unlocks' 버튼과 런 사이 해금 화면(UI-06부터 시안 B 패널)을 더한다. 다시 실행하면 같은 것을 새로 만든다.
        /// 해금 트랙 에셋(META-07)이 먼저 있어야 한다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Add Unlock Screen To Title Scene (META-06)")]
        public static void ApplyTitleUnlockScreen()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            var unlockCatalog = AssetDatabase.LoadAssetAtPath<ProjectNeo.Meta.UnlockCatalog>(UnlockCatalogSetup.CatalogPath)
                ?? throw new InvalidOperationException("Unlock catalog is missing: " + UnlockCatalogSetup.CatalogPath);
            Scene scene = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            TitleScreen screen = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                screen = screen != null ? screen : root.GetComponentInChildren<TitleScreen>(true);
            if (screen == null) throw new InvalidOperationException("Title scene has no TitleScreen.");
            Transform panel = screen.transform.Find("Panel") ?? throw new InvalidOperationException("TitleScreen has no Panel.");

            Transform oldButton = panel.Find("Button_Unlocks");
            if (oldButton != null) UnityEngine.Object.DestroyImmediate(oldButton.gameObject);
            Transform oldOverlay = screen.transform.Find("UnlockOverlay");
            if (oldOverlay != null) UnityEngine.Object.DestroyImmediate(oldOverlay.gameObject);
            UnlockScreen unlocks = screen.GetComponent<UnlockScreen>();
            if (unlocks == null) unlocks = screen.gameObject.AddComponent<UnlockScreen>();

            // 'Unlocks' 버튼은 삭제 버튼 바로 위에 둔다.
            GameObject open = ConstructionPrototypeSetup.Button(panel, "Unlocks", screen.OpenUnlocks, "해금");
            Transform delete = panel.Find("Button_Delete slot");
            if (delete != null) open.transform.SetSiblingIndex(delete.GetSiblingIndex());

            // UI-06: 해금 화면은 시안 B 패널로 만든다(트랙 목록 + 항목 카드 + 무작위 해금).
            TitleUnlockScreenSetup.Build(screen, unlocks, unlockCatalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 제목 바로 아래에 슬롯 버튼 3개와 안내 글, 맨 아래 종료 버튼 위에 삭제 버튼을 둔다. 글자는 TitleScreen이 실행 중에 채운다.
        static void SaveSlots(Transform panel, TitleScreen screen)
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
                GameObject slot = ConstructionPrototypeSetup.Button(panel, "Slot " + (i + 1), null, "슬롯 " + (i + 1));
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(slot.GetComponent<Button>().onClick, screen.SelectSlot, i);
                slot.transform.SetSiblingIndex(1 + i);
                slotLabels[i] = slot.GetComponentInChildren<TMP_Text>();
            }
            TMP_Text status = Label(panel, string.Empty, 24f, 15f);
            status.name = "Label_SaveStatus";
            status.alignment = TextAlignmentOptions.Center;
            status.transform.SetSiblingIndex(1 + slotLabels.Length);

            GameObject delete = ConstructionPrototypeSetup.Button(panel, "Delete selected slot", screen.DeleteSelectedSlot, "고른 슬롯 삭제");
            delete.name = "Button_Delete slot";
            delete.GetComponent<Image>().color = new Color(0.32f, 0.16f, 0.16f);
            Transform quit = panel.Find("Button_Quit");
            if (quit != null) delete.transform.SetSiblingIndex(quit.GetSiblingIndex());

            screen.ConfigureSlots(slotLabels, delete.GetComponentInChildren<TMP_Text>(), status);

            // 이어 하기(FLOW-02)는 새 게임 바로 위에 둔다. 글자와 누를 수 있는지는 TitleScreen이 실행 중에 정한다.
            GameObject resume = ConstructionPrototypeSetup.Button(panel, "Continue", screen.ContinueGame, "이어 하기");
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

            Transform panel = Panel("GameFlowHUD", out GameObject root);
            var hud = root.AddComponent<GameFlowHud>();
            TMP_Text phase = Label(panel, "단계", 44f, 22f);
            GameObject start = ConstructionPrototypeSetup.Button(panel, "Start battle", hud.StartBattle, "전투 시작");
            GameObject win = ConstructionPrototypeSetup.Button(panel, "Win (placeholder)", hud.Win, "승리 (임시)");
            GameObject lose = ConstructionPrototypeSetup.Button(panel, "Lose (placeholder)", hud.Lose, "패배 (임시)");
            GameObject next = ConstructionPrototypeSetup.Button(panel, "Next stage", hud.NextStage, "다음 스테이지");
            GameObject title = ConstructionPrototypeSetup.Button(panel, "Back to title", hud.ReturnToTitle, "타이틀로");
            hud.Configure(phase, start, win, lose, next, title);
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
            ReplaceCaption(buildHud, "Tab: 1인칭 지휘", "준비: 건설한 뒤 전투를 시작하세요");
            var commandData = new SerializedObject(commandHud);
            var help = (TMP_Text)commandData.FindProperty("help").objectReferenceValue;
            help.text = help.text.Replace("\nTab: 건설 화면", "\nEsc: 커서를 풀어 임시 승리/패배 버튼 사용");
            EditorUtility.SetDirty(help);

            // 단계 화면이 건설 화면 가운데를 가리지 않게 위쪽으로 옮긴다.
            var panel = (RectTransform)FindAll<GameFlowHud>(game)[0].transform.Find("Panel");
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -12f);
            panel.sizeDelta = new Vector2(300f, 0f);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        // SIEGE-36: 7.5m 칸, 5m 층. 지면 46×53칸(345×397.5m), 성 26×26칸(195m), 안뜰 180m. 북쪽 외벽에서 지면 끝까지 120m다.
        // 서·동·남 여백은 SIEGE-18과 같다(서 10칸, 동 10칸, 남 11칸). 성문은 북쪽 줄 x 23 칸이라 성 가운데 선에서 동쪽으로 3.75m 비껴 있다.
        // 성벽·보루·계단·성문 칸은 3층까지, 4층은 망루 층만 쓴다(Q-59). 골짜기 금지, 계단은 땅 위에만.
        internal const int GridWidth = 46, GridDepth = 53, MaxLevels = 4;
        internal const float CellSize = 7.5f;
        internal const int WallWest = 10, WallEast = 35, WallSouth = 11, WallNorth = 36, GateX = 23;
        /// <summary>네 모서리의 2층 탑 한 변 칸 수(고리 모서리 칸과 안쪽 칸을 합친 2×2).</summary>
        internal const int CornerTowerSize = 2;
        /// <summary>안뜰 큰길(성문 가운데 선)과 가운데 광장의 지면 기준 m.</summary>
        internal const float AvenueX = (GateX + 0.5f) * CellSize, PlazaZ = 180f;
        /// <summary>적 출현 가운데 칸: 성문 줄에서 북쪽 외벽 바깥 13칸(외벽 바깥면에서 90m, SIEGE-18과 같은 거리).</summary>
        internal static readonly Vector2 SpawnCell = new Vector2(GateX, WallNorth + 13);

        /// <summary>
        /// 기본 성(SIEGE-36): 1층(5m) 외벽 고리, 성문 위에 고정 성벽 한 층을 얹은 2층 성문탑, 네 모서리의 2×2칸 2층 탑,
        /// 안뜰에서 성벽으로 오르는 땅 위 계단 여덟(각 변에 둘). 계단 회전 0 북, 1 동, 2 남, 3 서 성벽으로 올라간다.
        /// </summary>
        internal static InitialBuildPiece[] DefaultSeeds(BuildPieceDefinition wall, BuildPieceDefinition stairs, BuildPieceDefinition gate)
        {
            var seeds = new List<InitialBuildPiece>();
            seeds.Add(new InitialBuildPiece { definition = gate, cell = new Vector2Int(GateX, WallNorth), fixedGate = true });
            // 성문탑 2층(SIEGE-14, Q-59): 고정 조각이라 철거할 수 없다(무너짐은 예외).
            seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(GateX, WallNorth), fixedGate = true });
            for (int x = WallWest; x <= WallEast; x++)
            {
                if (x != GateX) seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallNorth) });
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(x, WallSouth) });
            }
            for (int z = WallSouth + 1; z < WallNorth; z++)
            {
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(WallWest, z) });
                seeds.Add(new InitialBuildPiece { definition = wall, cell = new Vector2Int(WallEast, z) });
            }
            // 모서리 탑: 고리 모서리 2×2칸을 2층으로 쌓는다. 안쪽 칸은 1층부터 새로 채운다. 탑 위로는 성벽 위 계단이 자동으로 붙는다(SIEGE-19 장식 규칙).
            foreach (Vector2Int cell in CornerTowerCells())
            {
                bool onRing = cell.x == WallWest || cell.x == WallEast || cell.y == WallSouth || cell.y == WallNorth;
                if (!onRing) seeds.Add(new InitialBuildPiece { definition = wall, cell = cell });
                seeds.Add(new InitialBuildPiece { definition = wall, cell = cell });
            }
            foreach ((Vector2Int cell, int rotation) in StairSeeds())
                seeds.Add(new InitialBuildPiece { definition = stairs, cell = cell, rotation = rotation });
            return seeds.ToArray();
        }

        /// <summary>네 모서리 탑이 차지하는 칸(각 2×2).</summary>
        internal static IEnumerable<Vector2Int> CornerTowerCells()
        {
            int[] xs = { WallWest, WallEast - CornerTowerSize + 1 }, zs = { WallSouth, WallNorth - CornerTowerSize + 1 };
            foreach (int x0 in xs)
                foreach (int z0 in zs)
                    for (int dx = 0; dx < CornerTowerSize; dx++)
                        for (int dz = 0; dz < CornerTowerSize; dz++)
                            yield return new Vector2Int(x0 + dx, z0 + dz);
        }

        /// <summary>땅 위 계단 여덟: 각 변 안쪽에 둘, 성문·모서리 탑에서 떨어진 자리. 회전은 오를 성벽 쪽이다.</summary>
        internal static (Vector2Int cell, int rotation)[] StairSeeds() => new[]
        {
            (new Vector2Int(16, WallNorth - 1), 0), (new Vector2Int(30, WallNorth - 1), 0),
            (new Vector2Int(16, WallSouth + 1), 2), (new Vector2Int(30, WallSouth + 1), 2),
            (new Vector2Int(WallEast - 1, 20), 1), (new Vector2Int(WallEast - 1, 28), 1),
            (new Vector2Int(WallWest + 1, 20), 3), (new Vector2Int(WallWest + 1, 28), 3),
        };

        /// <summary>
        /// 게임 씬의 격자를 넓히고 기본 성 배치와 유닛 시작 위치를 적용한다. 여러 번 실행해도 같은 결과다.
        /// 프로토타입과 공유하지 않는 게임 전용 격자·건설 카메라 에셋을 쓴다. 안뜰 시설은 있으면 옮기기만 해서
        /// 다른 설정 메뉴가 붙인 부품을 지우지 않는다(SIEGE-18 격자 이전에도 이 메뉴를 다시 실행한다).
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
            gridData.FindProperty("forbidValleys").boolValue = true;
            gridData.FindProperty("groundStairsOnly").boolValue = true;
            gridData.ApplyModifiedPropertiesWithoutUndo();
            var cameraSettings = CombatPrototypeSetup.Save(BuildCameraSettings.Create(), Data + "/BuildCamera.asset");
            var cameraData = new SerializedObject(cameraSettings);
            // SIEGE-36: 성이 두 배라 처음 화면과 최대 축소도 두 배 가까이 넓힌다. 처음 초점은 성 가운데.
            cameraData.FindProperty("orthographicSize").floatValue = 105f;
            cameraData.FindProperty("distance").floatValue = 480f;
            cameraData.FindProperty("navigationEnabled").boolValue = true;
            cameraData.FindProperty("initialFocus").vector2Value = new Vector2(AvenueX / (GridWidth * CellSize), PlazaZ / (GridDepth * CellSize));
            cameraData.FindProperty("minZoom").floatValue = 20f;
            cameraData.FindProperty("maxZoom").floatValue = 320f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();

            ConstructionWorld world = FindAll<ConstructionWorld>(game)[0];
            Transform geometry = world.transform.Find("Geometry");
            world.Configure(grid, geometry, geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), DefaultSeeds(wall, stairs, gate));
            EditorUtility.SetDirty(world);
            float width = GridWidth * CellSize, depth = GridDepth * CellSize;
            Vector3 origin = new Vector3(-width * 0.5f, 0f, -depth * 0.5f);
            world.transform.position = origin;
            Transform ground = geometry.Find("Ground");
            ground.localScale = new Vector3(width, 1f, depth);
            ground.localPosition = new Vector3(width * 0.5f, -0.5f, depth * 0.5f);
            // 지면 상자는 걷는 바닥(충돌)만 맡고 보이지 않는다. 겉은 풀밭 조각이 맵 밖까지 덮는다(SIEGE-36: 녹색 판을 치운다).
            // 프로토타입과 같이 쓰는 ConstructionGround 머티리얼은 그대로 두고 Game 씬의 렌더러만 끈다.
            ground.GetComponent<Renderer>().enabled = false;
            // 건설 격자선은 건설 화면에서만 보인다.
            Transform gridLines = world.transform.Find("GridLines");
            if (gridLines != null)
            {
                var phaseView = FindAll<GamePhaseView>(game)[0];
                phaseView.ConfigureBuildGrid(gridLines.gameObject);
                gridLines.gameObject.SetActive(false);
                EditorUtility.SetDirty(phaseView);
            }
            GameCourtyardArtSetup.Plan courtyard = ApplyCourtyard(geometry);
            GameTownSetup.Apply(game, world, courtyard);
            ApplySpawnCells();

            var orbit = FindAll<ConstructionCameraController>(game)[0];
            orbit.Configure(world, cameraSettings);
            EditorUtility.SetDirty(orbit);
            var buildCamera = orbit.GetComponent<Camera>();
            buildCamera.orthographicSize = cameraSettings.OrthographicSize;
            buildCamera.farClipPlane = 1000f;
            buildCamera.transform.rotation = Quaternion.Euler(cameraSettings.Pitch, cameraSettings.InitialYaw, 0f);
            buildCamera.transform.position = world.transform.TransformPoint(new Vector3(AvenueX, 0f, PlazaZ))
                - buildCamera.transform.forward * cameraSettings.Distance;
            var firstPerson = FindAll<FirstPersonCamera>(game)[0].GetComponent<Camera>();
            firstPerson.farClipPlane = 1000f;
            // 1인칭에서 내 팔·몸(SIEGE-36 플레이어 모델)이 카메라 가까이에 있어도 잘리지 않게 한다.
            firstPerson.nearClipPlane = 0.05f;

            // 부대는 북쪽 계단 두 곳 앞(계단 발치에서 남쪽 5.5m)과 성문 안 집결지, 플레이어는 안뜰 남쪽 큰길에서 북쪽을 본다(지면 기준 m).
            float stairFoot = (WallNorth - 1) * CellSize - 5.5f;
            Vector3[] squadPoints =
            {
                new Vector3(16.5f * CellSize, 0f, stairFoot), new Vector3(30.5f * CellSize, 0f, stairFoot),
                new Vector3(AvenueX, 0f, WallNorth * CellSize - 22.5f)
            };
            foreach (GameObject root in game.GetRootGameObjects())
            {
                for (int squad = 0; squad < squadPoints.Length; squad++)
                    for (int i = 0; i < 4; i++)
                        if (root.name == "Squad" + (squad + 1) + "_" + (i + 1))
                            Place(root.transform, origin + squadPoints[squad]
                                + new Vector3((i % 2 - 0.5f) * 1.1f, 0f, -(i / 2) * 1.1f), 0f);
                if (root.name == "Player")
                    Place(root.transform, origin + new Vector3(AvenueX, 0f, (WallSouth + 1) * CellSize + 15f), 0f);
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
                hint = Label(root.transform, "WASD 이동 - 건설 작업대로 가서 F를 누르세요", 48f, 20f);
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

        // 안뜰(x 82.5~262.5m, z 90~270m, 지면 기준) 시설(SIEGE-36). 여기 자리와 크기는 처음 만들 때의 임시 상자다.
        // 최종 자리·방향·겉모습·충돌 상자는 GameCourtyardArtSetup.Buildings가 정한다(길과 광장을 보게 제각각 돌린다).
        static readonly (string name, Vector3 position, Vector3 size, string sign)[] Facilities =
        {
            ("Tavern", new Vector3(130f, 0f, 166f), new Vector3(18f, 12f, 18f), "주점"),
            ("Shop", new Vector3(124f, 0f, 224f), new Vector3(11f, 10f, 12f), "상점"),
            ("Training", new Vector3(116f, 0f, 123f), new Vector3(18f, 10f, 11f), "훈련장"),
            ("Smithy", new Vector3(226f, 0f, 164f), new Vector3(16f, 10f, 13f), "대장간"),
            ("Construction", new Vector3(230f, 0f, 226f), new Vector3(13f, 12f, 13f), "건설"),
        };

        /// <summary>
        /// 안뜰 시설을 놓고 겉모습(바닥·건물 모델·조형물)을 입힌다. 이미 있는 시설은 자리만 바꿔 다른 설정 메뉴가 붙인 부품
        /// (시설 기능, 상호작용)을 지키고, 없는 것만 새로 만든다. SIEGE-18의 흙길·집결지 상자는 돌 바닥으로 바뀌어 지운다.
        /// </summary>
        static GameCourtyardArtSetup.Plan ApplyCourtyard(Transform geometry)
        {
            Transform layout = geometry.Find("CourtyardLayout");
            if (layout == null)
            {
                layout = new GameObject("CourtyardLayout").transform;
                layout.SetParent(geometry, false);
            }
            Material plaster = CombatPrototypeSetup.MaterialAsset("FacilityPlaster", new Color(0.73f, 0.65f, 0.48f));
            Material roof = CombatPrototypeSetup.MaterialAsset("FacilityRoof", new Color(0.28f, 0.32f, 0.43f));
            foreach (string old in new[] { "MainAvenue", "CrossAvenue", "MusterGround" })
            {
                Transform existing = layout.Find(old);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            foreach ((string name, Vector3 position, Vector3 size, string sign) in Facilities)
                PlaceFacility(layout, name, position, size, plaster, roof, sign);
            PlaceFacility(layout, HeroHallSetup.BuildingName, HeroHallSetup.BuildingPosition, HeroHallSetup.BuildingSize, plaster, roof,
                HeroHallSetup.DisplayName);
            return GameCourtyardArtSetup.Apply(geometry);
        }

        /// <summary>
        /// 적 출현 칸을 새 북쪽 외벽에 맞춘다(SIEGE-36). 가운데는 <see cref="SpawnCell"/>, 좌우는 StageSetup과 같은 간격이다.
        /// 스테이지 편성은 그대로 두고 출현 칸만 바꾼다.
        /// </summary>
        static void ApplySpawnCells()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(Data + "/Battle.asset");
            if (battle != null) SetVector2(battle, "spawnCell", SpawnCell);
            (string asset, float offset)[] points = { ("SpawnLeft", -StageSetup.SideOffsetCells), ("SpawnCenter", 0f), ("SpawnRight", StageSetup.SideOffsetCells) };
            foreach ((string asset, float offset) in points)
            {
                var point = AssetDatabase.LoadAssetAtPath<ProjectNeo.Stages.SpawnPointDefinition>(StageSetup.Folder + "/" + asset + ".asset");
                if (point != null) SetVector2(point, "cell", SpawnCell + Vector2.right * offset);
            }
        }

        static void SetVector2(UnityEngine.Object target, string property, Vector2 value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(property).vector2Value = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        // 시설 크기는 그대로라 자리만 옮긴다(충돌·외형은 루트 기준).
        static void PlaceFacility(Transform parent, string name, Vector3 position, Vector3 size, Material wallMaterial, Material roofMaterial, string sign)
        {
            Transform existing = parent.Find(name);
            if (existing == null) { Facility(parent, name, position, size, wallMaterial, roofMaterial, sign); return; }
            existing.localPosition = position;
        }

        static void Decoration(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = CombatPrototypeSetup.Box(name, Vector3.zero, scale, material);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        internal static void Facility(Transform parent, string name, Vector3 position, Vector3 size, Material wallMaterial, Material roofMaterial, string sign = null)
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
                label.fontSize = 16f; label.alignment = TextAlignmentOptions.Center; label.text = sign ?? name;
            }
        }

        static void AddNavigationHelp(ConstructionHud hud)
        {
            ReplaceCaption(hud, "준비: 건설한 뒤 전투를 시작하세요",
                "준비 - 가운데 버튼: 이동 / 휠: 확대/축소 / Home: 초기화");
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
                    && (label.text == "이동 / 붕괴 시험" || label.text == "유닛 체력");
                if (testButton || testLabel) child.gameObject.SetActive(false);
            }
        }

        static void ReplaceCaption(ConstructionHud hud, string oldText, string newText)
        {
            foreach (TMP_Text label in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text != oldText) continue;
                label.text = newText;
                EditorUtility.SetDirty(label);
            }
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
