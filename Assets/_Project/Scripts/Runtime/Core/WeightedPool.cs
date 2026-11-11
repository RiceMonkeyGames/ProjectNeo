using System;
using System.Collections.Generic;

namespace ProjectNeo.Core
{
    /// <summary>랜덤 풀의 한 줄. 가중치가 클수록 자주 뽑힌다.</summary>
    [Serializable]
    public class WeightedEntry<T>
    {
        public T item;
        public float weight = 1f;

        public WeightedEntry() { }

        public WeightedEntry(T item, float weight = 1f)
        {
            this.item = item;
            this.weight = weight;
        }
    }

    public static class WeightedPool
    {
        /// <summary>
        /// 풀에서 서로 다른 항목을 count개까지 뽑는다. 주점 영웅, 상점 매물이 이 함수를 쓴다.
        /// isAvailable이 false인 항목(예: 해금 안 됨)은 빼고, guaranteed에 든 항목은 풀에 있으면 먼저 넣는다.
        /// </summary>
        public static List<T> Roll<T>(
            IReadOnlyList<WeightedEntry<T>> entries,
            int count,
            IRandomSource random,
            Func<T, bool> isAvailable = null,
            IEnumerable<T> guaranteed = null) where T : class
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var result = new List<T>();
            if (entries == null || count <= 0) return result;

            var candidates = new List<WeightedEntry<T>>();
            foreach (WeightedEntry<T> entry in entries)
            {
                if (entry == null || entry.item == null || entry.weight <= 0f) continue;
                if (isAvailable != null && !isAvailable(entry.item)) continue;
                if (candidates.Exists(c => ReferenceEquals(c.item, entry.item))) continue;
                candidates.Add(entry);
            }

            if (guaranteed != null)
            {
                foreach (T item in guaranteed)
                {
                    if (result.Count >= count) break;
                    int index = candidates.FindIndex(c => ReferenceEquals(c.item, item));
                    if (index < 0) continue;
                    result.Add(item);
                    candidates.RemoveAt(index);
                }
            }

            while (result.Count < count && candidates.Count > 0)
            {
                float total = 0f;
                foreach (WeightedEntry<T> candidate in candidates) total += candidate.weight;

                float roll = random.NextFloat() * total;
                int picked = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= candidates[i].weight;
                    if (roll < 0f) { picked = i; break; }
                }

                result.Add(candidates[picked].item);
                candidates.RemoveAt(picked);
            }

            return result;
        }
    }
}
