namespace ProjectNeo.Walls
{
    /// <summary>성벽 맨 위에 자동으로 세우는 엄폐물 모양(SIEGE-10). 저장·데이터에 쓰므로 값을 바꾸지 않는다.</summary>
    public enum BattlementStyle
    {
        /// <summary>세우지 않는다. 방어 시설이 아닌 시설 레벨의 기본값이다.</summary>
        None = 0,
        /// <summary>허리 높이 난간. 다리만 가린다.</summary>
        Parapet = 1,
        /// <summary>난간 위에 솟은 부분(머론)과 빈틈(크레넬)이 번갈아 있는 성가퀴.</summary>
        Crenellated = 2,
    }
}
