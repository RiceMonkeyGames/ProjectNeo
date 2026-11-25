using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Town;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// SIEGE-36: 성 안 주민. 준비 단계에는 시설 NPC가 입구 옆에 서 있고 시민이 걸어 다니며,
    /// 전투가 시작되면 모두 가까운 건물로 달려가 숨었다가 다음 준비 단계에 제자리로 돌아온다.
    /// </summary>
    public class GameTownPlayTests
    {
        static int cleanup;
        TownDirector town;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
            town = Object.FindAnyObjectByType<TownDirector>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameTownCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        [UnityTest]
        public IEnumerator Preparation_NpcsStandAtTheirDoors_AndCitizensWalkAround()
        {
            Assert.IsNotNull(town, "Game 씬에 주민 감독이 있다.");
            var npcs = town.Residents.Where(r => r.IsStationNpc).ToArray();
            var citizens = town.Residents.Where(r => !r.IsStationNpc).ToArray();
            Assert.AreEqual(6, npcs.Length, "시설 여섯 곳마다 NPC 한 명");
            Assert.AreEqual(8, citizens.Length, "시민 여덟 명");
            Assert.IsTrue(town.Residents.All(r => r.gameObject.activeSelf && r.Agent.enabled && r.Agent.isOnNavMesh),
                "준비 단계에는 모두 보이고 길찾기 메시 위에 있다.");
            Assert.IsTrue(npcs.All(n => n.State == TownResidentState.Standing));
            Assert.IsTrue(npcs.All(n => n.GetComponent<StationNpc>() != null && n.GetComponent<StationNpc>().Station.Npc == n.GetComponent<StationNpc>()),
                "NPC와 시설이 서로를 가리킨다.");

            Vector3[] start = citizens.Select(c => c.transform.position).ToArray();
            Vector3[] npcStart = npcs.Select(n => n.transform.position).ToArray();
            bool walked = false;
            for (int frame = 0; frame < 450 && !walked; frame++)
            {
                yield return null;
                for (int i = 0; i < citizens.Length; i++)
                    walked |= Vector3.Distance(citizens[i].transform.position, start[i]) > 3f;
            }
            Assert.IsTrue(walked, "시민이 산책 지점 사이를 걷는다.");
            for (int i = 0; i < npcs.Length; i++)
                Assert.Less(Vector3.Distance(npcs[i].transform.position, npcStart[i]), 0.5f, "시설 NPC는 제자리에 서 있다.");
        }

        [UnityTest]
        public IEnumerator BattleStart_EveryoneRunsIndoorsAndHides_ThenReturnsNextPreparation()
        {
            var battle = Object.FindAnyObjectByType<GameBattleController>();
            Vector3[] homes = town.Residents.Select(r => r.HomePosition).ToArray();
            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsTrue(town.Residents.All(r => r.State == TownResidentState.Fleeing || r.State == TownResidentState.Hidden),
                "전투가 시작되면 모두 달아난다.");
            for (int frame = 0; frame < 900 && town.Residents.Any(r => r.gameObject.activeSelf); frame++) yield return null;
            Assert.IsTrue(town.Residents.All(r => !r.gameObject.activeSelf && r.State == TownResidentState.Hidden),
                "모두 건물에 숨는다.");

            GameSession.Flow.FinishBattle(BattleOutcome.Victory);
            yield return null;
            Assert.IsTrue(town.Residents.All(r => !r.gameObject.activeSelf), "보상 화면에서도 숨어 있다.");
            GameSession.Flow.ContinueToNextStage();
            yield return null;
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            for (int i = 0; i < town.Residents.Count; i++)
            {
                Assert.IsTrue(town.Residents[i].gameObject.activeSelf, "다음 준비 단계에 다시 나온다.");
                Vector3 flat = town.Residents[i].transform.position - homes[i];
                flat.y = 0f;
                Assert.Less(flat.magnitude, 1f, "제자리로 돌아온다.");
            }
        }
    }
}
