using System;

namespace ProjectNeo.Meta
{
    /// <summary>디스크에 쓰는 저장 파일의 모양(META-01). 형식이 바뀌면 version을 올린다.</summary>
    [Serializable]
    public sealed class MetaSaveFile
    {
        public int version;
        public MetaProgress progress;
    }
}
