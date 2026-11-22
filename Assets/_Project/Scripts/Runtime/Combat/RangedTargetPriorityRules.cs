namespace ProjectNeo.Combat
{
    /// <summary>
    /// 원거리 유닛의 위협 우선순위(COMBAT-14, 사용자 결정 2026-11-22: 아군·적 원거리 모두 적용).
    /// 대상마다 거리 배율(1 이하)을 두어 그 대상을 배율만큼 가깝게 본다. 점수는 제곱 거리라 배율도 제곱해서 곱한다.
    /// 결투형 보스의 선호 배율(<see cref="UnitCombatAI.TargetDistanceScale"/>)과는 곱해서 함께 쓴다.
    /// </summary>
    public static class RangedTargetPriorityRules
    {
        /// <summary>배율의 하한. 0이면 거리가 사라지므로 막는다.</summary>
        public const float MinimumFactor = 0.05f;

        /// <summary>사다리 위에 있으면 사다리 위협, 아니면 판단이 알려 준 위협, 그것도 없으면 원거리 공격 여부로 정한다.</summary>
        public static TargetThreat Classify(bool onLadder, TargetThreat reported, bool isRanged)
        {
            if (onLadder) return TargetThreat.LadderClimber;
            if (reported != TargetThreat.None) return reported;
            return isRanged ? TargetThreat.Ranged : TargetThreat.None;
        }

        /// <summary>위협 종류에 맞는 거리 배율. 일반 대상은 1이다. 배율은 [<see cref="MinimumFactor"/>, 1]로 자른다.</summary>
        public static float DistanceFactor(TargetThreat threat, float ladderClimber, float siegeBreaker, float ranged)
        {
            float factor = threat switch
            {
                TargetThreat.LadderClimber => ladderClimber,
                TargetThreat.SiegeBreaker => siegeBreaker,
                TargetThreat.Ranged => ranged,
                _ => 1f,
            };
            return factor < MinimumFactor ? MinimumFactor : factor > 1f ? 1f : factor;
        }

        /// <summary>대상 비교 점수: 제곱 거리 × 보스 선호 배율 × 위협 배율². 작을수록 먼저 고른다.</summary>
        public static float Score(float sqrDistance, float preferenceScale, float distanceFactor) =>
            sqrDistance * preferenceScale * distanceFactor * distanceFactor;

        /// <summary>
        /// 이미 쏘고 있는 대상을 더 위협적인 대상으로 바꿀지. 배율이 확실히 더 작을 때만 바꿔서
        /// 같은 종류끼리 거리 차이로 왔다 갔다 하지 않게 한다.
        /// </summary>
        public static bool IsHigherThreat(float currentFactor, float candidateFactor) => candidateFactor < currentFactor - 0.001f;
    }
}
