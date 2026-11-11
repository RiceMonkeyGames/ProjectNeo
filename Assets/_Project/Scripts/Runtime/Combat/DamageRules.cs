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
    }
}
