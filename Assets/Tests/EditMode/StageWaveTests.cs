using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class StageWaveTests
    {
        static WaveSchedule Schedule(IWaveStartRule rule = null) => new WaveSchedule(new[]
        {
            new WaveTiming(0f, 4), new WaveTiming(30f, 6), new WaveTiming(60f, 2)
        }, rule);

        [Test]
        public void Schedule_ReleasesFirstWaveAtBattleStart_AndCountsPending()
        {
            WaveSchedule schedule = Schedule();
            Assert.AreEqual(12, schedule.PendingUnits);
            var released = new List<int>();
            Assert.AreEqual(1, schedule.Advance(0f, 0, released));
            CollectionAssert.AreEqual(new[] { 0 }, released);
            Assert.AreEqual(8, schedule.PendingUnits);
            Assert.AreEqual(2, schedule.PendingWaves);
            Assert.AreEqual(30f, schedule.SecondsUntilNext, 0.001f);
        }

        [Test]
        public void Schedule_ReleasesEachWaveExactlyOnce_AtItsTime()
        {
            WaveSchedule schedule = Schedule();
            var released = new List<int>();
            schedule.Advance(0f, 0, released);
            for (int i = 0; i < 299; i++) schedule.Advance(0.1f, 4, released);
            Assert.AreEqual(1, released.Count, "second wave must wait for 30s");
            schedule.Advance(0.2f, 4, released);
            CollectionAssert.AreEqual(new[] { 0, 1 }, released);
            // 시간이 흐르지 않으면(멈춤) 아무것도 다시 나오지 않는다.
            for (int i = 0; i < 10; i++) Assert.AreEqual(0, schedule.Advance(0f, 10, released));
            Assert.AreEqual(2, released.Count);
        }

        [Test]
        public void Schedule_LongStep_ReleasesAllRemainingInOrder_ThenNothing()
        {
            WaveSchedule schedule = Schedule();
            var released = new List<int>();
            Assert.AreEqual(3, schedule.Advance(500f, 0, released));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, released);
            Assert.IsTrue(schedule.AllReleased);
            Assert.AreEqual(0, schedule.PendingUnits);
            Assert.AreEqual(-1f, schedule.SecondsUntilNext);
            Assert.AreEqual(0, schedule.Advance(500f, 0, released));
        }

        [Test]
        public void Schedule_IgnoresNegativeTime_AndResetStartsOver()
        {
            WaveSchedule schedule = Schedule();
            schedule.Advance(40f, 0, null);
            schedule.Advance(-100f, 0, null);
            Assert.AreEqual(40f, schedule.ElapsedSeconds, 0.001f);
            Assert.AreEqual(2, schedule.ReleasedWaves);
            schedule.Reset();
            Assert.AreEqual(0, schedule.ReleasedWaves);
            Assert.AreEqual(12, schedule.PendingUnits);
            var released = new List<int>();
            schedule.Advance(0f, 0, released);
            CollectionAssert.AreEqual(new[] { 0 }, released);
        }

        [Test]
        public void Schedule_EmptyTimetable_IsAlreadyComplete()
        {
            var schedule = new WaveSchedule(null);
            Assert.IsTrue(schedule.AllReleased);
            Assert.AreEqual(0, schedule.Advance(10f, 0, null));
            Assert.AreEqual(0, schedule.PendingUnits);
        }

        sealed class EmptyFieldRule : IWaveStartRule
        {
            public bool IsDue(in WaveStartContext context) =>
                context.WaveIndex == 0 || context.EnemiesOnField == 0 || context.ElapsedSeconds >= context.StartSeconds;
        }

        [Test]
        public void Schedule_StartRuleCanBeReplaced_WithoutChangingTheTimetable()
        {
            WaveSchedule schedule = Schedule(new EmptyFieldRule());
            var released = new List<int>();
            schedule.Advance(0f, 0, released);
            // 방금 나온 4명이 전장에 있으므로 다음 웨이브는 기다린다.
            CollectionAssert.AreEqual(new[] { 0 }, released);
            schedule.Advance(1f, 3, released);
            Assert.AreEqual(1, released.Count);
            schedule.Advance(1f, 0, released);
            CollectionAssert.AreEqual(new[] { 0, 1 }, released);
        }

        [TestCase(2, false, false)]
        [TestCase(2, true, true)]
        [TestCase(0, false, true)]
        [TestCase(0, true, true)]
        public void Retreat_WaitsForRemainingWaves_UnlessSwitchedOn(int pendingWaves, bool allowWhilePending, bool expected)
        {
            Assert.AreEqual(expected, WaveSchedule.RetreatAllowed(pendingWaves, allowWhilePending));
        }

        [Test]
        public void Plan_FromStage_ResolvesSpawnPoints_AndDropsEmptyEntries()
        {
            var prefab = new GameObject("enemy");
            var left = SpawnPointDefinition.Create("Left", new Vector2(25f, 90f));
            StageDefinition stage = StageDefinition.Create("Test",
                new StageWave { startSeconds = 0f, groups = new[]
                {
                    new StageWaveGroup { enemyPrefab = prefab, count = 3, spawnPoint = left },
                    new StageWaveGroup { enemyPrefab = prefab, count = 2 },
                    new StageWaveGroup { enemyPrefab = null, count = 5 },
                    new StageWaveGroup { enemyPrefab = prefab, count = 0 }
                } },
                new StageWave { startSeconds = 20f, groups = new StageWaveGroup[0] },
                new StageWave { startSeconds = 40f, groups = new[] { new StageWaveGroup { enemyPrefab = prefab, count = 4, spawnPoint = left } } });
            try
            {
                EncounterPlan plan = EncounterPlan.FromStage(stage, new Vector2(40f, 90f));
                Assert.AreEqual(2, plan.Waves.Count, "wave without valid groups is dropped");
                Assert.AreEqual(9, plan.TotalUnits);
                Assert.AreEqual(2, plan.Waves[0].Groups.Count);
                Assert.AreEqual(new Vector2(25f, 90f), plan.Waves[0].Groups[0].Cell);
                Assert.AreEqual(new Vector2(40f, 90f), plan.Waves[0].Groups[1].Cell, "missing spawn point uses the default cell");
                WaveTiming[] timings = plan.Timings();
                Assert.AreEqual(40f, timings[1].StartSeconds);
                Assert.AreEqual(4, timings[1].UnitCount);
            }
            finally
            {
                Object.DestroyImmediate(stage); Object.DestroyImmediate(left); Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Plan_FromLegacyGroups_IsOneWaveAtBattleStart()
        {
            var prefab = new GameObject("enemy");
            try
            {
                EncounterPlan plan = EncounterPlan.FromGroups(new[]
                {
                    new SiegeUnitGroup { unitPrefab = prefab, count = 6 },
                    new SiegeUnitGroup { unitPrefab = null, count = 3 },
                    new SiegeUnitGroup { unitPrefab = prefab, count = 2 }
                }, new Vector2(40f, 90f));
                Assert.AreEqual(1, plan.Waves.Count);
                Assert.AreEqual(0f, plan.Waves[0].StartSeconds);
                Assert.AreEqual(8, plan.TotalUnits);
                Assert.AreEqual(0, EncounterPlan.FromGroups(null, Vector2.zero).Waves.Count);
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void Catalog_MapsStageNumbers_AndRepeatsTheLastStage()
        {
            StageDefinition first = StageDefinition.Create("1"), second = StageDefinition.Create("2");
            StageCatalog catalog = StageCatalog.Create(first, second);
            StageCatalog empty = StageCatalog.Create();
            try
            {
                Assert.AreSame(first, catalog.StageFor(1));
                Assert.AreSame(second, catalog.StageFor(2));
                Assert.AreSame(second, catalog.StageFor(7));
                Assert.AreSame(first, catalog.StageFor(0));
                Assert.IsNull(empty.StageFor(1));
            }
            finally
            {
                Object.DestroyImmediate(first); Object.DestroyImmediate(second);
                Object.DestroyImmediate(catalog); Object.DestroyImmediate(empty);
            }
        }
    }
}
