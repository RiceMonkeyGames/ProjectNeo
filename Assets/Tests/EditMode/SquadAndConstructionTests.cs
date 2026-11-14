using NUnit.Framework;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class SquadTests
    {
        [Test]
        public void PlacementMemory_RemembersPerSquad_ForgetsAndShiftsOnRemoval()
        {
            var memory = new SquadPlacementMemory();
            Assert.IsFalse(memory.TryGet(0, out _));
            memory.Remember(0, new Vector3(1f, 0f, 1f), 90f);
            memory.Remember(2, new Vector3(3f, 3f, 3f), 0f);
            memory.Remember(-1, Vector3.one, 0f);
            Assert.IsTrue(memory.TryGet(0, out SquadPlacementMemory.Entry first));
            Assert.AreEqual(new Vector3(1f, 0f, 1f), first.Position);
            Assert.AreEqual(90f, first.Facing);
            Assert.IsFalse(memory.TryGet(1, out _));
            memory.Remember(0, new Vector3(5f, 0f, 5f), 180f);
            Assert.IsTrue(memory.TryGet(0, out first));
            Assert.AreEqual(new Vector3(5f, 0f, 5f), first.Position, "마지막 배치 자리만 남는다.");

            memory.RemoveSquad(1);
            Assert.IsTrue(memory.TryGet(1, out SquadPlacementMemory.Entry shifted), "뒤 부대 기억이 당겨진다.");
            Assert.AreEqual(new Vector3(3f, 3f, 3f), shifted.Position);
            Assert.IsFalse(memory.TryGet(2, out _));
            memory.Forget(0);
            Assert.IsFalse(memory.TryGet(0, out _));
            memory.Clear();
            Assert.IsFalse(memory.TryGet(1, out _));
        }

        [Test]
        public void CastleInterior_BoundsWrapPiecesAndIncludeEdges()
        {
            Assert.IsFalse(CastleInteriorRules.TryGetBounds(new Vector2[0], 1f, out _));
            var pieces = new[] { new Vector2(10f, 10f), new Vector2(40f, 10f), new Vector2(10f, 50f), new Vector2(40f, 50f) };
            Assert.IsTrue(CastleInteriorRules.TryGetBounds(pieces, 1.5f, out Rect bounds));
            Assert.AreEqual(8.5f, bounds.xMin); Assert.AreEqual(41.5f, bounds.xMax);
            Assert.AreEqual(8.5f, bounds.yMin); Assert.AreEqual(51.5f, bounds.yMax);
            Assert.IsTrue(CastleInteriorRules.Contains(bounds, new Vector2(25f, 30f)));
            Assert.IsTrue(CastleInteriorRules.Contains(bounds, new Vector2(41.5f, 51.5f)), "성벽 바깥 끝(성벽 위)도 안이다.");
            Assert.IsFalse(CastleInteriorRules.Contains(bounds, new Vector2(25f, 52f)));
            Assert.IsFalse(CastleInteriorRules.Contains(bounds, new Vector2(8f, 30f)));
        }

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
