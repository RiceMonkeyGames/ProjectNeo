namespace ProjectNeo.Core
{
    public enum UnlockCategory
    {
        Hero = 0,
        Item = 1,
        Perk = 2,
        Skill = 3,
    }

    /// <summary>해금 포인트로 해금할 수 있는 데이터(영웅, 아이템, 퍽, 스킬)가 구현한다.</summary>
    public interface IUnlockable
    {
        string Id { get; }
        UnlockCategory UnlockCategory { get; }
        /// <summary>true면 해금하지 않아도 처음부터 쓸 수 있다.</summary>
        bool UnlockedByDefault { get; }
    }
}
