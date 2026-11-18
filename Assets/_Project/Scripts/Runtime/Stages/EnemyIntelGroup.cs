namespace ProjectNeo.Stages
{
    /// <summary>적 정보(C-5)의 무리 하나: 종류 ID, 보여 줄 이름, 수, 숨김 여부. 편성의 웨이브·무리 순서와 같다.</summary>
    public readonly struct EnemyIntelGroup
    {
        public string TypeId { get; }
        public string TypeName { get; }
        public int Count { get; }
        public bool Hidden { get; }

        public EnemyIntelGroup(string typeId, string typeName, int count, bool hidden)
        {
            TypeId = typeId ?? string.Empty;
            TypeName = string.IsNullOrEmpty(typeName) ? TypeId : typeName;
            Count = count < 0 ? 0 : count;
            Hidden = hidden;
        }
    }
}
