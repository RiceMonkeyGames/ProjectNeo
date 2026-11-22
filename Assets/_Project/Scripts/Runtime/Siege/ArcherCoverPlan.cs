namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성가퀴에 가려 쏠 대상이 없는 적 궁수의 자리 판단(SIEGE-10). 씬을 알지 않는다.
    /// 사거리 안인데 가려진 채 <c>delay</c>초가 지나면 옆 사격 자리를 한 번 찾는다. 찾으면 그 자리를 최소 <c>commit</c>초 지키고,
    /// 못 찾거나 자리가 쓸모없어지면 그 자리에 서서 겨눈 채 기다린다. 다음 찾기는 마지막 찾기에서 <c>cooldown</c>초 뒤에만 한다.
    /// 그래서 수비병이 숨었다 나왔다 해도 좌우로 흔들리지 않는다. 쏠 대상이 보이면 모두 처음으로 돌아간다.
    /// </summary>
    public sealed class ArcherCoverPlan
    {
        readonly float delay;
        readonly float cooldown;
        readonly float commit;
        readonly float duckTolerance;
        float blockedSince = -1f;
        float duckSince = -1f;
        float nextSearch;
        float commitUntil;

        /// <summary>옆 사격 자리를 골라 그리로 가거나 그 자리에 있다.</summary>
        public bool HasSpot { get; private set; }
        /// <summary>쏠 대상이 없어 제자리에서 겨눈 채 기다린다.</summary>
        public bool IsWaiting { get; private set; }

        /// <param name="duckTolerance">
        /// 마지막으로 쏠 수 있던 수비병이 재장전하느라 성가퀴 뒤에 웅크렸을 때(COMBAT-10) 자리를 옮기지 않고 겨눈 채 기다리는 최대 시간(SIEGE-15).
        /// 0이면 기다리지 않는다.
        /// </param>
        public ArcherCoverPlan(float delay, float cooldown, float commit, float duckTolerance = 0f)
        {
            this.delay = delay < 0f ? 0f : delay;
            this.cooldown = cooldown < 0f ? 0f : cooldown;
            this.commit = commit < 0f ? 0f : commit;
            this.duckTolerance = duckTolerance < 0f ? 0f : duckTolerance;
        }

        /// <summary>처음 상태로 돌린다(돌격·후퇴 등 다른 일을 하다 돌아왔을 때).</summary>
        public void Clear()
        {
            blockedSince = -1f; nextSearch = 0f; commitUntil = 0f; duckSince = -1f;
            HasSpot = false; IsWaiting = false;
        }

        /// <summary>지금 자리에서 쏠 대상이 있다.</summary>
        public void Shooting() => Clear();

        /// <summary>아직 사거리 밖이라 다가가는 중이다. 가려진 시간은 사거리 안에서만 잰다.</summary>
        public void Approaching()
        {
            blockedSince = -1f;
            duckSince = -1f;
            IsWaiting = false;
        }

        /// <summary>
        /// 마지막으로 쏠 수 있던 수비병이 사거리 안에서 웅크려 잠깐 가려졌다(SIEGE-15). 참는 시간 안이면 true를 돌려주고,
        /// 가려진 시간을 재지 않은 채 제자리에서 겨누고 기다린다. 참는 시간이 지나면 false이고, 그때부터 보통처럼 가려진 시간을 잰다.
        /// </summary>
        public bool TargetDucked(float now)
        {
            if (duckSince < 0f) duckSince = now;
            if (now - duckSince >= duckTolerance) return false;
            blockedSince = -1f;
            IsWaiting = true;
            return true;
        }

        /// <summary>웅크린 대상이 없다. 다음에 웅크리면 참는 시간을 처음부터 잰다.</summary>
        public void NoDuckedTarget() => duckSince = -1f;

        /// <summary>고른 자리를 지킬지. 지키는 시간 안이면 늘 지키고, 지난 뒤에는 그 자리가 아직 쓸모 있을 때만 지킨다.</summary>
        public bool KeepSpot(float now, bool stillUseful)
        {
            if (!HasSpot) return false;
            if (now < commitUntil || stillUseful) return true;
            HasSpot = false;
            return false;
        }

        /// <summary>지키는 시간 안인지. 이때는 자리가 쓸모 있는지 따로 볼 필요가 없다.</summary>
        public bool IsCommitted(float now) => HasSpot && now < commitUntil;

        /// <summary>사거리 안에서 가려졌을 때 부른다. 옆 자리를 찾을 때가 되었으면 true.</summary>
        public bool ShouldSearch(float now)
        {
            if (blockedSince < 0f) blockedSince = now;
            return now - blockedSince >= delay && now >= nextSearch;
        }

        /// <summary>찾기 결과. 찾았든 못 찾았든 다음 찾기는 cooldown 뒤다.</summary>
        public void SearchResult(float now, bool found)
        {
            nextSearch = now + cooldown;
            if (!found) return;
            HasSpot = true;
            IsWaiting = false;
            commitUntil = now + commit;
        }

        /// <summary>쏠 곳도 옮길 곳도 없어 제자리에서 기다린다.</summary>
        public void Wait() => IsWaiting = true;
    }
}
