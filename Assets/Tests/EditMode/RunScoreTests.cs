using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>META-04 런 점수(Q-70, Q-78): 항목별 계산, 승천 배율, 전투 결과로 쌓이는 수, 런 저장.</summary>
    public class RunScoreTests
    {
        readonly List<Object> created = new List<Object>();
        StageRewardTable table;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp() => table = Track(StageRewardTable.Create(50, 10));

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        static RunScoreTally Tally(int stages, int bosses, int enemies, int gateIntact)
        {
            var tally = new RunScoreTally();
            tally.Restore(stages, bosses, enemies, gateIntact);
            return tally;
        }

        [Test]
        public void Calculate_AddsEveryPart_WithTheAgreedValues()
        {
            RunScore score = RunScore.Calculate(table, Tally(3, 1, 25, 2), livingHeroes: 2, livingGrunts: 7, gold: 135,
                reachedEnding: false, multiplier: 1f);

            Assert.AreEqual(3000 + 500 + 250 + 400 + 200 + 70 + 13, score.Total);
            Assert.AreEqual("Stages 3,000 + Bosses 500 + Enemies 250 + Gate held 400 + Heroes 200 + Grunts 70 + Gold 13", score.Breakdown());
        }

        [Test]
        public void Ending_AddsTheBonus_AndAscensionMultipliesTheTotal()
        {
            RunScore score = RunScore.Calculate(table, Tally(7, 0, 0, 0), 0, 0, 0, reachedEnding: true, multiplier: 1.3f);

            Assert.AreEqual(7000 + 5000, score.Subtotal);
            Assert.AreEqual(15600, score.Total);
            StringAssert.EndsWith("Ending 5,000 x1.3", score.Breakdown());
        }

        [Test]
        public void EmptyRun_ScoresZero()
        {
            RunScore score = RunScore.Calculate(table, new RunScoreTally(), 0, 0, 9, false, 1f);
            Assert.AreEqual(0, score.Total, "골드 9는 10골드당 1점에 못 미친다");
            Assert.AreEqual("No points", score.Breakdown());
        }

        [Test]
        public void Tally_CountsKillsEveryBattle_AndVictoriesWithTheGateIntact()
        {
            var tally = new RunScoreTally();
            var win = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            win.AddKill("enemy_ladder", 4); win.AddKill("enemy_archer", 2);
            var winGateLost = new BattleResult(2, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated) { GateIntact = false };
            winGateLost.AddKill("enemy_ladder", 3);
            var defeat = new BattleResult(3, BattleOutcome.Defeat, BattleEndReason.PlayerDied);
            defeat.AddKill("enemy_ladder", 1);

            tally.Record(win); tally.Record(winGateLost); tally.Record(defeat);
            tally.AddBossKills(1);

            Assert.AreEqual(2, tally.StagesCleared);
            Assert.AreEqual(1, tally.GateIntactVictories);
            Assert.AreEqual(10, tally.EnemiesKilled, "패배한 전투의 처치도 센다");
            Assert.AreEqual(1, tally.BossesKilled);
        }

        [Test]
        public void RunState_RecordsResults_AndKeepsTheTallyInTheRunSave()
        {
            HeroDefinition player = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            SquadConfig squads = Track(SquadConfig.Create(4, 10));
            RunState run = RunState.CreateNew(Track(RunConfig.Create(squads, player, 0)));
            run.CreateBattleInput(1);
            var result = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.AddKill("enemy_ladder", 5);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
            Assert.AreEqual(1, run.ScoreTally.StagesCleared);
            Assert.AreEqual(5, run.ScoreTally.EnemiesKilled);

            var content = Track(RunContentCatalog.Create(new ItemDefinition[0], new MaterialDefinition[0], new[] { player },
                new SoldierDefinition[0], new FacilityDefinition[0], new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            RunState restored = RunState.Restore(run.Capture(2, content), content, new RunRestoreOptions { SquadConfig = squads }, out string error);

            Assert.IsNotNull(restored, error);
            Assert.AreEqual(1, restored.ScoreTally.StagesCleared);
            Assert.AreEqual(5, restored.ScoreTally.EnemiesKilled);
            Assert.AreEqual(1, restored.ScoreTally.GateIntactVictories);
            Assert.AreEqual(0, restored.LivingHeroCount, "플레이어는 영웅 점수에 들지 않는다");
        }
    }
}
