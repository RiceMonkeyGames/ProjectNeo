using NUnit.Framework;
using ProjectNeo.Combat;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-lod: 거리·시야·교전·탑다운 카메라 모드에 따른 3단계 AI LOD 등급과 배율 규칙.</summary>
    public class AILodRulesTests
    {
        [Test]
        public void Evaluate_ClassifiesByDistanceAndViewCone()
        {
            // 25m 안 + 시야 안 = LOD 0
            Assert.AreEqual(AILodTier.Lod0, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 10f * 10f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: true, highPriorityCombat: false, hasTarget: false, topDownMode: false));

            // 25m 안 + 시야 밖(카메라 뒤) = LOD 1
            Assert.AreEqual(AILodTier.Lod1, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 10f * 10f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: false, highPriorityCombat: false, hasTarget: false, topDownMode: false));

            // 25m~60m + 시야 안 = LOD 1
            Assert.AreEqual(AILodTier.Lod1, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 40f * 40f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: true, highPriorityCombat: false, hasTarget: false, topDownMode: false));

            // 25m~60m + 시야 밖 + 비전투(Target == null) = LOD 2
            Assert.AreEqual(AILodTier.Lod2, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 40f * 40f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: false, highPriorityCombat: false, hasTarget: false, topDownMode: false));

            // 25m~60m + 시야 밖 + 교전 중(Target != null) = LOD 1 유지
            Assert.AreEqual(AILodTier.Lod1, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 40f * 40f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: false, highPriorityCombat: false, hasTarget: true, topDownMode: false));

            // 60m 밖 = 시야·교전 여부와 무관하게 LOD 2
            Assert.AreEqual(AILodTier.Lod2, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 75f * 75f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: true, highPriorityCombat: false, hasTarget: true, topDownMode: false));
        }

        [Test]
        public void Evaluate_PromotesHighPriorityCombat_AndClampsTopDownMode()
        {
            // 영웅·플레이어 교전 중이면 100m 밖이어도 LOD 0
            Assert.AreEqual(AILodTier.Lod0, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 100f * 100f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: false, highPriorityCombat: true, hasTarget: true, topDownMode: true));

            // LOD 비활성화 시 언제나 LOD 0
            Assert.AreEqual(AILodTier.Lod0, AILodRules.Evaluate(
                enabled: false, sqrDistanceToView: 100f * 100f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: false, highPriorityCombat: false, hasTarget: false, topDownMode: false));

            // 탑다운 카메라 모드에서는 일반 유닛의 근경 LOD 0을 LOD 1로 제한해 전장 전체 부하를 고르게 유지한다.
            Assert.AreEqual(AILodTier.Lod1, AILodRules.Evaluate(
                enabled: true, sqrDistanceToView: 5f * 5f, lod0Distance: 25f, lod1Distance: 60f,
                inViewCone: true, highPriorityCombat: false, hasTarget: true, topDownMode: true));
        }

        [Test]
        public void ScalingRules_ScaleIntervalCandidatesAndMeleeSlotsByTier()
        {
            Assert.AreEqual(1f, AILodRules.IntervalMultiplier(AILodTier.Lod0));
            Assert.AreEqual(2f, AILodRules.IntervalMultiplier(AILodTier.Lod1));
            Assert.AreEqual(4f, AILodRules.IntervalMultiplier(AILodTier.Lod2));

            Assert.AreEqual(4, AILodRules.PathCandidateLimit(AILodTier.Lod0, 4));
            Assert.AreEqual(2, AILodRules.PathCandidateLimit(AILodTier.Lod1, 4));
            Assert.AreEqual(1, AILodRules.PathCandidateLimit(AILodTier.Lod2, 4));

            Assert.AreEqual(3, AILodRules.MeleeSlotTries(AILodTier.Lod0, 3));
            Assert.AreEqual(1, AILodRules.MeleeSlotTries(AILodTier.Lod1, 3));
            Assert.AreEqual(0, AILodRules.MeleeSlotTries(AILodTier.Lod2, 3));

            Assert.IsTrue(AILodRules.UsesFullMeleeSlotPath(AILodTier.Lod0));
            Assert.IsFalse(AILodRules.UsesFullMeleeSlotPath(AILodTier.Lod1));
            Assert.IsFalse(AILodRules.UsesFullMeleeSlotPath(AILodTier.Lod2));

            Assert.AreEqual(1, AILodRules.AnimatorDetailStride(AILodTier.Lod0, true));
            Assert.AreEqual(2, AILodRules.AnimatorDetailStride(AILodTier.Lod1, true));
            Assert.AreEqual(4, AILodRules.AnimatorDetailStride(AILodTier.Lod2, true));
            Assert.AreEqual(8, AILodRules.AnimatorDetailStride(AILodTier.Lod2, false));
        }
    }
}
