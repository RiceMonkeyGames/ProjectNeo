using System;

namespace ProjectNeo.Skills
{
    /// <summary>효과 종류별 공통 규칙(COMBAT-06). 순수 계산이라 EditMode로 검증한다.</summary>
    public static class SkillEffectRules
    {
        /// <summary>이 효과가 적에게 걸리면 true, 아군에게 걸리면 false.</summary>
        public static bool AffectsEnemies(SkillEffectKind kind)
        {
            switch (kind)
            {
                case SkillEffectKind.Damage:
                case SkillEffectKind.Debuff:
                case SkillEffectKind.Stagger:
                    return true;
                case SkillEffectKind.Buff:
                case SkillEffectKind.Heal:
                case SkillEffectKind.MoraleGain:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>
        /// 피해·회복량 = 고정량 + 시전자 공격력 x 비율. 음수는 0으로 막는다.
        /// 다른 종류는 고정량을 그대로 돌려준다(사기 수치, 경직력).
        /// </summary>
        public static float Amount(SkillEffect effect, float casterAttack)
        {
            float value = effect.amount;
            if (effect.kind == SkillEffectKind.Damage || effect.kind == SkillEffectKind.Heal)
                value += Math.Max(0f, casterAttack) * effect.attackScale;
            return Math.Max(0f, value);
        }

        /// <summary>버프·디버프처럼 시간이 지나면 풀리는 효과인지.</summary>
        public static bool IsTimed(SkillEffectKind kind) =>
            kind == SkillEffectKind.Buff || kind == SkillEffectKind.Debuff;
    }
}
