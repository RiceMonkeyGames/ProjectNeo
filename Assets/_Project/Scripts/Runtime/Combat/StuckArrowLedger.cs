using System.Collections.Generic;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 박힌 화살을 박힌 순서대로 기억하고, 상한을 넘으면 가장 오래된 것부터 내보낸다(ART-05).
    /// </summary>
    public sealed class StuckArrowLedger<T> where T : class
    {
        readonly List<T> order = new List<T>();

        public int Count => order.Count;

        /// <summary>새로 박힌 항목을 맨 뒤에 넣고, 상한을 넘은 만큼 오래된 항목을 evicted에 담아 뺀다.</summary>
        public void Add(T item, int cap, List<T> evicted)
        {
            if (item == null) return;
            order.Remove(item);
            order.Add(item);
            int limit = cap < 1 ? 1 : cap;
            while (order.Count > limit)
            {
                evicted?.Add(order[0]);
                order.RemoveAt(0);
            }
        }

        public bool Remove(T item) => item != null && order.Remove(item);

        public bool Contains(T item) => item != null && order.Contains(item);

        public void Clear() => order.Clear();
    }
}
