using System.Collections.Generic;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 같은 소리를 너무 자주 내지 못하게 막는다. 소리마다 다음에 낼 수 있는 시각을 기억한다.
    /// 시각은 부르는 쪽이 넘긴다(보통 Time.unscaledTime).
    /// </summary>
    public sealed class CueCooldownGate
    {
        readonly Dictionary<int, float> readyAt = new Dictionary<int, float>();

        /// <summary>지금 낼 수 있는지만 본다. 기록은 바꾸지 않는다.</summary>
        public bool IsReady(int cue, float now) => !readyAt.TryGetValue(cue, out float time) || now >= time;

        /// <summary>지금 냈다고 기록한다. cooldown이 0 이하면 아무것도 기록하지 않는다.</summary>
        public void Mark(int cue, float now, float cooldown)
        {
            if (cooldown > 0f) readyAt[cue] = now + cooldown;
        }

        /// <summary>낼 수 있으면 기록하고 true.</summary>
        public bool TryPass(int cue, float now, float cooldown)
        {
            if (!IsReady(cue, now)) return false;
            Mark(cue, now, cooldown);
            return true;
        }

        public void Clear() => readyAt.Clear();
    }
}
