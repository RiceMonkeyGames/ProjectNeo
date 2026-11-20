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
    /// <summary>
    /// D-8·UI-01: Game 씬 편성 화면(부대 카드)에서 잡졸을 고르고 카드에 놓으면 런과 씬 부대가 함께 바뀌고, 빈 부대는 둘 다에서 없어진다.
    /// 고용 영웅은 부대 카드에 놓으면 대장, 대기 카드에 놓으면 대기가 된다.
    /// </summary>
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
        public IEnumerator 카드_화면에서_잡졸을_새_부대로_옮기고_되돌리면_빈_부대가_없어진다()
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
            Assert.AreEqual(squadCount + 2, organization.CardCount, "부대 카드 + 새 부대 카드 + 대기 카드");
            var soldier = (RunSoldier)state.Army.Squads[source].Members.First(member => member is RunSoldier);
            int sourceSize = state.Army.Squads[source].Members.Count;

            Assert.IsTrue(organization.Select(soldier));
            Assert.AreSame(soldier, organization.Selected);
            Assert.IsTrue(organization.Select(soldier), "다시 누르면 선택을 푼다.");
            Assert.IsNull(organization.Selected);
            Assert.IsFalse(organization.DropOnSquad(-1), "고른 것이 없으면 옮기지 않는다.");

            Assert.IsTrue(organization.Select(soldier));
            Assert.IsTrue(organization.DropOnSquad(-1));
            Assert.IsNull(organization.Selected, "옮기면 선택이 풀린다.");
            Assert.AreEqual(squadCount + 1, state.Army.Squads.Count);
            Assert.AreEqual(squadCount + 1, center.Squads.Count, "씬 부대도 함께 늘어난다.");
            Assert.AreEqual(squadCount + 1, center.Commander.SquadCount);
            Assert.AreSame(state.Army.Squads[squadCount], state.SquadOf(soldier));
            Assert.IsTrue(InScene(center, squadCount, soldier, battle));
            Assert.AreEqual(sourceSize - 1, state.Army.Squads[source].Members.Count);
            StringAssert.Contains((squadCount + 1) + "부대", organization.BodyText);

            Assert.IsTrue(organization.Select(soldier));
            Assert.IsFalse(organization.DropOnReserve(), "잡졸은 대기로 보낼 수 없다.");
            Assert.IsTrue(organization.DropOnSquad(source));
            Assert.AreEqual(squadCount, state.Army.Squads.Count, "비게 된 새 부대가 없어진다.");
            Assert.AreEqual(squadCount, center.Squads.Count);
            Assert.AreEqual(squadCount, center.Commander.SquadCount);
            Assert.AreSame(state.Army.Squads[source], state.SquadOf(soldier));
            Assert.IsTrue(InScene(center, source, soldier, battle));
            StringAssert.Contains("빈 부대", organization.BodyText);

            Assert.IsFalse(organization.Select(state.Player), "플레이어는 카드에서 고를 수 없다(Q-32 임시).");
            organization.Close();
            yield return null;
            Assert.IsFalse(organization.IsOpen);
            Assert.IsTrue(view.IsExplorationView);
        }

        [UnityTest]
        public IEnumerator 고용_영웅을_부대_카드에_놓으면_대장_대기_카드에_놓으면_대기가_된다()
        {
            RunState state = GameSession.Flow.Run.State;
            var spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            // 시설은 처음 조회할 때 런 데이터에 붙는다(GameFacilities).
            Assert.IsNotNull(Object.FindAnyObjectByType<GameFacilities>().Facility(ProjectNeo.Facilities.FacilityType.Tavern));
            state.RestockTavernIfNeeded();
            Assert.Greater(state.TavernOffers.Count, 0, "주점 1레벨 영웅 풀");
            UnitCombatAI unit = spawner.HireAtTavern(state.TavernOffers[0], -1, out HireCheck check);
            Assert.AreEqual(HireCheck.Ok, check);
            RunHero hero = state.ReserveHeroes.Single();

            Assert.IsTrue(organization.Open());
            Assert.IsTrue(organization.Select(hero));
            Assert.IsFalse(organization.DropOnSquad(-1), "영웅은 새 부대 카드에 놓을 수 없다.");
            Assert.AreSame(hero, organization.Selected, "놓지 못하면 선택이 남는다.");
            Assert.IsTrue(center.TryGetGatherPoint(0, out Vector3 gather), "시작 부대 0에는 부대원이 있다.");
            Assert.AreEqual(UnitDirective.Hold, unit.Directive, "대기 영웅은 주점 앞에 선다.");
            Assert.IsTrue(organization.DropOnSquad(0));
            Assert.AreSame(hero, state.Army.Squads[0].Leader);
            Assert.AreSame(unit, center.Squads[0].Leader, "씬 부대장도 바뀐다.");
            Assert.AreEqual(UnitDirective.Move, unit.Directive, "새 대장은 부대 쪽으로 걸어간다(BUG-hired-unit-not-joining-squad).");
            Assert.Less(Vector3.Distance(gather, unit.MoveDestination), 0.01f, "목적지는 부대가 모인 곳이다.");

            if (state.Army.Squads.Count > 1)
            {
                Assert.IsTrue(organization.Select(hero));
                Assert.IsTrue(organization.DropOnSquad(1), "대장을 다른 부대로 바로 옮긴다.");
                Assert.IsNull(state.Army.Squads[0].Leader);
                Assert.AreSame(hero, state.Army.Squads[1].Leader);
            }

            Assert.IsTrue(organization.Select(hero));
            Assert.IsTrue(organization.DropOnReserve());
            CollectionAssert.AreEqual(new[] { hero }, state.ReserveHeroes);
            Assert.IsTrue(center.Squads.All(squad => squad.Leader == null));
            yield break;
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
