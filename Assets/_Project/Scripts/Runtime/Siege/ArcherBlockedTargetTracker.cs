using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 적 궁수가 완전히 가려진 수비병에게 붙잡히지 않게 하는 규칙(SIEGE-34). 씬을 알지 않는다.
    /// 같은 대상을 향한 옆 자리 찾기가 연달아 <c>limit</c>번 실패하면 그 대상을 <c>avoidSeconds</c>초 동안 다가갈 대상에서 뺀다.
    /// 다른 대상을 향하거나 찾기에 성공하거나 쏠 수 있게 되면 실패 횟수는 처음부터 센다. 빼 둔 대상은 최대 <c>capacity</c>명까지 기억한다.
    /// </summary>
    public sealed class ArcherBlockedTargetTracker<T> where T : class
    {
        struct Entry
        {
            public T Target;
            public float Until;
        }

        readonly int limit;
        readonly float avoidSeconds;
        readonly int capacity;
        readonly List<Entry> avoided = new List<Entry>();
        T counting;
        int failures;

        public ArcherBlockedTargetTracker(int limit, float avoidSeconds, int capacity = 4)
        {
            this.limit = limit < 1 ? 1 : limit;
            this.avoidSeconds = avoidSeconds < 0f ? 0f : avoidSeconds;
            this.capacity = capacity < 1 ? 1 : capacity;
        }

        /// <summary>지금 세고 있는 대상의 연속 실패 횟수(검증용).</summary>
        public int Failures => failures;

        /// <summary>빼 둔 대상인지. 시간이 지난 기록은 지운다.</summary>
        public bool IsAvoided(T target, float now)
        {
            if (target == null) return false;
            for (int i = avoided.Count - 1; i >= 0; i--)
            {
                if (avoided[i].Until <= now) { avoided.RemoveAt(i); continue; }
                if (ReferenceEquals(avoided[i].Target, target)) return true;
            }
            return false;
        }

        /// <summary>
        /// 대상을 향한 옆 자리 찾기 결과. 연속 실패가 기준에 닿아 대상을 새로 뺐으면 true.
        /// </summary>
        public bool RecordSearch(T target, bool found, float now)
        {
            if (target == null) return false;
            if (found) { Reset(); return false; }
            if (!ReferenceEquals(counting, target)) { counting = target; failures = 0; }
            failures++;
            if (failures < limit || avoidSeconds <= 0f) return false;
            failures = 0;
            counting = null;
            if (avoided.Count >= capacity) avoided.RemoveAt(0);
            avoided.Add(new Entry { Target = target, Until = now + avoidSeconds });
            return true;
        }

        /// <summary>쏠 대상이 보였거나 찾기에 성공했다. 연속 실패 횟수만 지운다(빼 둔 대상은 남긴다).</summary>
        public void Reset()
        {
            counting = null;
            failures = 0;
        }

        /// <summary>모두 지운다(전투 재시작 등).</summary>
        public void Clear()
        {
            Reset();
            avoided.Clear();
        }
    }
}
