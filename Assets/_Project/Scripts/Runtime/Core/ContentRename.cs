using System;

namespace ProjectNeo.Core
{
    /// <summary>업데이트에서 바뀐 콘텐츠 ID 하나(META-10). 옛 ID로 저장된 것을 새 ID로 읽는다.</summary>
    [Serializable]
    public struct ContentRename
    {
        public ContentKind kind;
        public string oldId;
        public string newId;

        public ContentRename(ContentKind kind, string oldId, string newId)
        {
            this.kind = kind;
            this.oldId = oldId;
            this.newId = newId;
        }
    }
}
