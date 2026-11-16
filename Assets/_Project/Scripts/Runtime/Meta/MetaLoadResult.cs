using System;

namespace ProjectNeo.Meta
{
    /// <summary>저장 슬롯을 읽어 얻은 메타 진행과 그 경위(META-01). Progress는 항상 쓸 수 있는 값이다.</summary>
    public readonly struct MetaLoadResult
    {
        public MetaProgress Progress { get; }
        public MetaLoadStatus Status { get; }

        public MetaLoadResult(MetaProgress progress, MetaLoadStatus status)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Status = status;
        }
    }
}
