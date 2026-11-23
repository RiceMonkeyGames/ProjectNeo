using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-alloc-and-split: 비행 적 지붕 검사 결과 캐시의 재사용·만료·정리 규칙.</summary>
    public class ShelterSampleCacheTests
    {
        sealed class Key { }

        [Test]
        public void TryGet_ReusesRecentSampleAtSamePosition()
        {
            var cache = new ShelterSampleCache<Key>();
            var ally = new Key();
            cache.Store(ally, Vector3.zero, 1f, true);
            Assert.IsTrue(cache.TryGet(ally, new Vector3(0.1f, 0f, 0f), 1.3f, 0.5f, 0.25f, out bool sheltered));
            Assert.IsTrue(sheltered);
        }

        [Test]
        public void TryGet_MissesWhenExpiredMovedOrDisabled()
        {
            var cache = new ShelterSampleCache<Key>();
            var ally = new Key();
            cache.Store(ally, Vector3.zero, 1f, true);
            Assert.IsFalse(cache.TryGet(ally, Vector3.zero, 1.6f, 0.5f, 0.25f, out _), "만료된 결과는 쓰지 않는다.");
            Assert.IsFalse(cache.TryGet(ally, new Vector3(1f, 0f, 0f), 1.1f, 0.5f, 0.25f, out _), "움직인 대상은 다시 잰다.");
            Assert.IsFalse(cache.TryGet(ally, Vector3.zero, 1.1f, 0f, 0.25f, out _), "캐시 시간이 0이면 늘 다시 잰다.");
            Assert.IsFalse(cache.TryGet(ally, Vector3.zero, 0.5f, 0.5f, 0.25f, out _), "시간이 거꾸로 가면 쓰지 않는다.");
            Assert.IsFalse(cache.TryGet(new Key(), Vector3.zero, 1.1f, 0.5f, 0.25f, out _), "잰 적 없는 대상은 없다.");
        }

        [Test]
        public void Prune_RemovesOnlyExpiredEntries()
        {
            var cache = new ShelterSampleCache<Key>();
            cache.Store(new Key(), Vector3.zero, 0f, false);
            var fresh = new Key();
            cache.Store(fresh, Vector3.zero, 2f, true);
            cache.Prune(2.2f, 0.5f);
            Assert.AreEqual(1, cache.Count);
            Assert.IsTrue(cache.TryGet(fresh, Vector3.zero, 2.2f, 0.5f, 0.25f, out bool sheltered));
            Assert.IsTrue(sheltered);
        }

        [Test]
        public void Prune_ClearsWhenTimeRestarts()
        {
            var cache = new ShelterSampleCache<Key>();
            cache.Store(new Key(), Vector3.zero, 10f, true);
            cache.Prune(10f, 0.5f);
            cache.Prune(0.1f, 0.5f);
            Assert.AreEqual(0, cache.Count);
        }
    }
}
