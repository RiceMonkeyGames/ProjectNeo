using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectNeo.Tests
{
    /// <summary>저장된 Title·Game 씬으로 타이틀 → 게임 → 타이틀 흐름을 확인한다. 실제 버튼 클릭은 직접 확인 대상이다.</summary>
    public class GameFlowPlayTests
    {
        static int cleanupCount;

        [SetUp]
        public void SetUp() => GameSession.Reset();

        // 씬을 통째로 바꾸는 테스트이므로 끝나면 빈 씬만 남겨 다른 테스트에 카메라·UI가 남지 않게 한다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameFlowTestCleanup" + cleanupCount++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameSession.Reset();
        }

        static IEnumerator WaitForActiveScene(string name)
        {
            for (int frame = 0; frame < 300 && SceneManager.GetActiveScene().name != name; frame++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose()
        {
            using var inputScope = new FocusIndependentInputScope();
            var originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
                var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
                var battle = Object.FindAnyObjectByType<GameBattleController>();
                var world = Object.FindAnyObjectByType<ConstructionWorld>();
                var player = Object.FindAnyObjectByType<PlayerInputController>();
                var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
                var hud = Object.FindAnyObjectByType<GameFlowHud>();
                for (int i = 0; i < 600 && !battle.CanStart; i++) yield return null;
                Assert.IsTrue(battle.CanStart);
                // Start on a wall, far from the former gate interaction.
                var wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall && p.Placement.Level == 0);
                var motor = player.GetComponent<CharacterController>();
                motor.enabled = false;
                player.transform.position = wall.transform.position + Vector3.up * (wall.Placement.Definition.Height + 0.05f);
                motor.enabled = true;
                camera.Look(new Vector2(35f, -12f));
                for (int i = 0; i < 5; i++) yield return null;
                Cursor.lockState = CursorLockMode.Locked;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
                Assert.Greater(interaction.BattleStartProgress, 0f);
                Assert.Less(interaction.BattleStartProgress, 1f);
                Assert.IsTrue(hud.InteractionHint.Contains("%"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.AreEqual(0f, interaction.BattleStartProgress);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);

                Vector3 position = player.transform.position;
                Quaternion rotation = camera.transform.rotation;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while (Time.realtimeSinceStartupAsDouble < deadline && GameSession.Flow.Phase == GamePhase.Preparation) yield return null;
                Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
                Assert.Less(Vector3.Distance(position, player.transform.position), 0.02f);
                Assert.Less(Quaternion.Angle(rotation, camera.transform.rotation), 0.01f);
                Assert.AreEqual(1, battle.BattleNumber);
                Assert.IsTrue(battle.Enemies.All(enemy => enemy.gameObject.activeSelf));
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(1, battle.BattleNumber);
                GameSession.Flow.FinishBattle(BattleOutcome.Victory);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
                Assert.AreEqual(0f, interaction.BattleStartProgress);
                GameSession.Flow.ContinueToNextStage();
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase, "Held B must not restart the next stage.");
                Assert.AreEqual(1, battle.BattleNumber);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator BattleHold_BlocksConstructionFacilitiesEquipmentAndRequiresFreshPress()
        {
            using var inputScope = new FocusIndependentInputScope();
            var originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
                var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
                var battle = Object.FindAnyObjectByType<GameBattleController>();
                var view = Object.FindAnyObjectByType<GamePhaseView>();
                var facilities = Object.FindAnyObjectByType<GameFacilities>();
                var equipment = Object.FindAnyObjectByType<GameEquipment>();
                for (int i = 0; i < 600 && !battle.CanStart; i++) yield return null;
                Assert.IsTrue(battle.CanStart);
                for (int modal = 0; modal < 3; modal++)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                    yield return new WaitForSecondsRealtime(0.2f);
                    Assert.Greater(interaction.BattleStartProgress, 0f);
                    bool opened = modal == 0 ? view.EnterConstruction()
                        : modal == 1 ? facilities.Open(ProjectNeo.Facilities.FacilityType.Shop) : equipment.Open();
                    Assert.IsTrue(opened);
                    yield return new WaitForSecondsRealtime(1.1f);
                    Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
                    Assert.AreEqual(0f, interaction.BattleStartProgress);
                    Assert.IsFalse(interaction.CanStartBattle);
                    if (modal == 0) view.ExitConstruction();
                    else if (modal == 1) facilities.Close();
                    else equipment.Close();
                    yield return new WaitForSecondsRealtime(1.1f);
                    Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase, "Closing a modal while B is held must not arm it.");
                    Assert.AreEqual(0f, interaction.BattleStartProgress);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return null;
                }
                // Simultaneous equipment/start input gives the modal priority.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I, Key.B));
                yield return null; yield return null;
                Assert.IsTrue(equipment.IsOpen);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
                Assert.AreEqual(0, battle.BattleNumber);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 타이틀에서_새_게임을_누르면_게임_씬의_준비_단계로_간다()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            Assert.AreEqual(GamePhase.Title, GameSession.Flow.Phase);
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNotNull(title);

            title.StartGame();
            title.StartGame();
            yield return WaitForActiveScene("Game");

            Assert.AreEqual(1, SceneManager.sceneCount, "타이틀 씬은 내려가야 한다.");
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(1, GameSession.Flow.Run.StageNumber);
            Assert.IsNotNull(Object.FindAnyObjectByType<GameFlowHud>());
        }

        [UnityTest]
        public IEnumerator 게임_씬_안에서_단계가_바뀌고_패배_후_타이틀_씬으로_돌아온다()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            Object.FindAnyObjectByType<TitleScreen>().StartGame();
            yield return WaitForActiveScene("Game");
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            GameFlow flow = GameSession.Flow;

            hud.StartBattle();
            Assert.AreEqual(GamePhase.Battle, flow.Phase);
            hud.Win();
            Assert.AreEqual(GamePhase.Reward, flow.Phase);
            hud.NextStage();
            Assert.AreEqual(GamePhase.Preparation, flow.Phase);
            Assert.AreEqual(2, flow.Run.StageNumber);
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name, "단계 전환은 씬을 바꾸지 않는다.");

            hud.StartBattle();
            hud.Lose();
            Assert.AreEqual(GamePhase.RunResult, flow.Phase);
            hud.ReturnToTitle();
            yield return WaitForActiveScene("Title");

            Assert.AreEqual(GamePhase.Title, flow.Phase);
            Assert.IsNull(flow.Run);
            Assert.IsNotNull(Object.FindAnyObjectByType<TitleScreen>());
        }

        [UnityTest]
        public IEnumerator 준비는_1인칭이고_건설소에서만_건설_시점으로_전환하며_유닛과_지시는_유지된다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            var placement = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            var command = Object.FindAnyObjectByType<SquadCommandInput>(FindObjectsInactive.Include);
            var buildCamera = Object.FindAnyObjectByType<ConstructionCameraController>(FindObjectsInactive.Include);
            var firstPerson = Object.FindAnyObjectByType<FirstPersonCamera>(FindObjectsInactive.Include);
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            GameFlow flow = GameSession.Flow;

            Assert.IsTrue(view.IsExplorationView);
            Assert.IsFalse(view.IsBuildView || placement.enabled || buildCamera.gameObject.activeInHierarchy);
            Assert.IsTrue(player.enabled && firstPerson.gameObject.activeInHierarchy);
            Assert.IsFalse(command.enabled);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            int alive = center.AliveCount(0);
            Assert.Greater(alive, 0);
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            Vector3 position = player.transform.position;
            Assert.IsTrue(view.EnterConstruction());
            Assert.IsTrue(view.IsBuildView && placement.enabled && buildCamera.gameObject.activeInHierarchy);
            Assert.IsFalse(player.enabled);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsFalse(view.EnterConstruction(), "중복 진입을 무시한다.");
            view.ExitConstruction();
            Assert.IsTrue(view.IsExplorationView && player.enabled);
            Assert.AreEqual(position, player.transform.position);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);

            flow.StartBattle();
            Assert.IsTrue(view.IsBattleView);
            Assert.IsTrue(player.enabled && command.enabled && firstPerson.gameObject.activeInHierarchy);
            Assert.IsFalse(placement.enabled || buildCamera.gameObject.activeInHierarchy);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            Assert.AreEqual(SquadOrderKind.Attack, center.Commander.OrderOf(0).Kind, "단계가 바뀌어도 지시는 남는다.");
            Assert.IsFalse(view.EnterConstruction(), "전투 중 건설 진입을 막는다.");
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);

            flow.FinishBattle(BattleOutcome.Victory);
            Assert.IsFalse(view.IsBuildView || view.IsBattleView);
            Assert.IsFalse(placement.enabled || player.enabled || command.enabled);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);

            flow.ContinueToNextStage();
            Assert.IsTrue(view.IsExplorationView && player.enabled && firstPerson.gameObject.activeInHierarchy);
            Assert.IsFalse(placement.enabled || command.enabled || buildCamera.gameObject.activeInHierarchy);
            Assert.AreEqual(alive, center.AliveCount(0), "다음 스테이지 준비에서도 같은 유닛이 남아 있다.");
        }

        [UnityTest]
        public IEnumerator 기본_성은_외벽으로_닫혀_있고_안뜰에서_성벽_위로_올라갈_수_있다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            LogAssert.NoUnexpectedReceived();
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
            Assert.AreEqual(240f, world.Grid.Width * world.Grid.CellSize);
            Assert.AreEqual(300f, world.Grid.Depth * world.Grid.CellSize);
            var facilities = world.transform.Find("Geometry/CourtyardLayout");
            Assert.IsNotNull(facilities);
            foreach (string name in new[] { "Tavern", "Shop", "Training", "Smithy", "Construction" })
                Assert.IsNotNull(facilities.Find(name), "시설 " + name);
            Assert.AreEqual(1, world.Structures.Count(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate));
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Assert.IsTrue(gate.Placement.IsFixed);
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            Vector3 inside = center.Squads[0].Members[0].transform.position;
            float height = gate.Placement.Definition.Height;
            Vector3 wallTop = gate.transform.position + new Vector3(-9f, height, 0f);
            Vector3 outside = gate.transform.position + new Vector3(0f, 0f, 12f);
            Assert.IsTrue(NavMesh.SamplePosition(inside, out NavMeshHit from, 1f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(wallTop, out NavMeshHit top, 0.5f, NavMesh.AllAreas), "성벽 위가 걸을 수 있어야 한다.");
            Assert.IsTrue(NavMesh.SamplePosition(outside, out NavMeshHit field, 0.5f, NavMesh.AllAreas), "성 밖 들판이 있어야 한다.");
            Assert.Less(Mathf.Abs(field.position.y), 0.5f);

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from.position, top.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "안뜰에서 계단으로 성벽 위에 닿아야 한다.");
            NavMesh.CalculatePath(from.position, field.position, NavMesh.AllAreas, path);
            Assert.AreNotEqual(NavMeshPathStatus.PathComplete, path.status, "성문과 외벽이 온전하면 걸어서 밖으로 나갈 수 없다.");

            var player = Object.FindAnyObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            Vector3 playerLocal = world.LocalPoint(player.transform.position);
            Assert.AreEqual(0f, playerLocal.y, 0.1f, "플레이어는 안뜰 바닥에서 시작한다.");
            Assert.Greater(playerLocal.x, 25f * world.Grid.CellSize);
            Assert.Less(playerLocal.x, 55f * world.Grid.CellSize);
            Assert.Greater(playerLocal.z, 29f * world.Grid.CellSize);
            Assert.Less(playerLocal.z, 59f * world.Grid.CellSize);
            var perimeter = world.Layout.Placements.Where(piece => piece.Definition.Kind != BuildPieceKind.Stairs).ToArray();
            Assert.AreEqual(96f, (perimeter.Max(piece => piece.Cell.x + piece.Size.x) - perimeter.Min(piece => piece.Cell.x)) * world.Grid.CellSize);
            Assert.AreEqual(96f, (perimeter.Max(piece => piece.Cell.y + piece.Size.y) - perimeter.Min(piece => piece.Cell.y)) * world.Grid.CellSize);
            Assert.AreEqual(120f, (world.Grid.Depth - perimeter.Max(piece => piece.Cell.y + piece.Size.y)) * world.Grid.CellSize);
            // 새 건물에 도달할 수 있고 건물 충돌체를 가로지르지 않는 안뜰 동선이다.
            foreach (string name in new[] { "Tavern", "Shop", "Training", "Smithy", "Construction" })
            {
                var building = facilities.Find(name);
                var bounds = building.GetComponent<BoxCollider>().bounds;
                Vector3 entrance = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
                Assert.IsTrue(NavMesh.SamplePosition(entrance, out NavMeshHit door, 0.5f, NavMesh.AllAreas));
                NavMesh.CalculatePath(from.position, door.position, NavMesh.AllAreas, path);
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, name + " 앞까지 갈 수 있어야 한다.");
            }
        }

        [UnityTest]
        public IEnumerator 넓은_맵의_건설_시점은_이동과_확대축소_후_초기화할_수_있다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            var camera = Object.FindAnyObjectByType<ConstructionCameraController>();
            Vector2 focus = camera.Focus;
            float zoom = camera.ZoomSize;
            camera.Pan(new Vector2(30f, 40f));
            Assert.AreEqual(focus + new Vector2(30f, 40f), camera.Focus);
            camera.Zoom(2f);
            Assert.Less(camera.ZoomSize, zoom);
            camera.Zoom(1000f);
            Assert.AreEqual(20f, camera.ZoomSize);
            camera.Zoom(-1000f);
            Assert.AreEqual(180f, camera.ZoomSize);
            camera.Pan(new Vector2(10000f, -10000f));
            Assert.AreEqual(new Vector2(240f, 0f), camera.Focus);
            camera.ResetNavigation();
            Assert.AreEqual(focus, camera.Focus);
            Assert.AreEqual(zoom, camera.ZoomSize);
            Assert.AreEqual(zoom, camera.GetComponent<Camera>().orthographicSize);
        }

        [UnityTest]
        public IEnumerator ConstructionGrid_RenderedLinesKeepPixelThicknessAcrossZoomAndAngles()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            var orbit = Object.FindAnyObjectByType<ConstructionCameraController>();
            var camera = orbit.GetComponent<Camera>();
            var grid = Object.FindAnyObjectByType<BuildGridView>();
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
            yield return null;
            Mesh mesh = grid.GetComponent<MeshFilter>().sharedMesh;
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture target = null;
            try
            {
                foreach (Vector2Int resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    target = new RenderTexture(resolution.x, resolution.y, 24);
                    target.Create();
                    camera.targetTexture = target;
                    foreach (float zoom in new[] { 20f, 55f, 180f })
                    {
                        orbit.ResetNavigation();
                        orbit.Zoom(Mathf.Log(zoom / orbit.ZoomSize) / Mathf.Log(0.85f));
                        foreach (float yaw in new[] { 0f, 22.5f, 45f, 90f, 135f })
                        {
                            camera.transform.rotation = Quaternion.Euler(30f, yaw, 0f);
                            camera.Render();
                            Vector3[] points = mesh.vertices;
                            Assert.AreEqual((world.Grid.Width + world.Grid.Depth + 2) * 4, points.Length);
                            Assert.AreSame(mesh, grid.GetComponent<MeshFilter>().sharedMesh, "Reuse mesh while zooming.");
                            foreach (int index in new[] { 0, (world.Grid.Width + 1) * 4 })
                            {
                                Vector2 p0 = camera.WorldToScreenPoint(grid.transform.TransformPoint(points[index]));
                                Vector2 along = (Vector2)camera.WorldToScreenPoint(grid.transform.TransformPoint(points[index + (index == 0 ? 1 : 3)])) - p0;
                                Vector2 across = (Vector2)camera.WorldToScreenPoint(grid.transform.TransformPoint(points[index + (index == 0 ? 3 : 1)])) - p0;
                                float pixels = Mathf.Abs(along.x * across.y - along.y * across.x) / along.magnitude;
                                Assert.GreaterOrEqual(pixels, orbit.MinimumGridLinePixels - 0.01f,
                                    $"{resolution}, zoom={zoom}, yaw={yaw}, line={index}");
                            }
                            Assert.AreEqual(0f, (points[0].x + points[3].x) * 0.5f, 0.0001f, "Line remains centered on cell boundary.");
                            Assert.AreEqual(world.Grid.CellSize, (points[4].x + points[7].x) * 0.5f, 0.0001f);
                        }
                    }
                    camera.targetTexture = originalTarget;
                    target.Release(); Object.Destroy(target); target = null;
                }
            }
            finally
            {
                camera.targetTexture = originalTarget;
                if (target != null) { target.Release(); Object.Destroy(target); }
                orbit.ResetNavigation();
            }
        }

        [UnityTest]
        public IEnumerator ConstructionWheelInput_UsesNormalizedStepsAndRestoresCamera()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            var camera = Object.FindAnyObjectByType<ConstructionCameraController>();
            float startZoom = camera.ZoomSize;
            Vector2 startFocus = camera.Focus;
            using var inputScope = new FocusIndependentInputScope();
            var originalScrollBehavior = InputSystem.settings.scrollDeltaBehavior;
            Mouse originalMouse = Mouse.current;
            Keyboard originalKeyboard = Keyboard.current;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Vector2 position = new Vector2(Screen.width - 5f, Screen.height * 0.5f);
            try
            {
                InputSystem.settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return null;
                yield return null;
                Assert.IsFalse(UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(),
                    "Wheel test must point outside construction UI.");

                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.up });
                yield return null;
                Assert.AreEqual(startZoom * 0.85f, camera.ZoomSize, 0.001f,
                    "One normalized wheel tick must zoom by the configured 15%, not 1/120 of a tick.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.down });
                yield return null;
                Assert.AreEqual(startZoom, camera.ZoomSize, 0.001f, "Reverse tick restores the previous zoom.");

                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.up * 0.5f });
                yield return null;
                Assert.Less(camera.ZoomSize, startZoom * 0.95f, "Fractional wheel deltas remain meaningful.");
                Assert.Greater(camera.ZoomSize, startZoom * 0.85f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.up * 1000f });
                yield return null;
                Assert.AreEqual(20f, camera.ZoomSize);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.down * 1000f });
                yield return null;
                Assert.AreEqual(180f, camera.ZoomSize);

                camera.Pan(new Vector2(10f, 10f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home));
                yield return null;
                Assert.AreEqual(startZoom, camera.ZoomSize);
                Assert.AreEqual(startFocus, camera.Focus);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                InputSystem.settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = Vector2.up * 120f });
                yield return null;
                Assert.AreEqual(startZoom * 0.85f, camera.ZoomSize, 0.001f,
                    "Unnormalized Windows wheel ticks must retain their existing sensitivity.");
#endif
            }
            finally
            {
                InputSystem.settings.scrollDeltaBehavior = originalScrollBehavior;
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 건설소를_가까이_바라보고_F로_열고_Esc로_나오며_전투에서는_열리지_않는다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
            var station = Object.FindAnyObjectByType<ConstructionStation>();
            var player = Object.FindAnyObjectByType<PlayerInputController>();
            var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var motor = player.GetComponent<CharacterController>();
            using var inputScope = new FocusIndependentInputScope();
            Keyboard originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                interaction.RefreshTarget();
                Assert.IsNull(interaction.Target, "시작 위치는 건설소에서 멀다.");
                Assert.IsFalse(interaction.TryInteract());
                Bounds bounds = station.GetComponent<BoxCollider>().bounds;
                Vector3 near = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
                motor.enabled = false; player.transform.position = near; motor.enabled = true;
                yield return null;
                Cursor.lockState = CursorLockMode.Locked;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Physics.SyncTransforms();
                interaction.RefreshTarget();
                Assert.IsNull(interaction.Target, "등을 돌리면 상호작용하지 않는다.");
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                interaction.RefreshTarget();
                Assert.AreSame(station, interaction.Target);
                // 실제 InputAction 경로를 통과시킨다. 기존 카메라의 yaw도 건설소 쪽(+Z)이다.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                Assert.IsTrue(view.IsBuildView);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
                Assert.IsFalse(player.enabled);
                Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
                yield return null; // 안내 글자는 LateUpdate에서 갱신한다.
                Assert.IsTrue(hud.InteractionHint.Contains("돌아가기"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.IsTrue(view.IsExplorationView && player.enabled);
                Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "건설 종료 Esc가 이동 커서를 다시 풀지 않아야 한다.");
                Assert.AreEqual(near.x, player.transform.position.x, 0.01f);
                Assert.AreEqual(near.z, player.transform.position.z, 0.01f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.IsTrue(GameSession.Flow.StartBattle());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                Assert.IsFalse(view.IsBuildView);
                Assert.IsNull(interaction.Target);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 게임_씬만_열어도_새_런의_준비_단계로_시작한다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(1, GameSession.Flow.Run.StageNumber);
        }
    }
}
