namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬 효과 종류(Q-65, COMBAT-06). 에셋에 숫자로 저장되므로 기존 숫자는 바꾸지 말고 끝에 추가한다.
    /// 누구에게 걸리는지는 <see cref="SkillEffectRules.AffectsEnemies"/>가 종류로 정한다.
    /// </summary>
    public enum SkillEffectKind
    {
        /// <summary>적에게 피해.</summary>
        Damage = 0,
        /// <summary>아군 스탯을 일정 시간 올린다.</summary>
        Buff = 1,
        /// <summary>적 스탯을 일정 시간 내린다.</summary>
        Debuff = 2,
        /// <summary>아군 체력 회복.</summary>
        Heal = 3,
        /// <summary>아군 분대 사기 상승(COMBAT-07 상승 규칙을 따른다).</summary>
        MoraleGain = 4,
        /// <summary>적 행동을 끊는 경직(COMBAT-04 강인도와 비교).</summary>
        Stagger = 5,
    }
}
