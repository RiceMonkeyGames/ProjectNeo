namespace ProjectNeo.Siege
{
    /// <summary>직접 조작 중 지금 쏠 수 있는지. 조준선 색과 안내 문구가 읽는다(SIEGE-07).</summary>
    public enum CannonAimState
    {
        /// <summary>조준한 곳에 바로 쏠 수 있다.</summary>
        Ready = 0,
        /// <summary>재장전 중이라 쏠 수 없다.</summary>
        Reloading = 1,
        /// <summary>조준선이 가리킨 곳을 찾지 못했다(하늘·너무 먼 곳).</summary>
        NoTarget = 2,
        /// <summary>최소 사거리 안이거나 최대 사거리 밖이다.</summary>
        OutOfRange = 3,
        /// <summary>회전 한계 밖이다.</summary>
        OutOfArc = 4,
    }
}
