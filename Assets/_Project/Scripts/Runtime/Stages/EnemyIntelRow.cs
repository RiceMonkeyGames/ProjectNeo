namespace ProjectNeo.Stages
{
    /// <summary>적 목록 한 줄(C-5). 숨긴 줄은 종류 이름 없이 "?"로 보인다.</summary>
    public readonly struct EnemyIntelRow
    {
        public const string HiddenLabel = "?";

        /// <summary>종류 이름. 숨긴 줄이면 비어 있다.</summary>
        public string TypeName { get; }
        /// <summary>아직 남은 수(나오지 않았거나 전장에 살아 있는 적).</summary>
        public int Remaining { get; }
        /// <summary>이번 전투에 나오는 전체 수.</summary>
        public int Total { get; }
        public bool Hidden { get; }
        public string Label => Hidden ? HiddenLabel : TypeName;

        public EnemyIntelRow(string typeName, int remaining, int total, bool hidden)
        {
            TypeName = hidden ? string.Empty : typeName ?? string.Empty;
            Remaining = remaining;
            Total = total;
            Hidden = hidden;
        }

        /// <summary>한 줄 글자. 예: "Archer × 3", "? × 4".</summary>
        public string Describe() => $"{Label} × {Remaining}";
    }
}
