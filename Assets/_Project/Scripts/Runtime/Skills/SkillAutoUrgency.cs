namespace ProjectNeo.Skills
{
    /// <summary>
    /// 자동 시전 긴급도(COMBAT-18, 사용자 결정 2026-11-23). 준비된 칸 중 값이 큰 스킬을 먼저 쓰고, 같으면 앞 칸이 먼저다.
    /// 저장하지 않는 값이지만 비교에 쓰므로 숫자를 고정한다.
    /// </summary>
    public enum SkillAutoUrgency
    {
        /// <summary>지금 쓸 이유가 없음.</summary>
        None = 0,
        /// <summary>일반 버프·단일 대상 공격·기준 수만 채운 범위 공격.</summary>
        General = 1,
        /// <summary>회복 기준 이하지만 위급하지 않은 아군 회복.</summary>
        Heal = 2,
        /// <summary>보스·공성 파괴·사다리 적을 잡는 경직, 또는 적이 많이 몰린 범위 공격.</summary>
        HighValueOffense = 3,
        /// <summary>사기가 기준 이하인 분대의 사기를 올림.</summary>
        MoraleStabilize = 4,
        /// <summary>위급 기준 이하로 다친 아군 회복.</summary>
        CriticalHeal = 5,
    }
}
