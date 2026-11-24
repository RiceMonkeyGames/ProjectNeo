using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>비행 적 대상 잠금·거리 여유 규칙(SIEGE-32).</summary>
    public class FlyerTargetLockTests
    {
        WallTopFirstFlyerTargetStrategy strategy;

        [SetUp]
        public void SetUp() => strategy = WallTopFirstFlyerTargetStrategy.Create();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(strategy);

        static FlyerTargetCandidate WallTop(int id, float distance) => new FlyerTargetCandidate(id, distance * distance, true);
        static FlyerTargetCandidate Ground(int id, float distance) => new FlyerTargetCandidate(id, distance * distance, false);

        [Test]
        public void NoCurrentTarget_UsesStrategyChoice()
        {
            var candidates = new List<FlyerTargetCandidate> { WallTop(0, 6f), WallTop(1, 5f) };
            Assert.AreEqual(1, FlyerTargetLockRules.Select(candidates, strategy, false, -1, 0f, 2f));
        }

        [Test]
        public void WhileLocked_KeepsCurrentEvenWhenAnotherIsNearer()
        {
            var candidates = new List<FlyerTargetCandidate> { WallTop(0, 9f), WallTop(1, 3f) };
            Assert.AreEqual(0, FlyerTargetLockRules.Select(candidates, strategy, false, 0, 0.5f, 2f));
        }

        [Test]
        public void AfterLock_SimilarDistanceDoesNotSwitch_ClearlyNearerDoes()
        {
            // 지금 대상 5m, 다른 대상 4m: 차이 1m < 여유 2m이므로 그대로.
            var close = new List<FlyerTargetCandidate> { WallTop(0, 5f), WallTop(1, 4f) };
            Assert.AreEqual(0, FlyerTargetLockRules.Select(close, strategy, false, 0, 0f, 2f));
            // 다른 대상 2.5m: 차이 2.5m > 2m이므로 바꾼다.
            var far = new List<FlyerTargetCandidate> { WallTop(0, 5f), WallTop(1, 2.5f) };
            Assert.AreEqual(1, FlyerTargetLockRules.Select(far, strategy, false, 0, 0f, 2f));
            // 여유를 잠깐 적용한 후보는 원래 값으로 돌아온다.
            Assert.AreEqual(25f, far[0].DistanceSquared, 1e-4f);
        }

        [Test]
        public void OscillatingDistances_DoNotFlipTargetEveryDecision()
        {
            // 두 수비병이 번갈아 조금씩 더 가까워져도 대상은 한 번 고른 뒤 그대로다.
            int current = -1;
            int switches = 0;
            for (int step = 0; step < 10; step++)
            {
                float wobble = step % 2 == 0 ? 0.4f : -0.4f;
                var candidates = new List<FlyerTargetCandidate> { WallTop(0, 6f + wobble), WallTop(1, 6f - wobble) };
                int chosen = FlyerTargetLockRules.Select(candidates, strategy, false, current, 0f, 2f);
                if (current >= 0 && chosen != current) switches++;
                current = chosen;
            }
            Assert.AreEqual(0, switches);
        }

        [Test]
        public void HigherRankTarget_BreaksLock_EvenIfFarther()
        {
            // 지금 대상은 마당(2m), 성벽 위 수비병은 20m. 잠금 중이어도 전략 순위대로 성벽 위를 고른다.
            var candidates = new List<FlyerTargetCandidate> { Ground(0, 2f), WallTop(1, 20f) };
            Assert.AreEqual(1, FlyerTargetLockRules.Select(candidates, strategy, false, 0, 0f, 2f));
            Assert.AreEqual(1, FlyerTargetLockRules.Select(candidates, strategy, false, 0, 1f, 2f));
            Assert.AreEqual(4f, candidates[0].DistanceSquared, 1e-4f);
        }

        [Test]
        public void LowerRankTarget_NeverReplacesCurrent()
        {
            // 지금 대상은 성벽 위(9m), 마당 아군이 아주 가까워도 순위가 낮아 바꾸지 않는다.
            var candidates = new List<FlyerTargetCandidate> { WallTop(0, 9f), Ground(1, 0.5f) };
            Assert.AreEqual(0, FlyerTargetLockRules.Select(candidates, strategy, false, 0, 0f, 2f));
        }

        [Test]
        public void NewPriorityTarget_BreaksLock()
        {
            var candidates = new List<FlyerTargetCandidate>
            {
                WallTop(0, 3f),
                new FlyerTargetCandidate(1, 100f, false, true),
            };
            Assert.AreEqual(1, FlyerTargetLockRules.Select(candidates, strategy, true, 0, 5f, 2f));
        }

        [Test]
        public void ZeroMargin_SwitchesToStrategyChoiceAfterLock()
        {
            var candidates = new List<FlyerTargetCandidate> { WallTop(0, 5f), WallTop(1, 4.9f) };
            Assert.AreEqual(1, FlyerTargetLockRules.Select(candidates, strategy, false, 0, 0f, 0f));
        }

        [Test]
        public void SettingsDefaults_AreClamped()
        {
            var settings = FlyerSettings.Create(strategy, null, null, targetLockSeconds: -1f,
                retargetDistanceMargin: -3f, sightlineRecheckInterval: 0f);
            try
            {
                Assert.AreEqual(0f, settings.TargetLockSeconds);
                Assert.AreEqual(0f, settings.RetargetDistanceMargin);
                Assert.AreEqual(0.05f, settings.SightlineRecheckInterval, 1e-5f);
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
