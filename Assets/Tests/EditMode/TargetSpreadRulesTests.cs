using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>COMBAT-17 규칙: 근접 혼잡 배율, 원거리 과잉 사격 배율, 크레넬 자리 옮기기 후보, 근접 자리 수 세기.</summary>
    public class TargetSpreadRulesTests
    {
        [Test]
        public void CongestionFactor_OnlyWhenInnerRingIsFull()
        {
            Assert.AreEqual(1f, TargetSpreadRules.CongestionFactor(5, 6, 1.5f), 1e-5f);
            Assert.AreEqual(1.5f, TargetSpreadRules.CongestionFactor(6, 6, 1.5f), 1e-5f);
            Assert.AreEqual(1.5f, TargetSpreadRules.CongestionFactor(9, 6, 1.5f), 1e-5f);
            Assert.AreEqual(1f, TargetSpreadRules.CongestionFactor(3, 0, 1.5f), 1e-5f, "no capacity is no penalty");
            Assert.AreEqual(1f, TargetSpreadRules.CongestionFactor(6, 6, 0.5f), 1e-5f, "factor never lowers the score");
        }

        [Test]
        public void CongestedTarget_LosesToFreeTargetWithinFactor()
        {
            // 6m 앞 꽉 찬 적(1.5배 → 9m처럼 보인다)보다 8.5m 뒤 빈 적이 먼저다. 10m면 꽉 찬 적이 그대로 먼저다.
            float full = RangedTargetPriorityRules.Score(36f, 1f, TargetSpreadRules.CongestionFactor(6, 6, 1.5f));
            Assert.Less(RangedTargetPriorityRules.Score(8.5f * 8.5f, 1f, 1f), full);
            Assert.Greater(RangedTargetPriorityRules.Score(100f, 1f, 1f), full);
        }

        [Test]
        public void IsFocusTarget_LadderBreakerAndBoss()
        {
            Assert.IsTrue(TargetSpreadRules.IsFocusTarget(TargetThreat.LadderClimber, false));
            Assert.IsTrue(TargetSpreadRules.IsFocusTarget(TargetThreat.SiegeBreaker, false));
            Assert.IsTrue(TargetSpreadRules.IsFocusTarget(TargetThreat.None, true));
            Assert.IsFalse(TargetSpreadRules.IsFocusTarget(TargetThreat.Ranged, false), "enemy archers are spread");
            Assert.IsFalse(TargetSpreadRules.IsFocusTarget(TargetThreat.None, false));
        }

        [Test]
        public void OverkillFactor_StepsPerShooterBeyondThreshold()
        {
            Assert.AreEqual(1f, TargetSpreadRules.OverkillFactor(2, 3, 1.25f, false), 1e-5f, "I would be the 3rd shooter");
            Assert.AreEqual(1.25f, TargetSpreadRules.OverkillFactor(3, 3, 1.25f, false), 1e-5f, "I would be the 4th shooter");
            Assert.AreEqual(1.25f * 1.25f, TargetSpreadRules.OverkillFactor(4, 3, 1.25f, false), 1e-5f);
            Assert.AreEqual(1f, TargetSpreadRules.OverkillFactor(10, 3, 1.25f, true), 1e-5f, "focus targets keep focus fire");
            Assert.AreEqual(1f, TargetSpreadRules.OverkillFactor(10, 0, 1.25f, false), 1e-5f, "threshold 0 disables");
            Assert.AreEqual(1f, TargetSpreadRules.OverkillFactor(5, 3, 0.5f, false), 1e-5f, "factor never lowers the score");
        }

        [Test]
        public void CrenelReposition_WaitsForDelay()
        {
            Assert.IsFalse(CrenelRepositionRules.ShouldReposition(1.9f, 2f));
            Assert.IsTrue(CrenelRepositionRules.ShouldReposition(2f, 2f));
            Assert.IsFalse(CrenelRepositionRules.ShouldReposition(10f, 0f), "delay 0 disables");
        }

        [Test]
        public void CrenelCandidate_WithinAnchorRadius_SameLevel_NotCurrentSpot()
        {
            Vector3 anchor = new Vector3(0f, 3f, 0f), self = new Vector3(0.2f, 3f, 0f);
            Assert.IsTrue(CrenelRepositionRules.IsCandidate(new Vector3(2f, 3.1f, 0f), anchor, self, 4f, 1f));
            Assert.IsFalse(CrenelRepositionRules.IsCandidate(new Vector3(4.5f, 3f, 0f), anchor, self, 4f, 1f), "beyond radius");
            Assert.IsFalse(CrenelRepositionRules.IsCandidate(new Vector3(2f, 0f, 0f), anchor, self, 4f, 1f), "other level");
            Assert.IsFalse(CrenelRepositionRules.IsCandidate(new Vector3(0.3f, 3f, 0.1f), anchor, self, 4f, 1f), "already there");
        }

        [Test]
        public void SlotBoard_CountsValidClaimsPerTargetAndRing()
        {
            var board = new MeleeSlotBoard<string>();
            object a = new object(), b = new object();
            board.Take("u1", a, MeleeRingRules.InnerRing, 0);
            board.Take("u2", a, MeleeRingRules.InnerRing, 1);
            board.Take("u3", a, MeleeRingRules.OuterRing, 0);
            board.Take("u4", b, MeleeRingRules.InnerRing, 0);
            board.Take("stale", a, MeleeRingRules.InnerRing, 2);
            int revision = board.Revision;
            var counts = new Dictionary<object, int>();
            board.CountByTarget(MeleeRingRules.InnerRing, (unit, target) => unit != "stale", counts);
            Assert.AreEqual(2, counts[a]);
            Assert.AreEqual(1, counts[b]);
            Assert.AreEqual(4, board.Count, "stale claim is removed");
            board.Release("u1");
            Assert.Greater(board.Revision, revision, "revision moves on release");
        }

        [Test]
        public void Settings_SpreadDefaults_AndClamp()
        {
            UnitAISettings defaults = UnitAISettings.Create();
            UnitAISettings clamped = UnitAISettings.Create(meleeCongestionDistanceFactor: 0.2f, rangedOverkillThreshold: -2,
                rangedOverkillDistanceFactor: 9f, crenelRepositionDelay: -1f);
            try
            {
                Assert.IsTrue(defaults.TargetSpread);
                Assert.AreEqual(1.5f, defaults.MeleeCongestionDistanceFactor, 1e-5f);
                Assert.AreEqual(3, defaults.RangedOverkillThreshold);
                Assert.AreEqual(1.25f, defaults.RangedOverkillDistanceFactor, 1e-5f);
                Assert.AreEqual(2f, defaults.CrenelRepositionDelay, 1e-5f);
                Assert.AreEqual(4f, defaults.CrenelRepositionRadius, 1e-5f);
                Assert.AreEqual(1f, clamped.MeleeCongestionDistanceFactor, 1e-5f);
                Assert.AreEqual(0, clamped.RangedOverkillThreshold);
                Assert.AreEqual(4f, clamped.RangedOverkillDistanceFactor, 1e-5f);
                Assert.AreEqual(0f, clamped.CrenelRepositionDelay, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(defaults);
                Object.DestroyImmediate(clamped);
            }
        }
    }
}
