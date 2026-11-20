namespace ProjectNeo.Walls
{
    /// <summary>한 면을 따라 놓는 성가퀴 한 덩어리. 면의 한쪽 끝에서 잰 구간과, 걷는 면에서 잰 높이.</summary>
    public readonly struct BattlementSegment
    {
        public readonly float Start;
        public readonly float End;
        public readonly float Height;
        public float Length => End - Start;
        public BattlementSegment(float start, float end, float height) { Start = start; End = end; Height = height; }
    }
}
