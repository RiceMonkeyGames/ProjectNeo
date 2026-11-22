using NUnit.Framework;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-siege-ai-scale: 분대 단위 대기 슬립 바운딩, 지상 공성 도달성 공유 캐시, 적 궁수 옆자리 탐색 프레임 예산.</summary>
    public class SiegeScaleRulesTests
    {
        [Test]
        public void SquadThreatWatch_ComputesBoundingCenterAndCoveringRadius()
        {
            var positions = new[]
            {
                new Vector3(-4f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
            };
            var radii = new[] { 10f, 16f };

            Assert.IsTrue(SquadThreatWatch.TryComputeBounds(positions, radii, out Vector3 center, out float totalRadius));
            Assert.AreEqual(Vector3.zero, center);
            // 중심(0,0,0)에서 두 번째 유닛(+4m)까지 4m + 탐색 반경 16m = 20m.
            Assert.AreEqual(20f, totalRadius, 1e-4f);

            Assert.IsTrue(SquadThreatWatch.CanSleepInHold(true, 2, false, 0));
            Assert.IsFalse(SquadThreatWatch.CanSleepInHold(false, 2, false, 0), "Hold 지시가 아니면 슬립하지 않는다.");
            Assert.IsFalse(SquadThreatWatch.CanSleepInHold(true, 2, true, 0), "누구라도 타겟이 있으면 슬립하지 않는다.");
            Assert.IsFalse(SquadThreatWatch.CanSleepInHold(true, 2, false, 1), "반경 안에 적이 1명이라도 있으면 슬립하지 않는다.");
        }

        [Test]
        public void SiegeReachabilityCache_SharesGroundUnreachable_UntilRevisionOrLadderOrTtlChanges()
        {
            var cache = new SiegeReachabilityCache();
            Assert.IsFalse(cache.HasValidGroundUnreachable(1, 0, 5, 10f));

            cache.RecordGroundResult(navigationRevision: 1, ladderMask: 0, actorRevision: 5, now: 10f, ttl: 0.5f, anyReachable: false);
            Assert.IsTrue(cache.HasValidGroundUnreachable(1, 0, 5, 10.2f));

            // 성문·성벽 파괴로 NavigationRevision이 바뀌면 즉시 무효화된다.
            Assert.IsFalse(cache.HasValidGroundUnreachable(2, 0, 5, 10.2f));
            // 사다리가 세워져 ladderMask가 바뀌면 즉시 무효화된다.
            Assert.IsFalse(cache.HasValidGroundUnreachable(1, 1, 5, 10.2f));
            // 유닛 스폰/사망으로 actorRevision이 바뀌면 즉시 무효화된다.
            Assert.IsFalse(cache.HasValidGroundUnreachable(1, 0, 6, 10.2f));
            // TTL(0.5초)이 지나면 재검사한다.
            Assert.IsFalse(cache.HasValidGroundUnreachable(1, 0, 5, 10.5f));

            // 도달 가능한 적이 있으면 unreachable 캐시가 켜지지 않는다.
            cache.RecordGroundResult(1, 0, 5, 11f, 0.5f, anyReachable: true);
            Assert.IsFalse(cache.HasValidGroundUnreachable(1, 0, 5, 11.1f));
        }

        [Test]
        public void SiegeReachabilityCache_LimitsArcherRepositionSearchesPerFrame()
        {
            var cache = new SiegeReachabilityCache();
            Assert.IsTrue(cache.TryConsumeArcherSearchToken(frameCount: 100, maxPerFrame: 2));
            Assert.IsTrue(cache.TryConsumeArcherSearchToken(frameCount: 100, maxPerFrame: 2));
            Assert.IsFalse(cache.TryConsumeArcherSearchToken(frameCount: 100, maxPerFrame: 2));
            Assert.AreEqual(2, cache.ArcherSearchesThisFrame);

            // 다음 프레임이 되면 예산이 초기화된다.
            Assert.IsTrue(cache.TryConsumeArcherSearchToken(frameCount: 101, maxPerFrame: 2));
            Assert.AreEqual(1, cache.ArcherSearchesThisFrame);
        }
    }
}
