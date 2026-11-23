namespace ProjectNeo.Walls
{
    /// <summary>
    /// 3층 성벽 칸 안의 계단 방(SIEGE-27). <see cref="BaseLevel"/>부터 <see cref="TopLevel"/>까지 층마다 방이고,
    /// 모든 방 층이 같은 꺾인 계단(<see cref="StairWell"/>, 발치 <see cref="FootFace"/>, 첫 계단 <see cref="FirstFlightFace"/>)을 쌓는다.
    /// 통로로 난 문은 층마다 면 비트(1 &lt;&lt; 면)로 둔다.
    /// </summary>
    public readonly struct StairRoom
    {
        public readonly int BaseLevel;
        public readonly int TopLevel;
        public readonly int FootFace;
        public readonly int FirstFlightFace;
        // (층 − BaseLevel) × 4 + 면 자리에 문 비트.
        readonly int doors;

        public StairRoom(int baseLevel, int topLevel, int footFace, int firstFlightFace, int doors)
        {
            BaseLevel = baseLevel; TopLevel = topLevel; FootFace = footFace; FirstFlightFace = firstFlightFace; this.doors = doors;
        }

        public bool Contains(int level) => level >= BaseLevel && level <= TopLevel;

        /// <summary>그 층의 통로 문 면 비트(1 &lt;&lt; 면). 방 층이 아니면 0.</summary>
        public int Doors(int level) => Contains(level) ? (doors >> ((level - BaseLevel) * 4)) & 0xF : 0;

        public bool HasDoor(int level, int face) => (Doors(level) & (1 << face)) != 0;

        internal static int Bit(int baseLevel, int level, int face) => 1 << ((level - baseLevel) * 4 + face);
    }
}
