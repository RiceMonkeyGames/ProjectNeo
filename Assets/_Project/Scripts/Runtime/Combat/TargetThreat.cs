namespace ProjectNeo.Combat
{
    /// <summary>원거리 유닛이 대상을 고를 때 보는 위협 종류(COMBAT-14). 값이 클수록 먼저 분류된다.</summary>
    public enum TargetThreat
    {
        None = 0,          // 일반 대상. 거리만으로 고른다.
        Ranged = 1,        // 원거리 공격을 하는 적(궁수, 원거리 영웅 등).
        SiegeBreaker = 2,  // 성문·성벽·수성 무기를 부수러 가거나 부수는 중인 적.
        LadderClimber = 3, // 사다리를 세우거나 오르는 중인 적.
    }
}
