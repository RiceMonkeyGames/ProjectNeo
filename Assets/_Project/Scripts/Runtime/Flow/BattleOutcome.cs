namespace ProjectNeo.Flow
{
    /// <summary>전투가 흐름에 돌려주는 결과. 손실·보상 같은 상세 결과는 전투 계약이 정해지면 따로 붙인다.</summary>
    public enum BattleOutcome
    {
        Victory = 0,
        Defeat = 1,
    }
}
