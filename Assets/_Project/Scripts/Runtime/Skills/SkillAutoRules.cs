using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전 판단 규칙(Q-63, COMBAT-06, COMBAT-13, COMBAT-18). 순수 계산이라 EditMode로 검증한다.
    /// 준비된 칸마다 긴급도(<see cref="SkillAutoUrgency"/>)를 매겨 가장 높은 칸을 쓴다(<see cref="MostUrgent"/>).
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
            int best = BestWeightedCenter(candidates, enemies, bodyRadii, null, radius, preferNear, out float total);
            hits = Mathf.RoundToInt(total);
            return best;
        }

        /// <summary>
        /// 범위 회복의 중심 고르기(COMBAT-18). 후보(회복 기준 이하인 아군 위치) 중 반경 안 아군의 잃은 체력 비율 합이 가장 큰 후보의 번호.
        /// 한 명만 가장 많이 다친 곳보다 여럿이 다친 곳을 덮는다. 같으면 <paramref name="preferNear"/>(가장 많이 다친 아군)에 가까운 후보.
        /// 후보가 없으면 -1이고 <paramref name="missing"/>은 0.
        /// </summary>
        public static int BestHealCenter(IReadOnlyList<Vector3> candidates, IReadOnlyList<Vector3> allies,
            IReadOnlyList<float> bodyRadii, IReadOnlyList<float> healthRatios, float radius, Vector3 preferNear, out float missing)
        {
            missingScratch.Clear();
            if (healthRatios != null)
                foreach (float ratio in healthRatios) missingScratch.Add(1f - Mathf.Clamp01(ratio));
            return BestWeightedCenter(candidates, allies, bodyRadii, missingScratch, radius, preferNear, out missing);
        }

        static readonly List<float> missingScratch = new List<float>();

        static int BestWeightedCenter(IReadOnlyList<Vector3> candidates, IReadOnlyList<Vector3> points,
            IReadOnlyList<float> bodyRadii, IReadOnlyList<float> weights, float radius, Vector3 preferNear, out float total)
        {
            int best = -1;
            total = 0f;
            float bestNear = float.PositiveInfinity;
            if (candidates == null || points == null) return best;
            for (int i = 0; i < candidates.Count; i++)
            {
                float sum = SumWithin(candidates[i], points, bodyRadii, weights, radius);
                float near = (candidates[i] - preferNear).sqrMagnitude;
                if (best >= 0 && (sum < total - 1e-4f || (sum <= total + 1e-4f && near >= bestNear))) continue;
                best = i;
                total = sum;
                bestNear = near;
            }
            return best;
        }

        static float SumWithin(Vector3 center, IReadOnlyList<Vector3> points, IReadOnlyList<float> bodyRadii,
            IReadOnlyList<float> weights, float radius)
        {
            if (radius <= 0f) return 0f;
            float sum = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float body = bodyRadii != null && i < bodyRadii.Count ? bodyRadii[i] : 0f;
                float reach = radius + body;
                if ((points[i] - center).sqrMagnitude > reach * reach) continue;
                sum += weights == null ? 1f : i < weights.Count ? weights[i] : 0f;
            }
            return sum;
        }

        /// <summary>경직 효과가 있는 스킬(COMBAT-18).</summary>
        public static bool HasStagger(SkillDefinition skill) => HasEffect(skill, SkillEffectKind.Stagger);

        /// <summary>사기 상승 효과가 있는 스킬(COMBAT-18).</summary>
        public static bool HasMoraleGain(SkillDefinition skill) => HasEffect(skill, SkillEffectKind.MoraleGain);

        static bool HasEffect(SkillDefinition skill, SkillEffectKind kind)
        {
            if (skill == null) return false;
            foreach (SkillEffect effect in skill.Effects)
                if (effect.kind == kind) return true;
            return false;
        }

        /// <summary>회복 긴급도(COMBAT-18). 기준 이하가 아니면 없음, 위급 기준 이하면 위급 회복.</summary>
        public static SkillAutoUrgency HealUrgency(float lowestHealthRatio, SkillAutoSettings settings)
        {
            if (!ShouldHeal(lowestHealthRatio, settings)) return SkillAutoUrgency.None;
            return lowestHealthRatio <= settings.CriticalHealBelowRatio ? SkillAutoUrgency.CriticalHeal : SkillAutoUrgency.Heal;
        }

        /// <summary>
        /// 범위 공격 긴급도(COMBAT-18). 경직으로 고가치 적(보스·공성 파괴·사다리)을 잡으면 적 수 기준과 상관없이 고가치 공격.
        /// 아니면 기준 수 미만은 없음, 고가치 수 이상은 고가치 공격, 그 사이는 일반.
        /// </summary>
        public static SkillAutoUrgency AreaOffenseUrgency(int enemiesInArea, bool staggersHighValue, SkillAutoSettings settings)
        {
            if (settings == null) return SkillAutoUrgency.None;
            if (staggersHighValue) return SkillAutoUrgency.HighValueOffense;
            if (!ShouldUseArea(enemiesInArea, settings)) return SkillAutoUrgency.None;
            return enemiesInArea >= settings.HighValueEnemiesInArea ? SkillAutoUrgency.HighValueOffense : SkillAutoUrgency.General;
        }

        /// <summary>대상 유닛 공격 긴급도(COMBAT-18). 고가치 적을 경직시키면 고가치 공격, 아니면 일반.</summary>
        public static SkillAutoUrgency UnitOffenseUrgency(bool staggersHighValue) =>
            staggersHighValue ? SkillAutoUrgency.HighValueOffense : SkillAutoUrgency.General;

        /// <summary>
        /// 지원(버프·사기) 긴급도(COMBAT-18). 교전 중이 아니면 없음. 대상 분대 사기가 낮으면 사기 안정,
        /// 사기만 올리는 스킬인데 사기가 낮지 않으면 없음, 그 밖은 일반.
        /// </summary>
        public static SkillAutoUrgency SupportUrgency(bool engaged, bool moraleOnly, bool moraleLow)
        {
            if (!engaged) return SkillAutoUrgency.None;
            if (moraleLow) return SkillAutoUrgency.MoraleStabilize;
            return moraleOnly ? SkillAutoUrgency.None : SkillAutoUrgency.General;
        }

        /// <summary>
        /// 교전 높이 판정(COMBAT-18). 근접 영웅은 높이 차가 기준 이하인 적만 교전 중으로 본다(성벽 위에서 성벽 아래 적 제외).
        /// 원거리 영웅이거나 기준이 0이면 높이를 따지지 않는다.
        /// </summary>
        public static bool WithinEngageHeight(bool melee, float heightGap, SkillAutoSettings settings) =>
            settings != null && (!melee || settings.MeleeEngageMaxHeight <= 0f || Mathf.Abs(heightGap) <= settings.MeleeEngageMaxHeight);

        /// <summary>
        /// 쓸 칸 고르기(COMBAT-18). 긴급도가 가장 높은 칸, 같으면 앞 칸. 모두 없음이면 -1.
        /// </summary>
        public static int MostUrgent(IReadOnlyList<SkillAutoUrgency> urgencies)
        {
            int best = -1;
            if (urgencies == null) return best;
            for (int i = 0; i < urgencies.Count; i++)
            {
                if (urgencies[i] == SkillAutoUrgency.None) continue;
                if (best < 0 || urgencies[i] > urgencies[best]) best = i;
            }
            return best;
        }

        static float Ratio(float current, float maximum) =>
            maximum > 0f ? (current <= 0f ? 0f : current >= maximum ? 1f : current / maximum) : 1f;
    }
}
