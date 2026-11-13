using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-8: Game 씬 편성 화면에서 잡졸을 옮기면 런과 씬 부대가 함께 바뀌고, 빈 부대는 둘 다에서 없어진다.</summary>
    public class GameOrganizationPlayTests
    {
        static int cleanup;
        GameSquadOrganization organization;
        GamePhaseView view;
        SquadCommandCenter center;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            organization = Object.FindAnyObjectByType<GameSquadOrganization>();
            view = Object.FindAnyObjectByType<GamePhaseView>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameOrganizationCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static bool InScene(SquadCommandCenter center, int squad, RunSoldier soldier, GameBattleController battle)
        {
            foreach (UnitCombatAI member in center.Squads[squad].Members)
                if (member != null && battle.RunLink.IdOf(member.GetComponent<CharacterActor>()) == soldier.Id) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator 편성_화면에서_잡졸을_새_부대로_옮기고_되돌리면_빈_부대가_없어진다()
        {
            Assert.IsNotNull(organization, "Game 씬 전투 오브젝트에 편성 컴포넌트가 있어야 한다.");
            var battle = organization.GetComponent<GameBattleController>();
            RunState state = GameSession.Flow.Run.State;
            int squadCount = state.Army.Squads.Count;
            Assert.AreEqual(squadCount, center.Squads.Count);
            int source = Enumerable.Range(0, squadCount).First(i => state.Army.Squads[i].Members.Count >= 2);
            Assert.IsTrue(state.Army.CanAddSquad, "시작 부대 수가 상한보다 적어야 새 부대 경로를 확인할 수 있다.");

            Assert.IsTrue(organization.Open());
            Assert.IsTrue(view.IsPanelView);
            Assert.IsTrue(organization.SelectSource(source));
            Assert.IsTrue(organization.SelectTarget(-1));
            var soldier = (RunSoldier)state.Army.Squads[source].Members.First(member => member is RunSoldier);
            int sourceSize = state.Army.Squads[source].Members.Count;

            Assert.IsTrue(organization.TryMove(0));
            Assert.AreEqual(squadCount + 1, state.Army.Squads.Count);
            Assert.AreEqual(squadCount + 1, center.Squads.Count, "씬 부대도 함께 늘어난다.");
            Assert.AreEqual(squadCount + 1, center.Commander.SquadCount);
            Assert.AreSame(state.Army.Squads[squadCount], state.SquadOf(soldier));
            Assert.IsTrue(InScene(center, squadCount, soldier, battle));
            Assert.AreEqual(sourceSize - 1, state.Army.Squads[source].Members.Count);
            StringAssert.Contains("부대 " + (squadCount + 1), organization.BodyText);

            Assert.IsTrue(organization.SelectSource(squadCount));
            Assert.IsTrue(organization.SelectTarget(source));
            Assert.IsTrue(organization.TryMove(0));
            Assert.AreEqual(squadCount, state.Army.Squads.Count, "비게 된 새 부대가 없어진다.");
            Assert.AreEqual(squadCount, center.Squads.Count);
            Assert.AreEqual(squadCount, center.Commander.SquadCount);
            Assert.AreSame(state.Army.Squads[source], state.SquadOf(soldier));
            Assert.IsTrue(InScene(center, source, soldier, battle));
            StringAssert.Contains("빈 부대", organization.BodyText);

            Assert.IsFalse(organization.TryAssignCaptain(0), "대기 영웅이 없다.");
            organization.Close();
            yield return null;
            Assert.IsFalse(organization.IsOpen);
            Assert.IsTrue(view.IsExplorationView);
        }

        [UnityTest]
        public IEnumerator 전투_중에는_편성_화면이_열리지_않는다()
        {
            var battle = organization.GetComponent<GameBattleController>();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsFalse(organization.Open());
            Assert.IsFalse(organization.IsOpen);
        }
    }
}
