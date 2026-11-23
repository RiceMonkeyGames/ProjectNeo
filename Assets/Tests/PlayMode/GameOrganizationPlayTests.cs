using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// D-8·UI-07: Game 씬 편성 화면(세 열)에서 줄을 누르면 런과 씬 부대가 함께 바뀐다. 보관 병사는 고른 부대로, 부대원은 보관으로 가고,
    /// 빈 부대는 남는다. 보관 영웅을 누르면 부대장이 되고(있던 부대장은 보관으로), 부대장을 누르면 해제된다.
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
        public IEnumerator 줄을_누르면_보관과_부대_사이를_오가고_빈_부대는_남는다()
        {
            Assert.IsNotNull(organization, "Game 씬 전투 오브젝트에 편성 컴포넌트가 있어야 한다.");
            var battle = organization.GetComponent<GameBattleController>();
            RunState state = GameSession.Flow.Run.State;
            int squadCount = state.Army.Squads.Count;
            Assert.AreEqual(squadCount, center.Squads.Count);
            Assert.IsTrue(state.Army.CanAddSquad, "시작 부대 수가 상한보다 적어야 새 부대 경로를 확인할 수 있다.");

            Assert.IsTrue(organization.Open());
            Assert.IsTrue(view.IsPanelView);
            Assert.AreEqual(0, organization.SelectedSquad);
            Assert.AreEqual(squadCount + 1, organization.RowsIn(0).Count, "부대 줄 + 새 부대 줄");
            Assert.IsTrue(organization.RowsIn(0)[0].IsSelected);
            Assert.AreEqual("1부대", organization.MemberTitleText);
            Squad<RunUnit> first = state.Army.Squads[0];
            Assert.AreEqual(first.Members.Count + 1, organization.RowsIn(1).Count, "부대장 줄 + 병사 줄");
            Assert.AreEqual("부대장 공석", organization.RowsIn(1)[0].NameText);
            Assert.IsFalse(organization.RowsIn(1)[0].IsClickable);

            // 병사를 눌러 보관으로, 보관 줄을 눌러 다시 부대로.
            var soldier = (RunSoldier)first.Members.First(member => member is RunSoldier);
            int size = first.Members.Count;
            Assert.IsTrue(organization.Release(soldier));
            Assert.AreEqual(size - 1, first.Members.Count);
            Assert.IsFalse(InScene(center, 0, soldier, battle));
            CollectionAssert.Contains(state.StoredSoldiers.ToList(), soldier);
            Assert.AreEqual(1, organization.RowsIn(2).Count, "보관 줄에 나타난다.");
            Assert.IsTrue(organization.Assign(soldier));
            Assert.AreSame(first, state.SquadOf(soldier));
            Assert.IsTrue(InScene(center, 0, soldier, battle));

            // 새 부대를 만들고 고른 뒤, 병사를 넣었다 빼도 빈 부대는 남는다.
            Assert.IsTrue(organization.AddSquad());
            Assert.AreEqual(squadCount, organization.SelectedSquad, "새 부대가 골라진다.");
            Assert.AreEqual(squadCount + 1, state.Army.Squads.Count);
            Assert.AreEqual(squadCount + 1, center.Squads.Count, "씬 부대도 함께 늘어난다.");
            Assert.AreEqual(squadCount + 1, center.Commander.SquadCount);
            Assert.IsTrue(organization.SelectSquad(0));
            Assert.IsTrue(organization.Release(soldier));
            Assert.IsTrue(organization.SelectSquad(squadCount));
            Assert.IsTrue(organization.Assign(soldier));
            Assert.IsTrue(InScene(center, squadCount, soldier, battle));
            Assert.IsTrue(organization.Release(soldier));
            Assert.AreEqual(squadCount + 1, state.Army.Squads.Count, "비게 된 부대도 남는다.");

            // 고른 부대를 없애면 부대원은 보관으로 간다.
            Assert.IsTrue(organization.Assign(soldier));
            Assert.IsTrue(organization.RemoveSquad(squadCount));
            Assert.AreEqual(squadCount, state.Army.Squads.Count);
            Assert.AreEqual(squadCount, center.Squads.Count);
            Assert.AreEqual(squadCount, center.Commander.SquadCount);
            Assert.IsNull(state.SquadOf(soldier));
            Assert.AreEqual(squadCount - 1, organization.SelectedSquad);

            Assert.IsFalse(organization.Assign(state.Player), "플레이어는 부대장이 될 수 없다(Q-32 임시).");
            organization.Close();
            yield return null;
            Assert.IsFalse(organization.IsOpen);
            Assert.IsTrue(view.IsExplorationView);
        }

        [UnityTest]
        public IEnumerator 보관_영웅을_누르면_부대장이_되고_다른_영웅을_누르면_교체되며_부대장을_누르면_해제된다()
        {
            RunState state = GameSession.Flow.Run.State;
            var spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            // 시설은 처음 조회할 때 런 데이터에 붙는다(GameFacilities).
            Assert.IsNotNull(Object.FindAnyObjectByType<GameFacilities>().Facility(ProjectNeo.Facilities.FacilityType.Tavern));
            state.RestockTavernIfNeeded();
            Assert.Greater(state.TavernOffers.Count, 0, "주점 1레벨 영웅 풀");
            state.Wallet.AddGold(2000);
            UnitCombatAI unit = spawner.HireAtTavern(state.TavernOffers[0], -1, out HireCheck check);
            Assert.AreEqual(HireCheck.Ok, check);
            RunHero hero = state.ReserveHeroes.Single();
            state.RestockTavernIfNeeded();
            Assume.That(state.TavernOffers.Count, Is.GreaterThan(0), "두 번째 영웅을 고용할 매물이 있어야 교체를 확인한다.");
            UnitCombatAI otherUnit = spawner.HireAtTavern(state.TavernOffers[0], -1, out check);
            Assert.AreEqual(HireCheck.Ok, check);
            RunHero other = state.ReserveHeroes.First(reserve => reserve != hero);

            Assert.IsTrue(organization.Open());
            Assert.IsTrue(center.TryGetGatherPoint(0, out Vector3 gather), "시작 부대 0에는 부대원이 있다.");
            Assert.AreEqual(UnitDirective.Hold, unit.Directive, "대기 영웅은 주점 앞에 선다.");
            Assert.IsTrue(organization.Assign(hero));
            Assert.AreSame(hero, state.Army.Squads[0].Leader);
            Assert.AreSame(unit, center.Squads[0].Leader, "씬 부대장도 바뀐다.");
            Assert.IsTrue(organization.RowsIn(1)[0].IsLeader);
            Assert.AreEqual(UnitDirective.Move, unit.Directive, "새 대장은 부대 쪽으로 걸어간다(BUG-hired-unit-not-joining-squad).");
            Assert.Less(Vector3.Distance(gather, unit.MoveDestination), 0.01f, "목적지는 부대가 모인 곳이다.");

            Assert.IsTrue(organization.Assign(other), "다른 영웅을 누르면 부대장이 바뀐다.");
            Assert.AreSame(other, state.Army.Squads[0].Leader);
            Assert.AreSame(otherUnit, center.Squads[0].Leader);
            CollectionAssert.AreEqual(new[] { hero }, state.ReserveHeroes, "있던 부대장은 보관으로 간다.");

            Assert.IsTrue(organization.Release(other));
            Assert.IsNull(state.Army.Squads[0].Leader);
            Assert.AreEqual(2, state.ReserveHeroes.Count);
            Assert.IsTrue(center.Squads.All(squad => squad.Leader == null));
            yield break;
        }

        [UnityTest]
        public IEnumerator 편성_키를_다시_누르면_창이_닫힌다()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.IsTrue(organization.Open());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.O));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.IsFalse(organization.IsOpen, "열 때 쓴 O 키로 닫는다(UI-07).");
                Assert.IsTrue(view.IsExplorationView);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
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
