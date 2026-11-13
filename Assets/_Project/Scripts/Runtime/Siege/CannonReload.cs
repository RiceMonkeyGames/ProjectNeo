using System;

namespace ProjectNeo.Siege
{
    /// <summary>재장전 타이머. 자동 사격과 직접 조작이 같은 타이머를 쓴다. 처음에는 장전된 상태다.</summary>
    public sealed class CannonReload
    {
        readonly float duration;
        public float Remaining { get; private set; }
        public bool IsReady => Remaining <= 0f;
        /// <summary>0(방금 쏨)에서 1(장전 완료)까지.</summary>
        public float Progress => duration <= 0f ? 1f : 1f - Remaining / duration;

        public CannonReload(float seconds) => duration = Math.Max(0f, seconds);

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) Remaining = Math.Max(0f, Remaining - deltaTime);
        }

        public bool TryFire()
        {
            if (!IsReady) return false;
            Remaining = duration;
            return true;
        }

        public void Reset() => Remaining = 0f;
    }
}
