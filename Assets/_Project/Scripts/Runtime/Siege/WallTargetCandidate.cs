namespace ProjectNeo.Siege
{
    /// <summary>씬 참조 없이 목표 선택 전략에 전달하는 후보 정보.</summary>
    public readonly struct WallTargetCandidate
    {
        public int StructureId { get; }
        public float DistanceSquared { get; }
        public WallTargetCandidate(int structureId, float distanceSquared)
        { StructureId = structureId; DistanceSquared = distanceSquared; }
    }
}
