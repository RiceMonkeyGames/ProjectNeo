namespace ProjectNeo.Skills
{
    /// <summary>시전 시도 결과(COMBAT-06). 저장하지 않는 값이다. 실패하면 쿨다운을 쓰지 않는다.</summary>
    public enum SkillCastResult
    {
        Cast = 0,
        /// <summary>빈 칸이거나 칸 번호가 틀림.</summary>
        NoSkill = 1,
        NotReady = 2,
        /// <summary>시전자가 죽었거나 사다리 위처럼 시전할 수 없는 상태.</summary>
        CannotAct = 3,
        /// <summary>TargetUnit 스킬에 알맞은 대상이 없음.</summary>
        NoTarget = 4,
        /// <summary>TargetUnit 대상이 사거리 밖.</summary>
        OutOfRange = 5,
    }
}
