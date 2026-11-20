using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>META-02: Game 씬에서 찍은 퍽의 능력치 효과가 대상 유닛(플레이어·잡졸)에 붙고, 나중에 모집한 잡졸에도 붙는다.</summary>
    public class GamePerkPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GamePerkCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
            GameSession.Reset();
        }

        CharacterActor ActorWhere(System.Func<RunUnit, bool> match)
        {
            RunState state = battle.RunLink.State;
            return Object.FindObjectsByType<CharacterActor>()
                .FirstOrDefault(actor => { RunUnit unit = state.Find(battle.RunLink.IdOf(actor)); return unit != null && match(unit); });
        }

        [UnityTest]
        public IEnumerator StatPerks_ApplyToTheirTargets_IncludingLaterRecruits()
        {
            RunState state = battle.RunLink.State;
            Assert.IsNotNull(state.Perks, "Game 씬의 런은 퍽 묶음을 가진다.");
            CharacterActor player = ActorWhere(unit => unit is RunHero hero && hero.IsPlayer);
            CharacterActor soldier = ActorWhere(unit => unit is RunSoldier);
            Assert.IsNotNull(player); Assert.IsNotNull(soldier);
            float playerAttack = player.Stats.GetValue(StatType.Attack);
            float soldierAttack = soldier.Stats.GetValue(StatType.Attack);

            PerkDefinition playerPerk = PerkDefinition.Create("test_player_attack", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.5f)) });
            PerkDefinition soldierPerk = PerkDefinition.Create("test_soldier_attack", PerkCategory.UnitBoost,
                new[] { new PerkStatEffect(PerkTarget.Soldiers, StatModifier.Percent(StatType.Attack, 1f)) });
            created.Add(playerPerk); created.Add(soldierPerk);
            state.Perks.BonusPoints = 2;

            Assert.IsTrue(state.TryAllocatePerk(playerPerk));
            Assert.AreEqual(playerAttack * 1.5f, player.Stats.GetValue(StatType.Attack), 0.01f, "플레이어 공격력 +50%");
            Assert.AreEqual(soldierAttack, soldier.Stats.GetValue(StatType.Attack), 0.01f, "잡졸은 플레이어 대상 퍽을 받지 않는다");

            Assert.IsTrue(state.TryAllocatePerk(soldierPerk));
            Assert.AreEqual(soldierAttack * 2f, soldier.Stats.GetValue(StatType.Attack), 0.01f, "잡졸 공격력 +100%");
            Assert.AreEqual(playerAttack * 1.5f, player.Stats.GetValue(StatType.Attack), 0.01f, "다시 붙여도 겹치지 않는다");

            var spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            UnitCombatAI recruit = spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround);
            Assert.IsNotNull(recruit);
            float baseAttack = recruit.Actor.Stats.GetBase(StatType.Attack);
            Assert.Greater(recruit.Actor.Stats.GetValue(StatType.Attack), baseAttack * 1.99f, "나중에 모집한 잡졸에도 붙는다");
            yield return null;
        }
    }
}
