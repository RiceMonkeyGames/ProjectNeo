namespace ProjectNeo.Siege
{
    /// <summary>
    /// 공성 적이 공성 임무를 멈추고 돌격(Assault)으로 바꿀지 정하는 순수 규칙(SIEGE-15). 씬을 알지 않는다.
    /// 성 밖 땅에 선 적은 돌파구(성 안이나 성벽 위 아군에게 걸어서 닿음)가 열리거나, 걸어서 닿는 아군이
    /// 가까운 반경 안에 들어올 때만 돌격한다. 성 밖으로 멀리 나온 아군 하나에 웨이브 전체가 끌려가지 않게 한다.
    /// </summary>
    public static class SiegeAssaultRules
    {
        /// <summary>
        /// 그 아군이 돌파구 대상인지. 성 범위 안(안뜰·성벽 칸)이거나 땅에서 <paramref name="wallTopMinimumHeight"/> 이상 높으면(성벽·보루 위) 그렇다.
        /// </summary>
        public static bool IsBreachTarget(bool withinCastle, float heightAboveGround, float wallTopMinimumHeight) =>
            withinCastle || heightAboveGround >= wallTopMinimumHeight;

        /// <summary>
        /// 근접 반경. 이미 근접 반경 때문에 돌격 중이면 더 넓은 풀림 반경을 써서, 반경 끝에서 들어갔다 나갔다 하지 않게 한다.
        /// </summary>
        public static float LeashRadius(bool leashAssaulting, float leash, float release)
        {
            if (leash < 0f) leash = 0f;
            return leashAssaulting && release > leash ? release : leash;
        }

        /// <summary>
        /// 돌격할지.
        /// 1) 이미 성 안이나 성벽 위에 있으면 걸어서 닿는 아군이 하나라도 있으면 돌격한다(기존 동작).
        /// 2) 근접 반경 안에 걸어서 닿는 아군이 있으면 돌격한다(궁수 포함, 자기 방어).
        /// 3) 돌파구가 열렸으면 공성병은 돌격한다. 궁수는 이미 진입하기로 했거나, 쏠 대상이 없는 시간이
        ///    <paramref name="archerAdvanceDelay"/>초 이상일 때만 들어간다. 그 전에는 바깥에서 계속 쏜다.
        /// </summary>
        public static bool ShouldAssault(SiegeAttackerRole role, bool attackerInside, bool anyReachable,
            bool breachReachable, bool leashReachable, bool archerCommitted, float archerIdleSeconds, float archerAdvanceDelay)
        {
            if (attackerInside) return anyReachable;
            if (leashReachable) return true;
            if (!breachReachable) return false;
            if (role != SiegeAttackerRole.Archer) return true;
            return archerCommitted || archerIdleSeconds >= archerAdvanceDelay;
        }
    }
}
