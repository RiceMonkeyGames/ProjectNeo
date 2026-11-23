namespace ProjectNeo.Siege
{
    /// <summary>비행 적이 고를 수 있는 대상 하나(아군 또는 수성 무기). 씬 참조 없이 판단에 필요한 값만 담는다.</summary>
    public readonly struct FlyerTargetCandidate
    {
        public readonly int Id;
        public readonly float DistanceSquared;
        public readonly bool OnWallTop;
        /// <summary>먼저 노릴 대상인지(SIEGE-09 결투형 보스: 플레이어·영웅).</summary>
        public readonly bool IsPriority;
        /// <summary>아군이 아니라 설치된 수성 무기(대포)인지(SIEGE-16).</summary>
        public readonly bool IsSiegeWeapon;

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop)
            : this(id, distanceSquared, onWallTop, false) { }

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop, bool isPriority)
            : this(id, distanceSquared, onWallTop, isPriority, false) { }

        public FlyerTargetCandidate(int id, float distanceSquared, bool onWallTop, bool isPriority, bool isSiegeWeapon)
        {
            Id = id;
            DistanceSquared = distanceSquared;
            OnWallTop = onWallTop;
            IsPriority = isPriority;
            IsSiegeWeapon = isSiegeWeapon;
        }

        /// <summary>수성 무기 후보. 성벽 위 아군도, 먼저 노릴 대상도 아니다.</summary>
        public static FlyerTargetCandidate SiegeWeapon(int id, float distanceSquared) =>
            new FlyerTargetCandidate(id, distanceSquared, false, false, true);
    }
}
