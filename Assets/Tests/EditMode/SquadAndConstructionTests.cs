using NUnit.Framework;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class SquadTests
    {
        [Test]
        public void TryAdd_StopsAtCapacity()
        {
            var squad = new Squad<string>(2);

            Assert.IsTrue(squad.TryAdd("a"));
            Assert.IsTrue(squad.TryAdd("b"));
            Assert.IsTrue(squad.IsFull);
            Assert.IsFalse(squad.TryAdd("c"));
            Assert.AreEqual(2, squad.Members.Count);
        }

        [Test]
        public void TryAdd_RejectsSameUnitTwice()
        {
            var squad = new Squad<string>(5);
            squad.TryAdd("a");

            Assert.IsFalse(squad.TryAdd("a"));
        }

        [Test]
        public void Leader_IsOptional_AndDoesNotUseMemberSlot()
        {
            var squad = new Squad<string>(1);
            Assert.IsNull(squad.Leader);

            squad.Leader = "hero";

            Assert.IsTrue(squad.TryAdd("soldier"));
        }

        [Test]
        public void Remove_FreesASlot()
        {
            var squad = new Squad<string>(1);
            squad.TryAdd("a");

            Assert.IsTrue(squad.Remove("a"));
            Assert.IsTrue(squad.TryAdd("b"));
        }

        [Test]
        public void Army_UsesLimitsFromConfig()
        {
            SquadConfig config = SquadConfig.Create(maxSquads: 2, maxMembersPerSquad: 12);
            var army = new Army<string>(config);

            Squad<string> first = army.TryAddSquad();
            Assert.AreEqual(12, first.Capacity);
            Assert.IsNotNull(army.TryAddSquad());
            Assert.IsNull(army.TryAddSquad());
            Assert.AreEqual(2, army.Squads.Count);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void SquadConfig_DefaultsToTenMembersPerSquad()
        {
            var config = ScriptableObject.CreateInstance<SquadConfig>();

            Assert.AreEqual(10, config.MaxMembersPerSquad);

            Object.DestroyImmediate(config);
        }
    }

    public class ConstructionProgressTests
    {
        [Test]
        public void ZeroStages_IsCompleteImmediately()
        {
            var construction = new ConstructionProgress(0);

            Assert.IsTrue(construction.IsComplete);
            Assert.AreEqual(1f, construction.Progress01);
        }

        [Test]
        public void AdvanceStage_CompletesAfterTotalStages()
        {
            var construction = new ConstructionProgress(2);
            Assert.IsFalse(construction.IsComplete);
            Assert.AreEqual(0f, construction.Progress01);

            construction.AdvanceStage();
            Assert.IsFalse(construction.IsComplete);
            Assert.AreEqual(0.5f, construction.Progress01, 0.001f);

            construction.AdvanceStage();
            Assert.IsTrue(construction.IsComplete);

            construction.AdvanceStage();
            Assert.AreEqual(0, construction.RemainingStages);
        }
    }
}
