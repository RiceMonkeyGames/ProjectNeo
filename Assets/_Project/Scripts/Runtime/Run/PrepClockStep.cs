namespace ProjectNeo.Run
{
    /// <summary>
    /// 준비 시계가 한 번 간 것(PREP-11). 흐른 시간과, 그 행동을 하느라 그동안 건설·수리에서 빠진 장인 수.
    /// 대장간 제작·강화와 시설 강화는 장인 1명을 쓰고, 기다리기와 영웅 승급은 0명이다.
    /// </summary>
    public readonly struct PrepClockStep
    {
        public float Hours { get; }
        public int OccupiedArtisans { get; }

        public PrepClockStep(float hours, int occupiedArtisans)
        {
            Hours = hours;
            OccupiedArtisans = occupiedArtisans < 0 ? 0 : occupiedArtisans;
        }

        /// <summary>장인 <paramref name="artisans"/>명 중 이번 걸음에 건설·수리를 할 수 있는 수.</summary>
        public int FreeArtisans(int artisans) => artisans - OccupiedArtisans > 0 ? artisans - OccupiedArtisans : 0;
    }
}
