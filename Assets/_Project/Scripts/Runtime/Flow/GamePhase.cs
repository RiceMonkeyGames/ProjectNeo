namespace ProjectNeo.Flow
{
    /// <summary>게임 전체 흐름의 단계. Title만 타이틀 씬이고 나머지는 게임 씬 안에서 바뀐다.</summary>
    public enum GamePhase
    {
        Title = 0,
        Preparation = 1,
        Battle = 2,
        Reward = 3,
        RunResult = 4,
        /// <summary>마지막 스테이지 보상 뒤의 엔딩(FLOW-03). 런이 승리로 끝나고 타이틀로만 갈 수 있다.</summary>
        Ending = 5,
    }
}
