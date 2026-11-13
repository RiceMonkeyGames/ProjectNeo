namespace ProjectNeo.Morale
{
    /// <summary>
    /// 사기 규칙이 다른 두 진영. 지금 적은 사기가 없어(Q-44) <see cref="Enemy"/>는 쓰지 않는다.
    /// 나중에 사기를 받는 사람 적을 넣을 때를 위해 남겨 둔다(한계 판정·붕괴·각성 없음).
    /// </summary>
    public enum MoraleSide
    {
        Ally = 0,
        Enemy = 1,
    }

    /// <summary>전투 중 부대의 사기 상태.</summary>
    public enum MoraleState
    {
        Normal = 0,
        Collapsed = 1,
        Awakened = 2,
        Routed = 3,
    }

    /// <summary>한계 판정에서 붕괴했을 때 걸리는 효과(무작위 하나).</summary>
    public enum CollapseEffect
    {
        None = 0,
        Fear = 1,
        Panic = 2,
        Contagion = 3,
    }

    /// <summary>한계 판정에서 각성했을 때 걸리는 효과(무작위 하나).</summary>
    public enum AwakenEffect
    {
        None = 0,
        Rally = 1,
        LastStand = 2,
        Steadfast = 3,
    }
}
