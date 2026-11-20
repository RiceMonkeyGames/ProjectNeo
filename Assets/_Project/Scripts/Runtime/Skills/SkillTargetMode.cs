namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬이 무엇을 겨누는지(Q-63, COMBAT-06). 에셋에 숫자로 저장되므로 기존 숫자는 바꾸지 말고 끝에 추가한다.
    /// </summary>
    public enum SkillTargetMode
    {
        /// <summary>시전자 주변(반경 안). 반경이 0이면 시전자 자신만.</summary>
        Self = 0,
        /// <summary>조준한 지점 주변(사거리 안의 지점, 반경 안).</summary>
        AimPoint = 1,
        /// <summary>조준한 유닛 하나(사거리 안).</summary>
        TargetUnit = 2,
        /// <summary>시전자가 속한 분대 전체(분대가 없으면 시전자 자신).</summary>
        OwnSquad = 3,
    }
}
