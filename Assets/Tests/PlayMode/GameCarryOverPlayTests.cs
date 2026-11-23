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
    /// C-3: 전투 뒤 다음 준비에서 죽은 유닛은 씬 부대에서 빠지고(몸은 남는다), 빈 부대도 런·씬에 남는다(UI-07).
    /// 그 뒤 모집·편성·다음 전투 입력이 런과 씬에서 어긋나지 않는다.
    /// </summary>
    public class GameCarryOverPlayTests
    {
        static int cleanup;
        GameBattleController battle;
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
            battle = Object.FindAnyObjectByType<GameBattleController>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameCarryOverCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        IEnumerator Navigation()
        {
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator 전투_뒤_죽은_유닛은_부대에서_빠지고_빈_부대는_남는다()
        {
            RunState state = GameSession.Flow.Run.State;
            int squadCount = state.Army.Squads.Count;
            Assert.GreaterOrEqual(squadCount, 3, "Game 씬 시작 부대가 셋 이상이어야 이 경우를 모두 확인한다.");

            // 0번 부대에 대장을 고용해 둔다.
            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            Assert.IsNotNull(facilities.Facility(FacilityType.Tavern));
            state.RestockTavernIfNeeded();
            var spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            UnitCombatAI captainUnit = spawner.HireAtTavern(state.TavernOffers[0], 0, out HireCheck hire);
            Assert.AreEqual(HireCheck.Ok, hire);
            RunHero captain = (RunHero)state.Army.Squads[0].Leader;
            Squad<RunUnit> keep = state.Army.Squads[0];
            Squad<RunUnit> lose = state.Army.Squads[1];
            Squad<RunUnit> survive = state.Army.Squads[2];
            CharacterActor[] doomed = center.Squads[0].Members.Concat(center.Squads[1].Members).Select(unit => unit.Actor).ToArray();
            CharacterActor wounded = center.Squads[2].Members[0].Actor;
            int soldiersBefore = state.Soldiers.Count;

            Assert.IsTrue(battle.TryStartBattle());
            foreach (CharacterActor actor in doomed) Kill(actor);
            wounded.ReceiveDamage(5f);
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.AreEqual(soldiersBefore - doomed.Length, state.Soldiers.Count, "죽은 잡졸은 런에서 한 번 빠진다.");

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();

            Assert.AreEqual(squadCount, state.Army.Squads.Count, "빈 부대도 없어지지 않는다(UI-07).");
            Assert.AreEqual(squadCount, center.Squads.Count, "씬 부대도 그대로다.");
            Assert.AreEqual(squadCount, center.Commander.SquadCount);
            Assert.AreSame(lose, state.Army.Squads[1]);
            CollectionAssert.IsEmpty(lose.Members);
            Assert.AreSame(keep, state.Army.Squads[0], "대장만 남은 부대도 남는다.");
            Assert.AreSame(captain, keep.Leader);
            Assert.AreSame(captainUnit, center.Squads[0].Leader);
            Assert.AreSame(survive, state.Army.Squads[2]);
            Assert.IsTrue(center.Squads.All(squad => squad.Members.All(unit => !unit.Actor.Health.IsDead)),
                "죽은 유닛은 씬 부대에서 빠진다.");
            Assert.IsTrue(doomed.All(actor => actor != null && actor.Health.IsDead), "몸은 씬에 남는다.");
            Assert.AreEqual(wounded.Health.Maximum, wounded.Health.Current);
            for (int i = 0; i < state.Army.Squads.Count; i++)
                Assert.AreEqual(state.Army.Squads[i].Members.Count, center.Squads[i].Members.Count, "런과 씬 부대원 수가 같다: " + i);

            UnitCombatAI recruit = spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround);
            Assert.IsNotNull(recruit);
            RunSoldier stored = state.StoredSoldiers.Single();
            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryMoveSoldier(stored, 0), "잡졸이 모두 죽었던 부대에도 다시 넣는다.");
            Assert.IsTrue(center.Squads[0].Members.Contains(recruit));
            Assert.IsTrue(recruit.gameObject.activeSelf, "부대에 넣으면 다시 나타난다.");
            Assert.AreEqual(1, keep.Members.Count);

            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(2, battle.RunLink.CurrentInput.BattleNumber);
            Assert.AreEqual(squadCount, battle.RunLink.CurrentInput.Squads.Count);
            Assert.AreEqual(state.Soldiers.Count, battle.RunLink.CurrentInput.Squads.Sum(squad => squad.Members.Count));
        }
    }
}
