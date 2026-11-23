using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>COMBAT-14 규칙: 원거리 위협 우선순위, 근접 위협 대응과 짧은 후퇴, 자리 방향 복귀.</summary>
    public class RangedThreatRulesTests
    {
        [Test]
        public void Classify_LadderBeatsReported_ReportedBeatsRanged()
        {
            Assert.AreEqual(TargetThreat.LadderClimber, RangedTargetPriorityRules.Classify(true, TargetThreat.SiegeBreaker, true));
            Assert.AreEqual(TargetThreat.SiegeBreaker, RangedTargetPriorityRules.Classify(false, TargetThreat.SiegeBreaker, true));
            Assert.AreEqual(TargetThreat.Ranged, RangedTargetPriorityRules.Classify(false, TargetThreat.None, true));
            Assert.AreEqual(TargetThreat.None, RangedTargetPriorityRules.Classify(false, TargetThreat.None, false));
        }

        [Test]
        public void DistanceFactor_PicksPerThreat_AndClamps()
        {
            Assert.AreEqual(0.5f, RangedTargetPriorityRules.DistanceFactor(TargetThreat.LadderClimber, 0.5f, 0.6f, 0.75f), 1e-5f);
            Assert.AreEqual(0.6f, RangedTargetPriorityRules.DistanceFactor(TargetThreat.SiegeBreaker, 0.5f, 0.6f, 0.75f), 1e-5f);
            Assert.AreEqual(0.75f, RangedTargetPriorityRules.DistanceFactor(TargetThreat.Ranged, 0.5f, 0.6f, 0.75f), 1e-5f);
            Assert.AreEqual(1f, RangedTargetPriorityRules.DistanceFactor(TargetThreat.None, 0.5f, 0.6f, 0.75f), 1e-5f);
            Assert.AreEqual(RangedTargetPriorityRules.MinimumFactor,
                RangedTargetPriorityRules.DistanceFactor(TargetThreat.LadderClimber, 0f, 0.6f, 0.75f), 1e-5f);
            Assert.AreEqual(1f, RangedTargetPriorityRules.DistanceFactor(TargetThreat.Ranged, 0.5f, 0.6f, 3f), 1e-5f);
        }

        [Test]
        public void Score_SquaresFactor_AndKeepsBossPreference()
        {
            // 12m 사다리 적(배율 0.5)은 6m 일반 적과 같은 점수, 8m 일반 적보다 먼저다.
            float ladder = RangedTargetPriorityRules.Score(144f, 1f, 0.5f);
            Assert.AreEqual(36f, ladder, 1e-4f);
            Assert.Less(ladder, RangedTargetPriorityRules.Score(64f, 1f, 1f));
            // 배율이 모두 1이면 제곱 거리 그대로다(예전과 같다).
            Assert.AreEqual(64f, RangedTargetPriorityRules.Score(64f, 1f, 1f), 1e-4f);
            // 보스 선호 배율은 곱해서 함께 쓴다.
            Assert.AreEqual(9f, RangedTargetPriorityRules.Score(144f, 0.25f, 0.5f), 1e-4f);
        }

        [Test]
        public void IsHigherThreat_OnlyWhenFactorIsClearlySmaller()
        {
            Assert.IsTrue(RangedTargetPriorityRules.IsHigherThreat(1f, 0.5f));
            Assert.IsTrue(RangedTargetPriorityRules.IsHigherThreat(0.75f, 0.6f));
            Assert.IsFalse(RangedTargetPriorityRules.IsHigherThreat(0.5f, 0.5f));
            Assert.IsFalse(RangedTargetPriorityRules.IsHigherThreat(0.5f, 0.75f));
        }

        [Test]
        public void Settings_ThreatFactor_IsOneWhenPriorityIsOff()
        {
            UnitAISettings on = UnitAISettings.Create(ladderClimberDistanceFactor: 0.4f);
            UnitAISettings off = UnitAISettings.Create(rangedThreatPriority: false);
            try
            {
                Assert.AreEqual(0.4f, on.ThreatDistanceFactor(TargetThreat.LadderClimber), 1e-5f);
                Assert.AreEqual(1f, on.ThreatDistanceFactor(TargetThreat.None), 1e-5f);
                Assert.AreEqual(1f, off.ThreatDistanceFactor(TargetThreat.LadderClimber), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(on);
                Object.DestroyImmediate(off);
            }
        }

        [Test]
        public void IsMeleeThreat_RangedSelf_MeleeCandidate_WithinRadiusInclusive()
        {
            Assert.IsTrue(RangedMeleeThreatRules.IsMeleeThreat(true, true, 9f, 3f));
            Assert.IsTrue(RangedMeleeThreatRules.IsMeleeThreat(true, true, 4f, 3f));
            Assert.IsFalse(RangedMeleeThreatRules.IsMeleeThreat(true, true, 9.01f, 3f));
            Assert.IsFalse(RangedMeleeThreatRules.IsMeleeThreat(false, true, 1f, 3f), "melee self does not react");
            Assert.IsFalse(RangedMeleeThreatRules.IsMeleeThreat(true, false, 1f, 3f), "ranged candidate is not a melee threat");
            Assert.IsFalse(RangedMeleeThreatRules.IsMeleeThreat(true, true, 0f, 0f), "radius 0 disables");
        }

        [Test]
        public void FallbackPoint_MovesHorizontallyAwayFromThreat()
        {
            Vector3 point = RangedMeleeThreatRules.FallbackPoint(new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 2f),
                Vector3.back, 3f);
            Assert.AreEqual(0f, point.x, 1e-4f);
            Assert.AreEqual(2f, point.y, 1e-4f, "keeps own height");
            Assert.AreEqual(-3f, point.z, 1e-4f);
            // 겹친 위치면 주어진 뒤쪽으로 간다.
            Vector3 overlap = RangedMeleeThreatRules.FallbackPoint(Vector3.zero, Vector3.zero, Vector3.left, 2f);
            Assert.AreEqual(-2f, overlap.x, 1e-4f);
            Assert.AreEqual(0f, overlap.z, 1e-4f);
        }

        [Test]
        public void FallbackAngles_StraightFirst_ThenSidesteps()
        {
            Assert.AreEqual(5, RangedMeleeThreatRules.FallbackCandidateCount);
            float[] expected = { 0f, 45f, -45f, 90f, -90f };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], RangedMeleeThreatRules.FallbackAngle(i), 1e-5f, $"candidate {i}");
            Assert.AreEqual(0f, RangedMeleeThreatRules.FallbackAngle(-1), 1e-5f, "out of range is straight");
            Assert.AreEqual(0f, RangedMeleeThreatRules.FallbackAngle(5), 1e-5f, "out of range is straight");
        }

        [Test]
        public void FallbackPoint_RotatesAwayDirection_AndKeepsHeight()
        {
            // 위협이 +z에 있으면 정반대는 -z. +90°는 -x, -90°는 +x(위에서 볼 때 시계 방향 +).
            Vector3 self = new Vector3(0f, 3f, 0f), threat = new Vector3(0f, 3f, 2f);
            Vector3 right = RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 2f, 90f);
            Assert.AreEqual(-2f, right.x, 1e-4f);
            Assert.AreEqual(0f, right.z, 1e-4f);
            Assert.AreEqual(3f, right.y, 1e-4f, "keeps own height");
            Vector3 left = RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 2f, -90f);
            Assert.AreEqual(2f, left.x, 1e-4f);
            Assert.AreEqual(0f, left.z, 1e-4f);
            Vector3 diagonal = RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 2f, 45f);
            Assert.AreEqual(-Mathf.Sqrt(2f), diagonal.x, 1e-4f);
            Assert.AreEqual(-Mathf.Sqrt(2f), diagonal.z, 1e-4f);
            Assert.AreEqual(2f, Vector3.Distance(self, diagonal), 1e-4f, "same step length");
            // 0° 오버로드는 예전 결과와 같다.
            Assert.AreEqual(RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 2f),
                RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 2f, 0f));
        }

        [Test]
        public void FallbackCandidates_AllIncreaseSeparation()
        {
            Vector3 self = new Vector3(1f, 0f, -1f), threat = new Vector3(2.5f, 0f, 0.5f);
            for (int i = 0; i < RangedMeleeThreatRules.FallbackCandidateCount; i++)
            {
                Vector3 point = RangedMeleeThreatRules.FallbackPoint(self, threat, Vector3.back, 3f,
                    RangedMeleeThreatRules.FallbackAngle(i));
                Assert.IsTrue(RangedMeleeThreatRules.IncreasesSeparation(self, threat, point), $"candidate {i}");
            }
        }

        [Test]
        public void IncreasesSeparation_StrictlyHorizontal()
        {
            Vector3 self = Vector3.zero, threat = new Vector3(0f, 0f, 2f);
            Assert.IsTrue(RangedMeleeThreatRules.IncreasesSeparation(self, threat, new Vector3(0f, 0f, -1f)));
            Assert.IsFalse(RangedMeleeThreatRules.IncreasesSeparation(self, threat, Vector3.zero), "same spot");
            Assert.IsFalse(RangedMeleeThreatRules.IncreasesSeparation(self, threat, new Vector3(0f, 0f, 1f)), "closer");
            Assert.IsFalse(RangedMeleeThreatRules.IncreasesSeparation(self, threat, new Vector3(0f, -5f, 0f)),
                "height alone does not count");
        }

        [Test]
        public void AllowsFallback_RejectsOtherLevel_AndPointsBeyondHoldLeash()
        {
            Vector3 self = new Vector3(0f, 3f, 0f);
            Vector3 post = new Vector3(0f, 3f, 0f);
            Assert.IsTrue(RangedMeleeThreatRules.AllowsFallback(self, new Vector3(0f, 3.2f, -3f), 1f, true, post, 4f));
            Assert.IsFalse(RangedMeleeThreatRules.AllowsFallback(self, new Vector3(0f, 0f, -3f), 1f, true, post, 4f),
                "dropping off the wall top is another level");
            Assert.IsFalse(RangedMeleeThreatRules.AllowsFallback(self, new Vector3(0f, 3f, -5f), 1f, true, post, 4f),
                "beyond the hold leash");
            Assert.IsTrue(RangedMeleeThreatRules.AllowsFallback(self, new Vector3(0f, 3f, -5f), 1f, false, post, 4f),
                "engage has no post leash");
        }

        [Test]
        public void IsShortPath_RejectsLongDetours()
        {
            Assert.IsTrue(RangedMeleeThreatRules.IsShortPath(3f, 3f, 2f));
            Assert.IsTrue(RangedMeleeThreatRules.IsShortPath(6f, 3f, 2f));
            Assert.IsFalse(RangedMeleeThreatRules.IsShortPath(6.1f, 3f, 2f));
        }

        [Test]
        public void HoldFacing_TurnsShortWay_AndStopsWithinTolerance()
        {
            Assert.IsTrue(HoldFacingRules.NeedsTurn(0f, 90f));
            Assert.IsFalse(HoldFacingRules.NeedsTurn(89.5f, 90f));
            Assert.IsFalse(HoldFacingRules.NeedsTurn(359.6f, 0f), "wraps around 360");
            Assert.AreEqual(20f, HoldFacingRules.Remaining(350f, 10f), 1e-4f);
            // 350도에서 10도로는 0도를 지나 짧은 쪽(+20도)으로 돈다.
            float step = HoldFacingRules.Step(350f, 10f, 5f);
            Assert.AreEqual(5f, Mathf.DeltaAngle(350f, step), 1e-4f);
            Assert.AreEqual(90f, HoldFacingRules.Step(80f, 90f, 45f), 1e-4f, "does not overshoot");
            Assert.AreEqual(80f, HoldFacingRules.Step(80f, 90f, -1f), 1e-4f, "negative step does not turn");
        }
    }
}
