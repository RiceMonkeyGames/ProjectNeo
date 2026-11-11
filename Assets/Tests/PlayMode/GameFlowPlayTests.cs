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
        public IEnumerator 준비는_건설_시점이고_전투는_1인칭_지휘이며_유닛과_지시는_유지된다()
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

            Assert.IsTrue(view.IsBuildView);
            Assert.IsTrue(placement.enabled && buildCamera.gameObject.activeInHierarchy);
            Assert.IsFalse(player.enabled || command.enabled || firstPerson.gameObject.activeInHierarchy);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            int alive = center.AliveCount(0);
            Assert.Greater(alive, 0);
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack));

            flow.StartBattle();
            Assert.IsTrue(view.IsBattleView);
            Assert.IsTrue(player.enabled && command.enabled && firstPerson.gameObject.activeInHierarchy);
            Assert.IsFalse(placement.enabled || buildCamera.gameObject.activeInHierarchy);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            Assert.AreEqual(SquadOrderKind.Attack, center.Commander.OrderOf(0).Kind, "단계가 바뀌어도 지시는 남는다.");
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);

            flow.FinishBattle(BattleOutcome.Victory);
            Assert.IsFalse(view.IsBuildView || view.IsBattleView);
            Assert.IsFalse(placement.enabled || player.enabled || command.enabled);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);

            flow.ContinueToNextStage();
            Assert.IsTrue(view.IsBuildView && placement.enabled && buildCamera.gameObject.activeInHierarchy);
            Assert.IsFalse(player.enabled || command.enabled);
            Assert.AreEqual(alive, center.AliveCount(0), "다음 스테이지 준비에서도 같은 유닛이 남아 있다.");
        }

        [UnityTest]
        public IEnumerator 기본_성은_외벽으로_닫혀_있고_안뜰에서_성벽_위로_올라갈_수_있다()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            LogAssert.NoUnexpectedReceived();
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
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
            Assert.Greater(player.transform.position.y, height - 0.5f, "플레이어는 성문 위에서 시작한다.");
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
