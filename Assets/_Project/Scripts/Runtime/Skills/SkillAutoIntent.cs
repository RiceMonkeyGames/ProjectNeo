namespace ProjectNeo.Skills
{
    /// <summary>자동 시전이 스킬을 어떤 용도로 보는지(COMBAT-06). 저장하지 않는 값이다.</summary>
    public enum SkillAutoIntent
    {
        /// <summary>적에게 거는 효과가 하나라도 있음(피해·디버프·경직).</summary>
        Offense = 0,
        /// <summary>적 효과 없이 회복이 있음.</summary>
        Heal = 1,
        /// <summary>그 밖의 아군 효과(버프·사기).</summary>
        Support = 2,
    }
}
