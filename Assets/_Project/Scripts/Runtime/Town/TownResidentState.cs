namespace ProjectNeo.Town
{
    /// <summary>주민 한 명의 지금 상태(SIEGE-36).</summary>
    public enum TownResidentState
    {
        /// <summary>시설 NPC가 제자리(입구 옆)에 서 있다.</summary>
        Standing = 0,
        /// <summary>시민이 산책 지점에서 쉬고 있다.</summary>
        Idle = 1,
        /// <summary>시민이 다음 산책 지점으로 걷고 있다.</summary>
        Walking = 2,
        /// <summary>전투가 시작되어 가까운 건물로 달아나고 있다.</summary>
        Fleeing = 3,
        /// <summary>건물에 숨어 보이지 않는다. 다음 준비 단계에 제자리로 돌아온다.</summary>
        Hidden = 4,
    }
}
