using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>보스(SIEGE-09)의 순수 규칙: 능력치, 지휘형 강화·약화, 결투형 선호, 편성·적 목록, 보상·점수.</summary>
    public class BossRulesTests
    {
        sealed class ZeroRandom : IRandomSource
        {
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => 0.99f;
        }

        readonly List<Object> created = new List<Object>();
        GameObject basePrefab;
        EnemyDefinition baseEnemy;

        T Track<T>(T value) where T : Object { created.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            basePrefab = Track(new GameObject("enemy_ladder"));
            basePrefab.SetActive(false);
            baseEnemy = Track(EnemyDefinition.Create("enemy_ladder", "Ladder", EnemyRole.GroundLadder, basePrefab));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in created) if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        BossDefinition Boss(BossRole role = BossRole.Siege, float health = 400f, float attack = 21f, float defense = 4f,
            float range = 0f, float speed = 1f, float structure = 1f, int gold = 0, params BossMaterialReward[] materials) =>
            Track(BossDefinition.Create("boss_test", "Test Boss", role, baseEnemy, 1.5f, health, attack, defense, range,
                speed, structure, gold, materials));

        static StatSheet MeleeStats()
        {
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, 80f);
            stats.SetBase(StatType.Attack, 14f);
            stats.SetBase(StatType.Defense, 2f);
            stats.SetBase(StatType.AttackRange, 1.5f);
            stats.SetBase(StatType.MoveSpeed, 3.5f);
            return stats;
        }

        [Test]
        public void StatModifiers_ReachTargetValues_AndKeepZeroTargets()
        {
            StatSheet stats = MeleeStats();
            BossDefinition boss = Boss(health: 400f, attack: 21f, defense: 4f, range: 0f, speed: 1.3f);
            stats.AddModifiers(BossRules.StatModifiers(boss, stats.GetBase), boss);

            Assert.AreEqual(400f, stats.GetValue(StatType.MaxHealth), 0.001f);
            Assert.AreEqual(21f, stats.GetValue(StatType.Attack), 0.001f);
            Assert.AreEqual(4f, stats.GetValue(StatType.Defense), 0.001f);
            Assert.AreEqual(1.5f, stats.GetValue(StatType.AttackRange), 0.001f, "목표값 0이면 바탕 사거리 그대로");
            Assert.AreEqual(3.5f * 1.3f, stats.GetValue(StatType.MoveSpeed), 0.001f);
        }

        [Test]
        public void StatModifiers_AimSpreadTarget_OnlyWhenSet()
        {
            StatSheet stats = MeleeStats();
            stats.SetBase(StatType.AimSpread, 3f);
            BossDefinition marksman = Boss(range: 30f).WithAimSpread(1f);
            stats.AddModifiers(BossRules.StatModifiers(marksman, stats.GetBase), marksman);
            Assert.AreEqual(1f, stats.GetValue(StatType.AimSpread), 0.001f, "명사수는 탄퍼짐 1°");
            Assert.AreEqual(30f, stats.GetValue(StatType.AttackRange), 0.001f);

            StatSheet plain = MeleeStats();
            plain.SetBase(StatType.AimSpread, 3f);
            BossDefinition other = Boss();
            plain.AddModifiers(BossRules.StatModifiers(other, plain.GetBase), other);
            Assert.AreEqual(3f, plain.GetValue(StatType.AimSpread), 0.001f, "목표값 0이면 바탕 탄퍼짐 그대로");
        }

        [Test]
        public void StatModifiers_AscensionPercentMultipliesBossValue()
        {
            StatSheet stats = MeleeStats();
            BossDefinition boss = Boss(health: 400f);
            stats.AddModifiers(BossRules.StatModifiers(boss, stats.GetBase), boss);
            stats.AddModifier(StatModifier.Percent(StatType.MaxHealth, 0.3f), this);
            Assert.AreEqual(520f, stats.GetValue(StatType.MaxHealth), 0.001f, "보스 400에 승천 +30%");
        }

        [Test]
        public void Commander_AuraAndDeathPenalty()
        {
            BossDefinition commander = Boss(BossRole.Commander).WithAura(12f, 0.2f, 2f, 0.2f);
            List<StatModifier> aura = BossRules.AuraModifiers(commander);
            Assert.AreEqual(2, aura.Count);
            StatSheet ally = MeleeStats();
            ally.AddModifiers(aura, commander);
            Assert.AreEqual(14f * 1.2f, ally.GetValue(StatType.Attack), 0.001f);
            Assert.AreEqual(4f, ally.GetValue(StatType.Defense), 0.001f);

            Assert.IsTrue(BossRules.InAura(commander, 144f), "반경 경계는 안");
            Assert.IsFalse(BossRules.InAura(commander, 144.1f));

            StatSheet mate = MeleeStats();
            mate.AddModifiers(BossRules.DeathPenaltyModifiers(commander), this);
            Assert.AreEqual(14f * 0.8f, mate.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void NonCommander_HasNoAuraOrPenalty()
        {
            BossDefinition siege = Boss(BossRole.Siege).WithAura(12f, 0.2f, 2f, 0.2f);
            Assert.IsEmpty(BossRules.AuraModifiers(siege));
            Assert.IsEmpty(BossRules.DeathPenaltyModifiers(siege));
            Assert.IsFalse(BossRules.InAura(siege, 1f));
        }

        [Test]
        public void Duelist_PrefersHeroes_OthersDoNot()
        {
            BossDefinition duelist = Boss(BossRole.Duelist).WithHeroPreference(3f);
            Assert.AreEqual(1f / 9f, BossRules.TargetDistanceScale(duelist, true), 0.0001f);
            Assert.AreEqual(1f, BossRules.TargetDistanceScale(duelist, false));
            Assert.AreEqual(1f, BossRules.TargetDistanceScale(Boss(BossRole.Siege), true));
            Assert.AreEqual(1f, BossRules.TargetDistanceScale(null, true));
        }

        [Test]
        public void StructureDamage_UsesSiegeMultiplier()
        {
            Assert.AreEqual(42f, BossRules.StructureDamage(21f, Boss(structure: 2f)), 0.001f);
            Assert.AreEqual(21f, BossRules.StructureDamage(21f, null), 0.001f);
        }

        [Test]
        public void FlyerPriority_PicksNearestHero_ElseFallsBack()
        {
            WallTopFirstFlyerTargetStrategy fallback = Track(WallTopFirstFlyerTargetStrategy.Create());
            var candidates = new[]
            {
                new FlyerTargetCandidate(0, 4f, true, false),
                new FlyerTargetCandidate(1, 100f, false, true),
                new FlyerTargetCandidate(2, 64f, false, true),
            };
            Assert.AreEqual(2, FlyerPriorityTargeting.Select(candidates, fallback), "가장 가까운 영웅");
            var noHero = new[] { new FlyerTargetCandidate(0, 9f, false), new FlyerTargetCandidate(1, 16f, true) };
            Assert.AreEqual(1, FlyerPriorityTargeting.Select(noHero, fallback), "영웅이 없으면 원래 전략(성벽 위 우선)");
        }

        StageDefinition StageWithBoss(BossDefinition boss, bool hiddenWave = false)
        {
            var groups = new[] { new StageWaveGroup { enemy = baseEnemy, count = 4, hidden = hiddenWave } };
            return Track(StageDefinition.Create("Stage",
                new StageWave { startSeconds = 0f, groups = groups },
                new StageWave { startSeconds = 30f, groups = groups, boss = boss }));
        }

        [Test]
        public void EncounterPlan_AddsBossAsLastGroupOfItsWave()
        {
            BossDefinition boss = Boss();
            EncounterPlan plan = EncounterPlan.FromStage(StageWithBoss(boss), new Vector2(3f, 4f));
            Assert.AreEqual(9, plan.TotalUnits);
            EncounterGroup last = plan.Waves[1].Groups[plan.Waves[1].Groups.Count - 1];
            Assert.IsTrue(last.IsBoss);
            Assert.AreSame(boss, last.Boss);
            Assert.AreSame(basePrefab, last.Prefab);
            Assert.AreEqual(1, last.Count);
            Assert.AreEqual(new Vector2(3f, 4f), last.Cell, "지점이 없으면 기본 스폰 칸");
            Assert.IsFalse(plan.Waves[0].Groups[0].IsBoss);
        }

        [Test]
        public void EncounterPlan_WithCountsKeepsSingleBoss()
        {
            EncounterPlan plan = EncounterPlan.FromStage(StageWithBoss(Boss()), Vector2.zero).WithCounts(count => count * 2);
            EncounterWave wave = plan.Waves[1];
            Assert.AreEqual(8, wave.Groups[0].Count);
            Assert.IsTrue(wave.Groups[1].IsBoss);
            Assert.AreEqual(1, wave.Groups[1].Count);
        }

        [Test]
        public void EncounterPlan_SkipsBossWithoutPrefab()
        {
            BossDefinition broken = Track(BossDefinition.Create("boss_none", "None", BossRole.Siege, null, 1f, 100f, 10f, 1f));
            EncounterPlan plan = EncounterPlan.FromStage(StageWithBoss(broken), Vector2.zero);
            Assert.AreEqual(8, plan.TotalUnits);
        }

        [Test]
        public void EnemyIntel_ShowsBossEvenInHiddenWave()
        {
            EnemyIntel intel = EnemyIntel.FromPlan(EncounterPlan.FromStage(StageWithBoss(Boss(), hiddenWave: true), Vector2.zero));
            StringAssert.Contains("보스: Test Boss × 1", intel.Describe());
            StringAssert.Contains("? × 8", intel.Describe());
        }

        [Test]
        public void StageReward_PaysBossRewardOnly()
        {
            MaterialDefinition hide = Track(MaterialDefinition.Create("hide"));
            MaterialDefinition bone = Track(MaterialDefinition.Create("bone"));
            StageRewardTable table = Track(StageRewardTable.Create(50, 10,
                Track(EnemyCatalog.Create(Track(EnemyDefinition.Create("enemy_ladder", "Ladder", EnemyRole.GroundLadder, basePrefab, 3, new EnemyMaterialDrop(bone, 1))))), 0f, 0, new WeightedEntry<ItemDefinition>[0], 0, 0, 1, 1));
            BossDefinition boss = Boss(gold: 30, materials: new BossMaterialReward(hide, 2));

            var result = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.AddKill(boss.Id);
            result.AddBossKill(boss);
            StageReward reward = StageReward.Roll(table, 1, result, new ZeroRandom());

            Assert.AreEqual(50 + 30, reward.Gold, "스테이지 골드 + 보스 골드. 보스 ID는 처치 표에 없다");
            Assert.AreEqual(2, reward.Materials[hide]);
            Assert.IsFalse(reward.Materials.ContainsKey(bone));
        }

        [Test]
        public void ScoreTally_CountsBossKillsEvenOnDefeat()
        {
            BossDefinition boss = Boss();
            var tally = new RunScoreTally();
            var win = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            win.AddKill(boss.Id);
            win.AddBossKill(boss);
            tally.Record(win);
            var loss = new BattleResult(2, BattleOutcome.Defeat, BattleEndReason.PlayerDied);
            loss.AddBossKill(boss);
            tally.Record(loss);
            Assert.AreEqual(2, tally.BossesKilled);
            Assert.AreEqual(1, tally.EnemiesKilled, "보스도 처치 수에 든다");
            Assert.AreEqual(1, tally.StagesCleared);
        }
    }
}
