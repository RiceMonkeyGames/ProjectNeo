using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>COMBAT-12 교전 규칙: 원거리 사선 막힘 처리, 근접 링 자리, 자리 표.</summary>
    public class EngagementRulesTests
    {
        [Test]
        public void Decide_LivingEnemyBlock_SwitchesWhenBlockerIsStrikeable_ElseHolds()
        {
            Assert.AreEqual(RangedBlockAction.Attack, RangedOcclusionRules.Decide(ShotBlock.Clear, false));
            Assert.AreEqual(RangedBlockAction.SwitchToBlocker, RangedOcclusionRules.Decide(ShotBlock.LivingEnemy, true));
            Assert.AreEqual(RangedBlockAction.HoldDistance, RangedOcclusionRules.Decide(ShotBlock.LivingEnemy, false));
            Assert.AreEqual(RangedBlockAction.CloseIn, RangedOcclusionRules.Decide(ShotBlock.Structure, false));
            Assert.AreEqual(RangedBlockAction.CloseIn, RangedOcclusionRules.Decide(ShotBlock.Structure, true));
        }

        [Test]
        public void Combine_AnyClearLineIsClear_BothStructureIsStructure_ElseEnemy()
        {
            Assert.AreEqual(ShotBlock.Clear, RangedOcclusionRules.Combine(ShotBlock.Structure, ShotBlock.Clear));
            Assert.AreEqual(ShotBlock.Clear, RangedOcclusionRules.Combine(ShotBlock.Clear, ShotBlock.LivingEnemy));
            Assert.AreEqual(ShotBlock.Structure, RangedOcclusionRules.Combine(ShotBlock.Structure, ShotBlock.Structure));
            Assert.AreEqual(ShotBlock.LivingEnemy, RangedOcclusionRules.Combine(ShotBlock.LivingEnemy, ShotBlock.Structure));
            Assert.AreEqual(ShotBlock.LivingEnemy, RangedOcclusionRules.Combine(ShotBlock.Structure, ShotBlock.LivingEnemy));
            Assert.AreEqual(ShotBlock.LivingEnemy, RangedOcclusionRules.Combine(ShotBlock.LivingEnemy, ShotBlock.LivingEnemy));
        }

        [Test]
        public void InnerRadius_IsBodiesPlusGap_ClampedToReach()
        {
            Assert.AreEqual(0.4f + 0.4f + 0.15f, MeleeRingRules.InnerRadius(0.4f, 0.4f, 0.15f, 2f), 1e-5f);
            // 사거리 0.5m: 표면까지 0.4m(사거리의 80%)로 줄인다.
            Assert.AreEqual(0.4f + 0.4f, MeleeRingRules.InnerRadius(0.4f, 0.4f, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(1.95f, MeleeRingRules.RingRadius(MeleeRingRules.OuterRing, 0.95f, 1f), 1e-5f);
            Assert.AreEqual(0.95f, MeleeRingRules.RingRadius(MeleeRingRules.InnerRing, 0.95f, 1f), 1e-5f);
        }

        [Test]
        public void SlotCount_FitsCircumference_AndIsClamped()
        {
            // 반지름 0.95m 둘레 약 5.97m / 0.9m = 6자리.
            Assert.AreEqual(6, MeleeRingRules.SlotCount(0.95f, 0.9f));
            Assert.AreEqual(1, MeleeRingRules.SlotCount(0.05f, 0.9f));
            Assert.AreEqual(1, MeleeRingRules.SlotCount(1f, 0f));
            Assert.AreEqual(MeleeRingRules.MaxSlots, MeleeRingRules.SlotCount(20f, 0.9f));
        }

        [Test]
        public void SlotOffset_StartsNorthAndTurnsClockwise()
        {
            AssertVector(new Vector3(0f, 0f, 1f), MeleeRingRules.SlotOffset(1f, 0, 4));
            AssertVector(new Vector3(1f, 0f, 0f), MeleeRingRules.SlotOffset(1f, 1, 4));
            AssertVector(new Vector3(0f, 0f, -1f), MeleeRingRules.SlotOffset(1f, 2, 4));
            AssertVector(new Vector3(-1f, 0f, 0f), MeleeRingRules.SlotOffset(1f, 3, 4));
        }

        [Test]
        public void OrderByDistance_NearestSlotFirst()
        {
            var order = new List<int>();
            // 대상 동쪽(+x)에서 오는 공격자: 동(1) 먼저, 그다음 북(0)·남(2), 서(3)가 마지막.
            MeleeRingRules.OrderByDistance(new Vector3(5f, 2f, 0f), Vector3.zero, 1f, 4, order);
            Assert.AreEqual(1, order[0]);
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, new[] { order[1], order[2] });
            Assert.AreEqual(3, order[3]);
        }

        sealed class Unit
        {
            public object Target;
            public bool Alive = true;
        }

        static bool Valid(Unit unit, object target) => unit.Alive && ReferenceEquals(unit.Target, target);

        [Test]
        public void Board_OneUnitPerSlot_AndOneSlotPerUnit()
        {
            var board = new MeleeSlotBoard<Unit>();
            var target = new object();
            var a = new Unit { Target = target };
            var b = new Unit { Target = target };
            board.Take(a, target, 0, 2);
            Assert.IsTrue(board.IsTaken(target, 0, 2, b, Valid), "b sees a's slot as taken");
            Assert.IsFalse(board.IsTaken(target, 0, 2, a, Valid), "own slot is free for the owner");
            Assert.IsFalse(board.IsTaken(target, 1, 2, b, Valid), "other ring");
            board.Take(a, target, 1, 0);
            Assert.IsFalse(board.IsTaken(target, 0, 2, b, Valid), "taking another slot frees the old one");
            Assert.IsTrue(board.TryGet(a, target, out int ring, out int index));
            Assert.AreEqual((1, 0), (ring, index));
            Assert.IsFalse(board.TryGet(a, new object(), out _, out _), "claim belongs to the first target");
            board.Release(a);
            Assert.AreEqual(0, board.Count);
        }

        [Test]
        public void Board_DropsClaimsOfUnitsThatChangedTargetOrDied()
        {
            var board = new MeleeSlotBoard<Unit>();
            var target = new object();
            var a = new Unit { Target = target };
            var b = new Unit { Target = target };
            board.Take(a, target, 0, 1);
            a.Target = new object();
            Assert.IsFalse(board.IsTaken(target, 0, 1, b, Valid), "a now chases someone else");
            Assert.AreEqual(0, board.Count, "stale claim removed");
            var c = new Unit { Target = target };
            board.Take(c, target, 0, 1);
            c.Alive = false;
            Assert.IsFalse(board.IsTaken(target, 0, 1, b, Valid), "dead claimant frees its slot");
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }
    }
}
