using System;
using ProjectNeo.Stats;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬 효과 하나(COMBAT-06). 한 스킬은 여러 효과를 가질 수 있다(예: 피해 + 경직).
    /// 쓰지 않는 칸은 종류에 따라 무시한다.
    /// </summary>
    [Serializable]
    public struct SkillEffect
    {
        public SkillEffectKind kind;
        /// <summary>고정량. 피해·회복은 체력, 사기는 사기 수치, 경직은 경직력.</summary>
        public float amount;
        /// <summary>시전자 공격력에 곱해 <see cref="amount"/>에 더하는 비율(피해·회복만, 1 = 공격력 100%).</summary>
        public float attackScale;
        /// <summary>버프·디버프로 붙는 스탯 수정값.</summary>
        public StatModifier modifier;
        /// <summary>버프·디버프 지속 시간(초).</summary>
        public float duration;

        public static SkillEffect Damage(float amount, float attackScale = 0f) =>
            new SkillEffect { kind = SkillEffectKind.Damage, amount = amount, attackScale = attackScale };

        public static SkillEffect Heal(float amount, float attackScale = 0f) =>
            new SkillEffect { kind = SkillEffectKind.Heal, amount = amount, attackScale = attackScale };

        public static SkillEffect Buff(StatModifier modifier, float duration) =>
            new SkillEffect { kind = SkillEffectKind.Buff, modifier = modifier, duration = duration };

        public static SkillEffect Debuff(StatModifier modifier, float duration) =>
            new SkillEffect { kind = SkillEffectKind.Debuff, modifier = modifier, duration = duration };

        public static SkillEffect MoraleGain(float amount) =>
            new SkillEffect { kind = SkillEffectKind.MoraleGain, amount = amount };

        public static SkillEffect Stagger(float power) =>
            new SkillEffect { kind = SkillEffectKind.Stagger, amount = power };
    }
}
