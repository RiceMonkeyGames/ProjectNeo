namespace ProjectNeo.Skills
{
    /// <summary>
    /// 영웅 AI 자동 시전 판단 규칙(Q-63, COMBAT-06). 순수 계산이라 EditMode로 검증한다.
    /// <list type="bullet">
    /// <item>공격: 대상 유닛 스킬은 현재 대상이 사거리 안이면, 범위 스킬은 범위 안 적이 기준 수 이상이면 쓴다.</item>
    /// <item>회복: 범위 안 아군 중 체력 비율이 기준 이하인 유닛이 있으면 쓴다.</item>
    /// <item>지원(버프·사기): 현재 대상 적이 교전 거리 안이면 쓴다.</item>
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

        public static bool ShouldUseArea(int enemiesInArea, SkillAutoSettings settings) =>
            settings != null && enemiesInArea >= settings.MinimumEnemiesInArea;

        public static bool ShouldHeal(float lowestHealthRatio, SkillAutoSettings settings) =>
            settings != null && lowestHealthRatio <= settings.HealBelowHealthRatio;

        public static bool ShouldSupport(bool hasTarget, float targetDistance, SkillAutoSettings settings) =>
            settings != null && hasTarget && targetDistance <= settings.EngageDistance;

        /// <summary>체력 비율(0~1). 최대 체력이 0 이하면 1(다치지 않은 것으로 본다).</summary>
        public static float HealthRatio(float current, float maximum) =>
            maximum > 0f ? (current <= 0f ? 0f : current >= maximum ? 1f : current / maximum) : 1f;
    }
}
