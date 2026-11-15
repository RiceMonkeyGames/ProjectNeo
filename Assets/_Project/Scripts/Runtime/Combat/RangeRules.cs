using System;

namespace ProjectNeo.Combat
{
    /// <summary>원거리 공격의 사거리 규칙(BUG-wall-archer-los). 높이 차이에 따른 사거리와 화살이 날아갈 거리를 정한다.</summary>
    public static class RangeRules
    {
        /// <summary>
        /// 높은 곳에서 아래로 쏠 때의 사거리 배율. 쏘는 쪽 발이 대상 발보다 기준 높이 이상 위에 있을 때만 (1 + bonus)이고,
        /// 그 밖에는 1이다. 위로 쏠 때의 피해 감소(DamageRules.ElevationMultiplier)와 짝을 이룬다.
        /// </summary>
        public static float HighGroundMultiplier(float drop, float minimumDrop, float bonus)
        {
            if (float.IsNaN(drop) || float.IsNaN(minimumDrop) || float.IsNaN(bonus) || float.IsInfinity(bonus)) return 1f;
            return drop >= Math.Max(0f, minimumDrop) ? 1f + Math.Max(0f, bonus) : 1f;
        }

        /// <summary>
        /// 화살이 날아갈 거리. 사거리 판정은 대상 몸통의 가장 가까운 점까지 재지만 화살은 몸통 가운데를 겨누므로,
        /// 가파르게 내려(올려) 쏘면 겨눈 지점이 사거리보다 조금 더 멀다. 판정을 통과한 대상에게는 닿도록 둘 중 큰 값을 쓴다.
        /// </summary>
        public static float FlightDistance(float range, float aimDistance)
        {
            if (float.IsNaN(range) || float.IsInfinity(range)) range = 0f;
            if (float.IsNaN(aimDistance) || float.IsInfinity(aimDistance)) aimDistance = 0f;
            return Math.Max(Math.Max(0f, range), aimDistance);
        }
    }
}
