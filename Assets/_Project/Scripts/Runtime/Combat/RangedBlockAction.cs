namespace ProjectNeo.Combat
{
    /// <summary>교전(Engage) 중인 원거리 유닛이 사선 상태에 따라 할 일(COMBAT-12).</summary>
    public enum RangedBlockAction
    {
        /// <summary>사선이 열려 있다. 평소대로 쏜다.</summary>
        Attack = 0,
        /// <summary>사선을 가린 적으로 대상을 바꾼다.</summary>
        SwitchToBlocker = 1,
        /// <summary>사거리를 지킨 채 기다린다(다가가지 않는다).</summary>
        HoldDistance = 2,
        /// <summary>구조물에 막혔다. 다가가 사선을 찾는다(예전 동작).</summary>
        CloseIn = 3,
    }
}
