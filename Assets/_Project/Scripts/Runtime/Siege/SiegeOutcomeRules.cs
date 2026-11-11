namespace ProjectNeo.Siege
{
    public enum SiegeOutcome { Ongoing = 0, Victory = 1, Defeat = 2 }

    /// <summary>수성전 한 판의 승패 판정. 씬을 알지 않는다.</summary>
    public static class SiegeOutcomeRules
    {
        /// <summary>
        /// 패배: 플레이어 사망, 또는 메인 성문이 부서지고 아군 전원 사망.
        /// 승리: 살아 있는 적과 나올 예정인 적이 모두 없음.
        /// 같은 순간에 둘 다 성립하면 패배로 본다. 이 우선순위는 Q-09가 정해질 때까지의 임시 처리다.
        /// </summary>
        public static SiegeOutcome Evaluate(bool playerDead, bool gateDestroyed, int alliesAlive,
            int enemiesAlive, int enemiesPending)
        {
            if (playerDead || (gateDestroyed && alliesAlive <= 0)) return SiegeOutcome.Defeat;
            if (enemiesAlive <= 0 && enemiesPending <= 0) return SiegeOutcome.Victory;
            return SiegeOutcome.Ongoing;
        }

        /// <summary>
        /// 남은 적이 공성 수단을 잃었는지(Q-30). 사다리병과 성문 파괴병이 모두 없고, 남은 적 중 걸어서
        /// 아군에게 닿는 적도 없으면 후퇴한다. 아직 첫 판단을 하지 않은 적이 있으면 기다린다.
        /// 사기·도망(B-8)이 들어오면 대체될 임시 규칙이다.
        /// </summary>
        public static bool ShouldRetreat(int enemiesAlive, int breachersAlive, int assaulting, int undecided) =>
            enemiesAlive > 0 && breachersAlive <= 0 && assaulting <= 0 && undecided <= 0;
    }
}
