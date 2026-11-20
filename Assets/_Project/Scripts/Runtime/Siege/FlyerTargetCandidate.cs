namespace ProjectNeo.Siege
{
    /// <summary>비행 적이 고를 수 있는 아군 하나. 씬 참조 없이 판단에 필요한 값만 담는다.</summary>
    public readonly struct FlyerTargetCandidate
    {
        public readonly int Id;
        public readonly float DistanceSquared;
        public readonly bool OnWallTop;
        /// <summary>먼저 노릴 대상인지(SIEGE-09 결투형 보스: 플레이어·영웅).</summary>
        public readonly bool IsPriority;

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop)
            : this(id, distanceSquared, onWallTop, false) { }

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop, bool isPriority)
        {
            Id = id;
            DistanceSquared = distanceSquared;
            OnWallTop = onWallTop;
            IsPriority = isPriority;
        }
    }
}
