using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// SIEGE-09 Game 씬 보스: 마지막 웨이브와 함께 등장, 보스 능력치·크기, 적 목록의 보스 줄, 보스 체력바,
    /// 지휘형 강화와 죽을 때 웨이브 약화, 보스 처치 보상과 런 점수 기록.
    /// </summary>
    public class GameBossPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        float capture;
        BossDefinition boss;
        MaterialDefinition trophy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            ConstructionWorld world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SingleWaveEncounterFixture.UseSingleWave();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBossCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
            if (boss != null) Object.Destroy(boss);
            if (trophy != null) Object.Destroy(trophy);
        }

        GameObject Prefab(SiegeAttackerRole role) => battle.Settings.Enemies
            .First(group => group.unitPrefab != null && group.unitPrefab.GetComponent<SiegeAttacker>().Role == role).unitPrefab;

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        [UnityTest]
        public IEnumerator 보스는_웨이브와_함께_나와_능력치_체력바_지휘_강화를_쓰고_죽으면_웨이브가_약해지며_보상과_점수에_든다()
        {
            GameObject ladder = Prefab(SiegeAttackerRole.Ladder);
            trophy = MaterialDefinition.Create("boss_test_trophy");
            boss = BossDefinition.Create("boss_test_captain", "Test Captain", BossRole.Commander, ladder, 1.6f,
                600f, 21f, 5f, rewardGold: 77, rewardMaterials: new BossMaterialReward(trophy, 3)).WithAura(60f, 0.2f, 2f, 0.2f);
            Vector2 cell = battle.Settings.SpawnCell;
            var plan = new EncounterPlan(new[]
            {
                new EncounterWave(0f, new[] { new EncounterGroup(ladder, 1, cell), EncounterGroup.ForBoss(boss, cell) })
            });
            GameBattleController.EncounterSourceOverride = (settings, stage) => plan;
            Assert.IsTrue(battle.RebuildEncounter());

            Assert.AreEqual(2, battle.Enemies.Count);
            Assert.AreEqual(1, battle.Bosses.Count);
            BossUnit unit = battle.Bosses[0];
            Assert.AreSame(boss, unit.Definition);
            Assert.IsFalse(unit.gameObject.activeSelf, "보스도 웨이브가 나오기 전에는 꺼져 있다.");
            StringAssert.Contains("Boss: Test Captain × 1", battle.Intel.Describe());
            Assert.IsNull(battle.ActiveBoss, "준비 단계에는 체력바에 보일 보스가 없다.");
            Vector3 baseScale = ladder.transform.Find("Visual").localScale;
            Assert.AreEqual(baseScale.x * 1.6f, unit.transform.Find("Visual").localScale.x, 0.001f, "겉모습만 커진다.");

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            CharacterActor bossActor = unit.Actor;
            Assert.AreEqual(600f, bossActor.Health.Maximum, 0.01f);
            Assert.AreEqual(600f, bossActor.Health.Current, 0.01f, "보스 체력으로 채워서 나온다.");
            Assert.AreEqual(21f, bossActor.Stats.GetValue(StatType.Attack), 0.01f);
            Assert.AreSame(unit, battle.ActiveBoss);
            yield return null;
            Assert.AreSame(unit, battle.BossHud.Shown);
            Assert.AreEqual("Test Captain", battle.BossHud.NameText);
            Assert.AreEqual(1f, battle.BossHud.FillRatio, 0.001f);

            CharacterActor mate = battle.Enemies.First(enemy => enemy.GetComponent<BossUnit>() == null).Actor;
            float baseAttack = mate.Stats.GetBase(StatType.Attack);
            for (int i = 0; i < 30 && unit.BuffedCount == 0; i++) yield return null;
            Assert.AreEqual(1, unit.BuffedCount, "반경 안의 같은 편 적이 강화된다.");
            Assert.AreEqual(baseAttack * 1.2f, mate.Stats.GetValue(StatType.Attack), 0.01f);

            bossActor.ReceiveDamage(300f + bossActor.Stats.GetValue(StatType.Defense));
            yield return null;
            Assert.AreEqual(0.5f, battle.BossHud.FillRatio, 0.01f, "체력바가 줄어든다.");

            Kill(bossActor);
            yield return null; yield return null;
            Assert.IsTrue(unit.PenaltyApplied);
            Assert.AreEqual(0, unit.BuffedCount);
            Assert.AreEqual(baseAttack * 0.8f, mate.Stats.GetValue(StatType.Attack), 0.01f, "강화가 빠지고 남은 전투 동안 -20%.");
            Assert.IsNull(battle.BossHud.Shown, "보스가 죽으면 체력바를 숨긴다.");
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);

            Kill(mate);
            for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            var rewards = battle.GetComponent<GameRewardController>();
            Assert.IsNotNull(rewards.LastReward);
            Assert.GreaterOrEqual(rewards.LastReward.Gold, 77, "보스 골드가 승리 보상에 든다.");
            Assert.AreEqual(3, rewards.LastReward.Materials[trophy]);
            Assert.AreEqual(1, battle.RunLink.State.LastBattleResult.BossesKilled.Count);
            Assert.AreEqual(1, battle.RunLink.State.ScoreTally.BossesKilled, "런 점수·해금 포인트가 읽는 보스 수");
        }

        [UnityTest]
        public IEnumerator 보스는_다른_적이_후퇴해도_물러나지_않는다()
        {
            GameObject archer = Prefab(SiegeAttackerRole.Archer);
            boss = BossDefinition.Create("boss_test_marksman", "Test Marksman", BossRole.Artillery, archer, 1.4f, 350f, 26f, 2f, 22f);
            Vector2 cell = battle.Settings.SpawnCell;
            var plan = new EncounterPlan(new[]
            {
                new EncounterWave(0f, new[] { new EncounterGroup(archer, 1, cell), EncounterGroup.ForBoss(boss, cell) })
            });
            GameBattleController.EncounterSourceOverride = (settings, stage) => plan;
            Assert.IsTrue(battle.RebuildEncounter());
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.AreEqual(22f, battle.Bosses[0].Actor.Stats.GetValue(StatType.AttackRange), 0.01f, "포병형 사거리");
            // 공성 수단 없이 궁수만 남으면 일반 적은 물러난다(Q-30). 보스는 남는다.
            for (int i = 0; i < 120 && !battle.IsRetreating; i++) yield return null;
            Assert.IsTrue(battle.IsRetreating, "궁수만 남아 후퇴가 시작된다.");
            for (int i = 0; i < 900 && battle.RetreatedCount == 0; i++) yield return null;
            Assert.AreEqual(1, battle.RetreatedCount, "일반 궁수만 떠난다.");
            Assert.IsTrue(battle.Bosses[0].gameObject.activeSelf);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "보스가 남아 있으면 승리가 아니다.");
            Kill(battle.Bosses[0].Actor);
            for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
        }
    }
}
