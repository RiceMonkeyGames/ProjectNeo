using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전 판단 규칙(Q-63, COMBAT-06, COMBAT-13). 순수 계산이라 EditMode로 검증한다.
    /// <list type="bullet">
    /// <item>공격: 대상 유닛 스킬은 사거리 안 적이 있으면, 범위 스킬은 범위 안 적이 기준 수 이상이면 쓴다.
    /// 조준 지점 범위 스킬은 사거리 안 적 위치 중 범위에 적이 가장 많이 드는 곳을 조준한다(<see cref="BestClusterCenter"/>).</item>
    /// <item>회복: 범위 안 아군 중 체력 비율이 기준 이하인 유닛이 있으면 쓴다.</item>
    /// <item>지원(버프·사기): 적이 교전 거리 안이면 쓴다. 사기만 올리는 스킬은 대상 분대 사기가 기준 비율 이하일 때만 쓴다.</item>
    /// </list>
    /// </summary>
    public static class SkillAutoRules
    {
        public static SkillAutoIntent Intent(SkillDefinition skill)
        {
            bool heal = false;
            if (skill != null)
                foreach (SkillEffect effect in skill.Effects)
                {
                    if (SkillEffectRules.AffectsEnemies(effect.kind)) return SkillAutoIntent.Offense;
                    if (effect.kind == SkillEffectKind.Heal) heal = true;
                }
            return heal ? SkillAutoIntent.Heal : SkillAutoIntent.Support;
        }

        /// <summary>효과가 하나 이상이고 모두 사기 상승인 스킬(COMBAT-13). 버프가 섞이면 버프 때문에라도 쓸 수 있어 아니다.</summary>
        public static bool IsMoraleOnly(SkillDefinition skill)
        {
            if (skill == null || skill.Effects.Count == 0) return false;
            foreach (SkillEffect effect in skill.Effects)
                if (effect.kind != SkillEffectKind.MoraleGain) return false;
            return true;
        }

        public static bool ShouldUseArea(int enemiesInArea, SkillAutoSettings settings) =>
            settings != null && enemiesInArea >= settings.MinimumEnemiesInArea;

        public static bool ShouldHeal(float lowestHealthRatio, SkillAutoSettings settings) =>
            settings != null && lowestHealthRatio <= settings.HealBelowHealthRatio;

        public static bool ShouldSupport(bool hasTarget, float targetDistance, SkillAutoSettings settings) =>
            settings != null && hasTarget && targetDistance <= settings.EngageDistance;

        /// <summary>사기 상승 스킬을 쓸지(COMBAT-13). 대상 분대 중 가장 낮은 사기 비율이 기준 이하이면 쓴다.</summary>
        public static bool ShouldRaiseMorale(float lowestMoraleRatio, SkillAutoSettings settings) =>
            settings != null && lowestMoraleRatio <= settings.MoraleGainBelowRatio;

        /// <summary>체력 비율(0~1). 최대 체력이 0 이하면 1(다치지 않은 것으로 본다).</summary>
        public static float HealthRatio(float current, float maximum) => Ratio(current, maximum);

        /// <summary>사기 비율(0~1). 최대 사기가 0 이하면 1(가득 찬 것으로 본다).</summary>
        public static float MoraleRatio(float current, float maximum) => Ratio(current, maximum);

        /// <summary>
        /// 조준 지점 범위 스킬의 중심 고르기(COMBAT-13). 후보(사거리 안 적 위치) 중 반경 안에 드는 적이 가장 많은 후보의 번호.
        /// 적은 몸 반경만큼 더 멀어도 든다(<see cref="SkillTargeting"/> 범위 판정과 같다).
        /// 수가 같으면 <paramref name="preferNear"/>에 가까운 후보. 후보가 없으면 -1이고 <paramref name="hits"/>는 0.
        /// </summary>
        public static int BestClusterCenter(IReadOnlyList<Vector3> candidates, IReadOnlyList<Vector3> enemies,
            IReadOnlyList<float> bodyRadii, float radius, Vector3 preferNear, out int hits)
        {
            int best = -1;
            hits = 0;
            float bestNear = float.PositiveInfinity;
            if (candidates == null || enemies == null) return best;
            for (int i = 0; i < candidates.Count; i++)
            {
                int count = CountWithin(candidates[i], enemies, bodyRadii, radius);
                float near = (candidates[i] - preferNear).sqrMagnitude;
                if (best >= 0 && (count < hits || (count == hits && near >= bestNear))) continue;
                best = i;
                hits = count;
                bestNear = near;
            }
            return best;
        }

        static int CountWithin(Vector3 center, IReadOnlyList<Vector3> enemies, IReadOnlyList<float> bodyRadii, float radius)
        {
            if (radius <= 0f) return 0;
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                float body = bodyRadii != null && i < bodyRadii.Count ? bodyRadii[i] : 0f;
                float reach = radius + body;
                if ((enemies[i] - center).sqrMagnitude <= reach * reach) count++;
            }
            return count;
        }

        static float Ratio(float current, float maximum) =>
            maximum > 0f ? (current <= 0f ? 0f : current >= maximum ? 1f : current / maximum) : 1f;
    }
}
