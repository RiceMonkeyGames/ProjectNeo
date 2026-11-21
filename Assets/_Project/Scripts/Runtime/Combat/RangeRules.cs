using System;

namespace ProjectNeo.Combat
{
    /// <summary>원거리 공격의 사거리 규칙(BUG-wall-archer-los). 높이 차이에 따른 사거리를 정한다. 화살은 COMBAT-09부터 포물선으로 땅에 닿을 때까지 난다.</summary>
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
    }
}
