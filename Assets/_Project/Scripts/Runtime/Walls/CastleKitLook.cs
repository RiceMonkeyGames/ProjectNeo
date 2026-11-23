namespace ProjectNeo.Walls
{
    /// <summary>
    /// 칸이 킷 모듈(5m)보다 넓을 때 면을 덮는 방식(SIEGE-35 7.5m 격자 시험). 칸이 모듈과 같으면 둘 다 같다.
    /// </summary>
    public enum CastleKitLook
    {
        /// <summary>모듈 조각 하나를 면 방향으로만 늘린다(높이·두께는 그대로).</summary>
        Stretch = 0,
        /// <summary>모듈 조각 하나와 반 모듈 조각으로 짠다. 성문·문은 가운데 한 모듈, 양옆을 반 조각을 줄여 채운다.</summary>
        Compose = 1,
    }
}
