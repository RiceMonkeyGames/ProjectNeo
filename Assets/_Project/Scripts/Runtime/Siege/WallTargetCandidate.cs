namespace ProjectNeo.Siege
{
    /// <summary>씬 참조 없이 목표 선택 전략에 전달하는 후보 정보.</summary>
    public readonly struct WallTargetCandidate
    {
        public int StructureId { get; }
        public float DistanceSquared { get; }
        /// <summary>남은 체력 비율(0~1). 모르면 1(SIEGE-34).</summary>
        public float HealthRatio { get; }
        /// <summary>이 조각을 이미 치고 있는 다른 성벽 파괴병 수(SIEGE-34).</summary>
        public int EngagedBreakers { get; }

        public WallTargetCandidate(int structureId, float distanceSquared)
            : this(structureId, distanceSquared, 1f, 0) { }

        public WallTargetCandidate(int structureId, float distanceSquared, float healthRatio, int engagedBreakers)
        {
            StructureId = structureId;
            DistanceSquared = distanceSquared;
            HealthRatio = healthRatio < 0f ? 0f : healthRatio > 1f ? 1f : healthRatio;
            EngagedBreakers = engagedBreakers < 0 ? 0 : engagedBreakers;
        }
    }
}
