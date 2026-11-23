namespace ProjectNeo.Combat
{
    /// <summary>
    /// 원거리 유닛의 근접 위협 후퇴(COMBAT-14) 시간 상태만 담는 순수 C# 부품(PERF-ai-alloc-and-split).
    /// 후퇴 중인지, 남은 후퇴 시간, 다음 후퇴까지의 대기 시간을 한곳에서 관리해
    /// 지시 변경·사망·비활성화 때 일부만 지워지는 일이 없게 한다. 길찾기와 NavMeshAgent 조작은 소유자가 맡는다.
    /// </summary>
    public sealed class RangedFallbackState
    {
        /// <summary>지금 물러나는 중인지.</summary>
        public bool IsActive { get; private set; }
        /// <summary>이번 후퇴에 남은 시간(초).</summary>
        public float Remaining { get; private set; }
        /// <summary>다음 후퇴를 시도할 수 있을 때까지 남은 시간(초).</summary>
        public float CooldownRemaining { get; private set; }
        public bool IsCoolingDown => CooldownRemaining > 0f;

        /// <summary>프레임마다 대기 시간을 줄인다. 후퇴 시간은 <see cref="Advance"/>가 줄인다.</summary>
        public void TickCooldown(float deltaTime)
        {
            if (CooldownRemaining > 0f) CooldownRemaining = System.Math.Max(0f, CooldownRemaining - deltaTime);
        }

        public void Begin(float maxSeconds)
        {
            IsActive = true;
            Remaining = System.Math.Max(0f, maxSeconds);
        }

        /// <summary>후퇴 시간을 줄이고, 아직 시간이 남았으면 true.</summary>
        public bool Advance(float deltaTime)
        {
            if (!IsActive) return false;
            Remaining -= deltaTime;
            return Remaining > 0f;
        }

        /// <summary>후퇴를 마치고 다음 후퇴까지 기다린다.</summary>
        public void Finish(float cooldownSeconds)
        {
            IsActive = false;
            Remaining = 0f;
            StartCooldown(cooldownSeconds);
        }

        /// <summary>물러날 곳이 없을 때 그 시간 동안 다시 찾지 않는다.</summary>
        public void StartCooldown(float cooldownSeconds) => CooldownRemaining = System.Math.Max(0f, cooldownSeconds);

        /// <summary>지시 변경·사망·비활성화로 후퇴를 그만둔다. 대기 시간은 건드리지 않는다.</summary>
        public void Cancel()
        {
            IsActive = false;
            Remaining = 0f;
        }
    }
}
