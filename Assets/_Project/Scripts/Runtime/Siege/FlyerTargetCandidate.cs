namespace ProjectNeo.Siege
{
    /// <summary>비행 적이 고를 수 있는 아군 하나. 씬 참조 없이 판단에 필요한 값만 담는다.</summary>
    public readonly struct FlyerTargetCandidate
    {
        public readonly int Id;
        public readonly float DistanceSquared;
        public readonly bool OnWallTop;

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop)
        {
            Id = id;
            DistanceSquared = distanceSquared;
            OnWallTop = onWallTop;
        }
    }
}
