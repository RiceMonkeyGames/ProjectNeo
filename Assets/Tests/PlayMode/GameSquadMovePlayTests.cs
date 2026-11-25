using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// BUG-hero-move-stops-early: 부대 이동 지시는 대장(고용 영웅)이 도착할 때까지 끝나지 않는다.
    /// 잡졸만 먼저 도착해도 멀리서 걸어오던 대장이 중간에 멈추면 안 된다.
    /// </summary>
    public class GameSquadMovePlayTests
    {
        static int cleanup;
        SquadCommandCenter center;
        ConstructionWorld world;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameSquadMoveCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        [UnityTest]
        public IEnumerator SquadMove_WaitsForTheCaptainBeforeCompleting()
        {
            RunState state = GameSession.Flow.Run.State;
            Assert.IsNotNull(Object.FindAnyObjectByType<GameFacilities>().Facility(FacilityType.Tavern));
            state.RestockTavernIfNeeded();
            var spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            UnitCombatAI captain = spawner.HireAtTavern(state.TavernOffers[0], 0, out HireCheck hire);
            Assert.AreEqual(HireCheck.Ok, hire);
            Assert.AreSame(captain, center.Squads[0].Leader);

            // 잡졸이 이미 서 있는 곳으로 보내 잡졸은 곧 도착하고, 선술집 앞에서 출발하는 대장은 한참 걸어야 하게 한다.
            // SIEGE-36: 잡졸이 선 자리 그대로면 대장 칸을 잡졸이 막아 대장이 1m 앞에서 멈춘다. 6m 북쪽(계단 쪽) 빈 땅으로 보낸다(잡졸은 여전히 가깝다).
            Vector3 destination = center.Squads[0].Members.Aggregate(Vector3.zero, (sum, unit) => sum + unit.transform.position)
                / center.Squads[0].Members.Count + Vector3.forward * 6f;
            float startDistance = Flat(captain.transform.position, destination);
            Assert.Greater(startDistance, 8f, "대장이 부대에서 멀리 떨어져 출발해야 이 경우를 확인한다.");
            center.Commander.ClearSelection();
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, destination, 0f));
            center.Commander.ClearSelection();

            bool soldiersArrivedFirst = false;
            // SIEGE-36: 안뜰이 넓어져 주점 앞에서 부대까지 100m 넘게 걷는다(30fps로 60초까지 기다린다).
            for (int frame = 0; frame < 1800 && captain.Directive == UnitDirective.Move; frame++)
            {
                if (!soldiersArrivedFirst && center.Squads[0].Members.All(unit => unit.Directive != UnitDirective.Move))
                {
                    soldiersArrivedFirst = true;
                    Assert.AreEqual(SquadOrderKind.Move, center.Commander.OrderOf(0).Kind,
                        "잡졸만 도착했을 때 부대 이동이 끝나면 안 된다.");
                }
                yield return null;
            }

            Assert.IsTrue(soldiersArrivedFirst, "잡졸이 대장보다 먼저 도착해야 이 경우를 확인한다.");
            Assert.AreNotEqual(UnitDirective.Move, captain.Directive, "대장도 결국 도착한다.");
            Assert.Less(Flat(captain.transform.position, destination), 4f,
                $"대장이 목적지 대형까지 와야 한다(출발 거리 {startDistance:0.0}m).");
            yield return null;
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(0).Kind, "모두 도착하면 부대는 그 자리 중지가 된다.");
        }
    }
}
