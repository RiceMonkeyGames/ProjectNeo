using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>FLOW-03 승천 단계(Q-78): 규칙 누적, 각 수치 규칙, 해금, 흐름의 엔딩, 실제 단계 표 에셋.</summary>
    public class AscensionTests
    {
        readonly List<Object> created = new List<Object>();
        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        // 승인된 10단계와 같은 표(수치는 Q-78).
        AscensionTable ApprovedTable() => Track(AscensionTable.Create(0.1f,
            new AscensionLevel("Enemy HP +15%", new AscensionRule(AscensionRuleKind.EnemyHealthPercent, 0.15f)),
            new AscensionLevel("Starting gold -25%", new AscensionRule(AscensionRuleKind.StartingGoldPercent, -0.25f)),
            new AscensionLevel("Enemy attack +15%", new AscensionRule(AscensionRuleKind.EnemyAttackPercent, 0.15f)),
            new AscensionLevel("Prep 12h -> 10h", new AscensionRule(AscensionRuleKind.PrepBudgetHours, -2f)),
            new AscensionLevel("Victory gold -20%", new AscensionRule(AscensionRuleKind.VictoryGoldPercent, -0.2f)),
            new AscensionLevel("Enemies +20%", new AscensionRule(AscensionRuleKind.EnemyCountPercent, 0.2f)),
            new AscensionLevel("Morale 150 -> 120", new AscensionRule(AscensionRuleKind.StartingMorale, -30f)),
            new AscensionLevel("Boss", new AscensionRule(AscensionRuleKind.BossHealthPercent, 0.3f), new AscensionRule(AscensionRuleKind.BossAttackPercent, 0.2f)),
            new AscensionLevel("Repair x2", new AscensionRule(AscensionRuleKind.RepairTimePercent, 1f)),
            new AscensionLevel("Speed +15%", new AscensionRule(AscensionRuleKind.EnemyMoveSpeedPercent, 0.15f))));

        [Test]
        public void 단계_0이나_표가_없으면_아무것도_바뀌지_않는다()
        {
            foreach (AscensionEffects none in new[] { AscensionEffects.For(null, 5), AscensionEffects.For(ApprovedTable(), 0) })
            {
                Assert.AreEqual(0, none.Level);
                Assert.AreEqual(1f, none.RewardMultiplier);
                Assert.AreEqual(300, none.StartingGold(300));
                Assert.AreEqual(12f, none.PrepBudgetHours(12f));
                Assert.AreEqual(7, none.EnemyCount(7));
                CollectionAssert.IsEmpty(none.EnemyModifiers());
                Assert.AreEqual(55, none.ScaleReward(55));
            }
        }

        [Test]
        public void 높은_단계는_낮은_단계의_규칙을_모두_쓴다()
        {
            AscensionTable table = ApprovedTable();
            AscensionEffects first = AscensionEffects.For(table, 1);
            Assert.AreEqual(0.15f, first.Total(AscensionRuleKind.EnemyHealthPercent), 0.0001f);
            Assert.AreEqual(0f, first.Total(AscensionRuleKind.StartingGoldPercent), "2단계 규칙은 아직 없다.");

            AscensionEffects third = AscensionEffects.For(table, 3);
            Assert.AreEqual(0.15f, third.Total(AscensionRuleKind.EnemyHealthPercent), 0.0001f);
            Assert.AreEqual(-0.25f, third.Total(AscensionRuleKind.StartingGoldPercent), 0.0001f);
            Assert.AreEqual(0.15f, third.Total(AscensionRuleKind.EnemyAttackPercent), 0.0001f);
            Assert.AreEqual(0f, third.Total(AscensionRuleKind.PrepBudgetHours));

            Assert.AreEqual(10, AscensionEffects.For(table, 99).Level, "표보다 높은 단계는 최고 단계로 맞춘다.");
            Assert.AreEqual(new[] { "A1 Enemy HP +15%", "A2 Starting gold -25%" }, table.DescribeUpTo(2).ToArray());
        }

        [Test]
        public void 승인된_수치가_각_값에_적용된다()
        {
            AscensionEffects top = AscensionEffects.For(ApprovedTable(), 10);
            Assert.AreEqual(150, top.StartingGold(200), "시작 골드 -25%");
            Assert.AreEqual(10f, top.PrepBudgetHours(12f), 0.0001f, "준비 시간 12h -> 10h");
            Assert.AreEqual(0f, top.PrepBudgetHours(1f), "준비 시간은 0 아래로 내려가지 않는다.");
            Assert.AreEqual(80, top.VictoryGold(100), "승리 골드 -20%");
            Assert.AreEqual(6, top.EnemyCount(5), "적 수 +20%");
            Assert.AreEqual(5, top.EnemyCount(4), "늘어난 수는 올림한다(4.8 -> 5).");
            Assert.AreEqual(0, top.EnemyCount(0));
            Assert.AreEqual(120f, top.StartingMorale(150f, 200f), 0.0001f, "시작 사기 150 -> 120");
            Assert.AreEqual(2f, top.RepairHours(1f), 0.0001f, "수리 시간 x2");

            List<StatModifier> enemy = top.EnemyModifiers();
            Assert.AreEqual(3, enemy.Count);
            Assert.AreEqual(0.15f, enemy.Single(m => m.stat == StatType.MaxHealth).value, 0.0001f);
            Assert.AreEqual(0.15f, enemy.Single(m => m.stat == StatType.Attack).value, 0.0001f);
            Assert.AreEqual(0.15f, enemy.Single(m => m.stat == StatType.MoveSpeed).value, 0.0001f);
            Assert.IsTrue(enemy.All(m => m.kind == ModifierKind.Percent));

            List<StatModifier> boss = top.EnemyModifiers(true);
            Assert.AreEqual(0.45f, boss.Single(m => m.stat == StatType.MaxHealth).value, 0.0001f, "보스는 일반 +15%에 +30%를 더한다.");
            Assert.AreEqual(0.35f, boss.Single(m => m.stat == StatType.Attack).value, 0.0001f);
        }

        [Test]
        public void 점수와_해금_포인트_배율은_단계마다_0_1씩_오르고_올림한다()
        {
            AscensionTable table = ApprovedTable();
            Assert.AreEqual(1.5f, AscensionEffects.For(table, 5).RewardMultiplier, 0.0001f);
            Assert.AreEqual(2f, AscensionEffects.For(table, 10).RewardMultiplier, 0.0001f);
            Assert.AreEqual(2000, AscensionEffects.For(table, 10).ScaleReward(1000), "float 오차로 한 칸 더 올라가면 안 된다.");
            Assert.AreEqual(11, AscensionEffects.For(table, 3).ScaleReward(8), "8 x 1.3 = 10.4는 올림해서 11이다.");
        }

        [Test]
        public void 열린_가장_높은_단계를_깨야_다음_단계가_열린다()
        {
            var meta = new MetaProgress();
            Assert.AreEqual(0, AscensionProgress.HighestUnlocked(meta, 10));
            Assert.IsFalse(AscensionProgress.CanStart(meta, 1, 10));

            Assert.IsTrue(AscensionProgress.RecordClear(meta, 0, 10), "기본 게임을 깨면 승천 1이 열린다.");
            Assert.AreEqual(1, meta.ascensionUnlocked);
            Assert.IsTrue(AscensionProgress.CanStart(meta, 1, 10));

            Assert.IsFalse(AscensionProgress.RecordClear(meta, 0, 10), "낮은 단계를 다시 깨도 새 단계는 열리지 않는다.");
            Assert.AreEqual(1, meta.ascensionUnlocked);

            meta.ascensionUnlocked = 10;
            Assert.IsFalse(AscensionProgress.RecordClear(meta, 10, 10), "최고 단계 뒤에는 더 없다.");
            Assert.AreEqual(10, meta.ascensionUnlocked);

            meta.ascensionUnlocked = 15;
            Assert.AreEqual(10, AscensionProgress.HighestUnlocked(meta, 10), "표가 줄어도 범위 안으로 맞춘다.");
        }

        [Test]
        public void 마지막_스테이지의_보상_뒤에는_다음_스테이지_대신_엔딩으로_간다()
        {
            var flow = new GameFlow();
            Assert.IsTrue(flow.StartRun(null, 3));
            Assert.AreEqual(3, flow.Run.AscensionLevel);
            Assert.IsTrue(flow.SetStageCount(2));
            Assert.IsFalse(flow.Run.IsFinalStage);
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Victory);
            Assert.IsFalse(flow.FinishRun(), "마지막 스테이지가 아니면 엔딩으로 갈 수 없다.");
            Assert.IsTrue(flow.ContinueToNextStage());

            Assert.IsTrue(flow.Run.IsFinalStage);
            Assert.IsFalse(flow.FinishRun(), "보상 단계가 아니면 엔딩으로 갈 수 없다.");
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Victory);
            Assert.IsFalse(flow.ContinueToNextStage(), "마지막 스테이지 뒤에는 다음 스테이지가 없다.");
            Assert.AreEqual(GamePhase.Reward, flow.Phase);
            var phases = new List<GamePhase>();
            flow.PhaseChanged += phases.Add;
            Assert.IsTrue(flow.FinishRun());
            Assert.AreEqual(GamePhase.Ending, flow.Phase);
            CollectionAssert.AreEqual(new[] { GamePhase.Ending }, phases);
            Assert.IsFalse(flow.FinishRun());
            Assert.IsFalse(flow.ContinueToNextStage());
            Assert.IsTrue(flow.ReturnToTitle());
            Assert.IsNull(flow.Run);
        }

        [Test]
        public void 스테이지_수를_모르면_마지막_스테이지가_없고_단계는_다시_시작해도_남는다()
        {
            var flow = new GameFlow();
            flow.StartRun(null, 2);
            Assert.IsFalse(flow.Run.IsFinalStage);
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Victory);
            Assert.IsTrue(flow.ContinueToNextStage());

            var resumed = new GameFlow();
            Assert.IsTrue(resumed.ResumeRun(4, 2));
            Assert.AreEqual(2, resumed.Run.AscensionLevel);
            Assert.IsTrue(resumed.RestartRunAtFirstStage());
            Assert.AreEqual(1, resumed.Run.StageNumber);
            Assert.AreEqual(2, resumed.Run.AscensionLevel, "되살리기에 실패해 1스테이지로 다시 시작해도 고른 단계는 남는다.");
        }

        [Test]
        public void 승천의_준비_시간이_준비_시계_예산에_적용된다()
        {
            HeroDefinition hero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            SquadConfig squads = Track(SquadConfig.Create(4, 10));
            PrepTimeSettings time = Track(PrepTimeSettings.Create(budgetHours: 12f));
            RunState run = RunState.CreateNew(Track(RunConfig.Create(squads, hero, 0)).WithPrepTime(time));
            run.SetupAscension(ApprovedTable(), 4);
            Assert.IsTrue(run.BeginPreparationClockIfNeeded());
            Assert.AreEqual(10f, run.Clock.Remaining, 0.0001f);
        }

        [Test]
        public void 적_수_규칙은_무리마다_적용되고_시간과_스폰_칸은_그대로다()
        {
            var prefab = new GameObject("enemy");
            created.Add(prefab);
            var plan = new EncounterPlan(new[]
            {
                new EncounterWave(0f, new[] { new EncounterGroup(prefab, 5, new Vector2(1, 2)), new EncounterGroup(prefab, 1, Vector2.zero, true) }),
                new EncounterWave(30f, new[] { new EncounterGroup(prefab, 4, new Vector2(3, 4)) }),
            });
            EncounterPlan scaled = plan.WithCounts(AscensionEffects.For(ApprovedTable(), 6).EnemyCount);
            Assert.AreEqual(2, scaled.Waves.Count);
            Assert.AreEqual(30f, scaled.Waves[1].StartSeconds);
            Assert.AreEqual(6, scaled.Waves[0].Groups[0].Count);
            Assert.AreEqual(new Vector2(1, 2), scaled.Waves[0].Groups[0].Cell);
            Assert.AreEqual(2, scaled.Waves[0].Groups[1].Count, "1마리도 올림하면 2마리가 된다.");
            Assert.IsTrue(scaled.Waves[0].Groups[1].Hidden);
            Assert.AreEqual(5, scaled.Waves[1].Groups[0].Count);
            Assert.AreEqual(13, scaled.TotalUnits);
            Assert.AreSame(plan, plan.WithCounts(null));
        }

        [Test]
        public void 실제_단계_표는_승인된_10단계이고_RunStart에_연결되어_있다()
        {
            var table = AssetDatabase.LoadAssetAtPath<AscensionTable>("Assets/_Project/Data/GameFlow/Ascension.asset");
            Assert.IsNotNull(table, "Ascension.asset이 있어야 한다.");
            Assert.AreEqual(10, table.MaxLevel);
            Assert.AreEqual(0.1f, table.RewardMultiplierPerLevel, 0.0001f);
            var start = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/_Project/Data/GameFlow/Build/RunStart.asset");
            Assert.AreSame(table, start.Ascension);

            AscensionEffects top = AscensionEffects.For(table, 10);
            Assert.AreEqual(0.15f, top.Total(AscensionRuleKind.EnemyHealthPercent), 0.0001f);
            Assert.AreEqual(-0.25f, top.Total(AscensionRuleKind.StartingGoldPercent), 0.0001f);
            Assert.AreEqual(0.15f, top.Total(AscensionRuleKind.EnemyAttackPercent), 0.0001f);
            Assert.AreEqual(-2f, top.Total(AscensionRuleKind.PrepBudgetHours), 0.0001f);
            Assert.AreEqual(-0.2f, top.Total(AscensionRuleKind.VictoryGoldPercent), 0.0001f);
            Assert.AreEqual(0.2f, top.Total(AscensionRuleKind.EnemyCountPercent), 0.0001f);
            Assert.AreEqual(-30f, top.Total(AscensionRuleKind.StartingMorale), 0.0001f);
            Assert.AreEqual(0.3f, top.Total(AscensionRuleKind.BossHealthPercent), 0.0001f);
            Assert.AreEqual(0.2f, top.Total(AscensionRuleKind.BossAttackPercent), 0.0001f);
            Assert.AreEqual(1f, top.Total(AscensionRuleKind.RepairTimePercent), 0.0001f);
            Assert.AreEqual(0.15f, top.Total(AscensionRuleKind.EnemyMoveSpeedPercent), 0.0001f);
            for (int level = 1; level <= table.MaxLevel; level++)
                Assert.IsNotEmpty(table.LevelAt(level).description, "단계 " + level + " 설명이 있어야 한다.");
        }
    }
}
