using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-17 사다리 고르기 점수·막힘 판단과 성벽 파괴병 자리 나누기 규칙.</summary>
    public class SiegeApproachRulesTests
    {
        [Test]
        public void LadderScore_AddsCrowdAndAvoidPenalty_AndKeepsCurrentLadder()
        {
            Assert.AreEqual(10f, LadderChoiceRules.Score(10f, 0, false, false, 4f, 30f));
            Assert.AreEqual(18f, LadderChoiceRules.Score(10f, 2, false, false, 4f, 30f));
            Assert.AreEqual(14f, LadderChoiceRules.Score(10f, 2, true, false, 4f, 30f));
            Assert.AreEqual(40f, LadderChoiceRules.Score(10f, 0, false, true, 4f, 30f));
            Assert.AreEqual(10f, LadderChoiceRules.Score(10f, -1, false, false, 4f, 30f), "A negative crowd never lowers the score.");
        }

        [Test]
        public void LadderNextBest_WalksCandidatesInScoreOrder_SkippingUnusable()
        {
            float[] scores = { 12f, float.PositiveInfinity, 5f, 8f };
            bool[] excluded = new bool[4];
            var order = new List<int>();
            int index;
            while ((index = LadderChoiceRules.NextBest(scores, excluded, scores.Length)) >= 0)
            {
                order.Add(index);
                excluded[index] = true;
            }
            CollectionAssert.AreEqual(new[] { 2, 3, 0 }, order);
        }

        [Test]
        public void LadderStall_ApproachOrLadderActivityResetsTimer()
        {
            var tracker = new LadderStallTracker(0.5f);
            Assert.IsFalse(tracker.IsStalled(100f, 6f));
            tracker.Reset(0f, 10f);
            tracker.Observe(3f, 9.8f, false);
            Assert.IsTrue(tracker.IsStalled(6f, 6f), "Less than the minimum approach is not progress.");
            tracker.Observe(4f, 8f, false);
            Assert.IsFalse(tracker.IsStalled(9f, 6f));
            tracker.Observe(9f, 8f, true);
            Assert.IsFalse(tracker.IsStalled(14f, 6f), "Ladder activity (raising/climbing) keeps a waiting unit committed.");
            Assert.IsTrue(tracker.IsStalled(15f, 6f));
            Assert.IsFalse(tracker.IsStalled(15f, 0f), "Zero disables stall detection.");
            tracker.Stop();
            Assert.IsFalse(tracker.IsStalled(100f, 6f));
        }

        [Test]
        public void LadderStall_PushedBackDoesNotCountAsProgress()
        {
            var tracker = new LadderStallTracker(0.5f);
            tracker.Reset(0f, 5f);
            tracker.Observe(2f, 7f, false);
            tracker.Observe(4f, 6f, false);
            Assert.IsTrue(tracker.IsStalled(6f, 6f), "Only getting closer than the best distance so far is progress.");
        }

        [TestCase(5f, 0.6f, 1.2f, 4)]
        [TestCase(3f, 0.6f, 1.2f, 2)]
        [TestCase(10f, 0.6f, 1.2f, 8)]
        [TestCase(1f, 0.6f, 1.2f, 1)]
        [TestCase(7.5f, 0.6f, 1.2f, 6)]
        public void SlotCount_GrowsWithFaceLength(float face, float margin, float spacing, int expected)
        {
            Assert.AreEqual(expected, WallBreakerSpreadRules.SlotCount(face, margin, spacing));
        }

        [Test]
        public void SlotOffsets_SpreadEvenlyWithinMargins_AtLeastSpacingApart()
        {
            const float face = 5f, margin = 0.6f, spacing = 1.2f;
            int count = WallBreakerSpreadRules.SlotCount(face, margin, spacing);
            Assert.AreEqual(margin, WallBreakerSpreadRules.SlotOffset(0, count, face, margin), 1e-4f);
            Assert.AreEqual(face - margin, WallBreakerSpreadRules.SlotOffset(count - 1, count, face, margin), 1e-4f);
            for (int i = 1; i < count; i++)
                Assert.GreaterOrEqual(WallBreakerSpreadRules.SlotOffset(i, count, face, margin)
                    - WallBreakerSpreadRules.SlotOffset(i - 1, count, face, margin), spacing - 1e-4f);
            Assert.AreEqual(0.5f, WallBreakerSpreadRules.SlotOffset(0, 1, 1f, 0.6f), 1e-4f, "A short face uses its center.");
        }

        [Test]
        public void SlotClaims_CountHorizontalNeighbours_AndPenalizeScore()
        {
            var claims = new List<Vector3> { new Vector3(0f, 3f, 0.5f), new Vector3(2f, 0f, 0f), new Vector3(0.9f, 0f, 0f) };
            Assert.AreEqual(2, WallBreakerSpreadRules.CountClaims(Vector3.zero, claims, 1.2f), "Height difference is ignored.");
            Assert.AreEqual(0, WallBreakerSpreadRules.CountClaims(new Vector3(5f, 0f, 0f), claims, 1.2f));
            float free = WallBreakerSpreadRules.Score(4f, 0, 5f);
            float taken = WallBreakerSpreadRules.Score(1f, 1, 5f);
            Assert.Less(free, taken, "A free slot a little farther beats a nearer slot already taken.");
        }
    }
}
