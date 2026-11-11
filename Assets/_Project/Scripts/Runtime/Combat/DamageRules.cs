using System;

namespace ProjectNeo.Combat
{
    public static class DamageRules
    {
        public static float Calculate(float attack, float defense, float minimumDamage)
        {
            if (float.IsNaN(attack) || float.IsNaN(defense) || float.IsNaN(minimumDamage)
                || float.IsInfinity(attack) || float.IsInfinity(defense) || float.IsInfinity(minimumDamage)) return 0f;
            return Math.Max(Math.Max(0f, minimumDamage), Math.Max(0f, attack) - Math.Max(0f, defense));
        }

        /// <summary>아래에서 위로 쏜 공격의 피해 배율. 대상이 기준 높이 이상 위에 있을 때만 비율을 적용한다.</summary>
        public static float ElevationMultiplier(float rise, float minimumRise, float ratio)
        {
            if (float.IsNaN(rise) || float.IsNaN(minimumRise) || float.IsNaN(ratio)) return 1f;
            return rise >= Math.Max(0f, minimumRise) ? Math.Min(1f, Math.Max(0f, ratio)) : 1f;
        }
    }
}
