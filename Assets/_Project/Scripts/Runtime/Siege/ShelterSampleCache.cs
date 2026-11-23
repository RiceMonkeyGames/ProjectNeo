using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대상마다 마지막 지붕 검사 결과를 잠깐 기억하는 순수 캐시(PERF-ai-alloc-and-split).
    /// 여러 비행 적이 같은 판단 주기에 같은 아군의 머리 위를 다시 쏘지 않게 한다. 결과는 정해진 시간이 지났거나
    /// 대상이 허용 거리보다 많이 움직였으면 쓰지 않는다. 오래된 항목(사라진 대상 포함)은 <see cref="Prune"/>이 비운다.
    /// </summary>
    public sealed class ShelterSampleCache<TKey> where TKey : class
    {
        struct Sample
        {
            public float Time;
            public Vector3 Position;
            public bool Sheltered;
        }

        readonly Dictionary<TKey, Sample> samples = new Dictionary<TKey, Sample>();
        readonly List<TKey> stale = new List<TKey>();
        float lastPrune = float.NegativeInfinity;

        public int Count => samples.Count;

        /// <summary>
        /// <paramref name="now"/> 기준 <paramref name="maxAge"/>초 안에 잰 결과가 있고, 그때부터 대상이
        /// <paramref name="moveTolerance"/>보다 덜 움직였으면 그 결과를 돌려준다. maxAge가 0이면 늘 다시 잰다.
        /// </summary>
        public bool TryGet(TKey key, Vector3 position, float now, float maxAge, float moveTolerance, out bool sheltered)
        {
            sheltered = false;
            if (key == null || maxAge <= 0f || !samples.TryGetValue(key, out Sample sample)) return false;
            if (now - sample.Time > maxAge || now < sample.Time) return false;
            if ((position - sample.Position).sqrMagnitude > moveTolerance * moveTolerance) return false;
            sheltered = sample.Sheltered;
            return true;
        }

        public void Store(TKey key, Vector3 position, float now, bool sheltered)
        {
            if (key == null) return;
            samples[key] = new Sample { Time = now, Position = position, Sheltered = sheltered };
        }

        /// <summary>
        /// <paramref name="maxAge"/>보다 오래된 항목을 지운다. 매번 훑지 않도록 maxAge마다 한 번만 실제로 비운다.
        /// 시간이 거꾸로 가면(새 플레이 세션) 모두 비운다.
        /// </summary>
        public void Prune(float now, float maxAge)
        {
            if (now < lastPrune) { Clear(); lastPrune = now; return; }
            if (now - lastPrune < Mathf.Max(maxAge, 0.05f)) return;
            lastPrune = now;
            stale.Clear();
            foreach (var pair in samples)
                if (now - pair.Value.Time > maxAge || now < pair.Value.Time) stale.Add(pair.Key);
            foreach (TKey key in stale) samples.Remove(key);
            stale.Clear();
        }

        public void Clear()
        {
            samples.Clear();
            stale.Clear();
            lastPrune = float.NegativeInfinity;
        }
    }
}
