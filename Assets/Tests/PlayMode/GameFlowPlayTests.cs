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
