namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽·성문이 아직 열리지 않았을 때 지상 공성 적(200명)이 매 판단 주기마다 성 안 수비병에게
    /// <see cref="UnityEngine.AI.NavMeshAgent.CalculatePath"/>를 반복 호출하지 않도록,
    /// 같은 길찾기 리비전(<paramref name="navigationRevision"/>)과 사다리 연결 상태(<paramref name="ladderMask"/>),
    /// 아군 변화(<paramref name="actorRevision"/>) 동안 지상 외부의 '도달 가능한 아군 없음' 결과를 공유하는 순수 캐시(PERF-siege-ai-scale).
    /// 또한 프레임당 궁수 옆자리 탐색(<see cref="SiegeAttackerBrain"/>) 횟수를 제한한다.
    /// </summary>
    public sealed class SiegeReachabilityCache
    {
        int cachedNavRevision = -1;
        int cachedLadderMask = -1;
        int cachedActorRevision = -1;
        float expiresAt = -1f;
        bool hasCachedResult;
        bool cachedGroundUnreachable;

        int archerFrame = -1;
        int archerSearchesThisFrame;

        public bool HasValidGroundUnreachable(int navigationRevision, int ladderMask, int actorRevision, float now) =>
            hasCachedResult
            && cachedGroundUnreachable
            && cachedNavRevision == navigationRevision
            && cachedLadderMask == ladderMask
            && cachedActorRevision == actorRevision
            && now < expiresAt;

        /// <summary>
        /// 지상 공격자가 성 밖 땅에서 모든 아군에 대해 걸어서 닿는 길이 없음을 확인했을 때 기록한다.
        /// 길찾기 메시가 바뀌거나 사다리가 세워지거나 유효 시간(<paramref name="ttl"/>)이 지나면 자동으로 무효가 된다.
        /// </summary>
        public void RecordGroundResult(int navigationRevision, int ladderMask, int actorRevision, float now, float ttl, bool anyReachable)
        {
            cachedNavRevision = navigationRevision;
            cachedLadderMask = ladderMask;
            cachedActorRevision = actorRevision;
            hasCachedResult = true;
            cachedGroundUnreachable = !anyReachable;
            expiresAt = ttl > 0f ? now + ttl : now;
        }

        // 돌파구(성 안·성벽 위 아군에게 걸어서 닿는 길) 없음 결과(SIEGE-15). 위의 '아무 아군에게도 닿지 않음'과 따로 둔다.
        int breachNavRevision = -1;
        int breachLadderMask = -1;
        int breachActorRevision = -1;
        float breachExpiresAt = -1f;
        bool hasBreachResult;

        /// <summary>성 밖 땅의 공격자가 돌파구 대상 아군에게 닿는 길이 없다는 결과가 아직 유효한지(SIEGE-15).</summary>
        public bool HasValidNoBreach(int navigationRevision, int ladderMask, int actorRevision, float now) =>
            hasBreachResult
            && breachNavRevision == navigationRevision
            && breachLadderMask == ladderMask
            && breachActorRevision == actorRevision
            && now < breachExpiresAt;

        /// <summary>돌파구 대상 아군에게 닿는 길이 없음을 기록한다. 길찾기 메시·사다리·아군 변화나 유효 시간으로 무효가 된다.</summary>
        public void RecordNoBreach(int navigationRevision, int ladderMask, int actorRevision, float now, float ttl)
        {
            breachNavRevision = navigationRevision;
            breachLadderMask = ladderMask;
            breachActorRevision = actorRevision;
            hasBreachResult = true;
            breachExpiresAt = ttl > 0f ? now + ttl : now;
        }

        public void Invalidate()
        {
            hasCachedResult = false;
            cachedGroundUnreachable = false;
            expiresAt = -1f;
            hasBreachResult = false;
            breachExpiresAt = -1f;
        }

        /// <summary>
        /// 이번 프레임에 궁수가 옆 사격 자리 탐색(TryFindFiringPosition)을 실행할 토큰을 하나 소비한다.
        /// <paramref name="maxPerFrame"/>이 0 이하면 제한 없이 허용한다.
        /// </summary>
        public bool TryConsumeArcherSearchToken(int frameCount, int maxPerFrame)
        {
            if (archerFrame != frameCount)
            {
                archerFrame = frameCount;
                archerSearchesThisFrame = 0;
            }
            if (maxPerFrame > 0 && archerSearchesThisFrame >= maxPerFrame)
                return false;
            archerSearchesThisFrame++;
            return true;
        }

        public int ArcherSearchesThisFrame => archerSearchesThisFrame;
    }
}
