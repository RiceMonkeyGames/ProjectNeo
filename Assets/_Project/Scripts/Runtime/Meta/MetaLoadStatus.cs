namespace ProjectNeo.Meta
{
    /// <summary>저장 슬롯을 읽은 결과(META-01).</summary>
    public enum MetaLoadStatus
    {
        /// <summary>저장 파일이 없다. 새 진행으로 시작한다.</summary>
        Empty = 0,
        /// <summary>본 파일을 정상적으로 읽었다.</summary>
        Loaded = 1,
        /// <summary>본 파일이 없거나 깨져서 직전 백업을 읽었다.</summary>
        RestoredFromBackup = 2,
        /// <summary>본 파일과 백업 모두 읽을 수 없다. 새 진행으로 시작한다.</summary>
        Unreadable = 3,
    }
}
