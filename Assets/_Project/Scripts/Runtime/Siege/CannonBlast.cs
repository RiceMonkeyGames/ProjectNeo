using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>대포 폭발 피해와 피해 대상 규칙.</summary>
    public static class CannonBlast
    {
        /// <summary>중심 1에서 가장자리 edgeRatio까지 직선으로 줄어드는 배율. 반경 밖은 0.</summary>
        public static float Falloff(float distance, float radius, float edgeRatio)
        {
            if (float.IsNaN(distance) || !(radius > 0f) || distance > radius) return 0f;
            float t = Mathf.Clamp01(Mathf.Max(0f, distance) / radius);
            return Mathf.Lerp(1f, Mathf.Clamp01(edgeRatio), t);
        }

        /// <summary>거리 감쇠 뒤 방어력을 뺀 피해. 반경 밖이면 최소 피해도 없이 0.</summary>
        public static float Damage(float centerDamage, float distance, float radius, float edgeRatio,
            float defense, float minimumDamage)
        {
            float falloff = Falloff(distance, radius, edgeRatio);
            return falloff <= 0f ? 0f : DamageRules.Calculate(centerDamage * falloff, defense, minimumDamage);
        }

        /// <summary>쏜 쪽과 같은 편 유닛은 아군 피해가 켜져 있을 때만 맞는다.</summary>
        public static bool ShouldDamage(CombatTeam shooter, CombatTeam target, bool friendlyFire) =>
            friendlyFire || shooter != target;

        /// <summary>우리 성벽·무기는 아군 피해가 켜져 있을 때만 맞는다.</summary>
        public static bool ShouldDamageOwnStructures(bool friendlyFire) => friendlyFire;
    }
}
