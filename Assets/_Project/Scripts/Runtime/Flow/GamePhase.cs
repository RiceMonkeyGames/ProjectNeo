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
    }
}
