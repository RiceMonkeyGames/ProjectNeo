using System.Collections.Generic;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 시간이 지나면 풀리는 버프·디버프 타이머(COMBAT-06). 같은 열쇠로 다시 걸면 겹치지 않고 남은 시간만 새로 고친다.
    /// </summary>
    public sealed class SkillStatusTimers
    {
        readonly Dictionary<object, float> remaining = new Dictionary<object, float>();
        readonly List<object> scratch = new List<object>();

        public int Count => remaining.Count;

        public bool Has(object key) => key != null && remaining.ContainsKey(key);

        public float Remaining(object key) => key != null && remaining.TryGetValue(key, out float time) ? time : 0f;

        /// <summary>타이머를 건다. 새로 걸렸으면 true, 이미 있어서 시간만 새로 고쳤으면 false.</summary>
        public bool Apply(object key, float duration)
        {
            if (key == null) return false;
            bool added = !remaining.ContainsKey(key);
            float time = duration > 0f ? duration : 0f;
            if (!added && remaining[key] > time) time = remaining[key];
            remaining[key] = time;
            return added;
        }

        /// <summary>시간을 흘리고 끝난 열쇠를 <paramref name="expired"/>에 더한다.</summary>
        public void Tick(float deltaTime, List<object> expired)
        {
            scratch.Clear();
            scratch.AddRange(remaining.Keys);
            foreach (object key in scratch)
            {
                float time = remaining[key] - (deltaTime > 0f ? deltaTime : 0f);
                if (time > 0f)
                {
                    remaining[key] = time;
                    continue;
                }
                remaining.Remove(key);
                expired?.Add(key);
            }
        }

        /// <summary>모두 지우고 지운 열쇠를 <paramref name="expired"/>에 더한다.</summary>
        public void Clear(List<object> expired)
        {
            expired?.AddRange(remaining.Keys);
            remaining.Clear();
        }
    }
}
