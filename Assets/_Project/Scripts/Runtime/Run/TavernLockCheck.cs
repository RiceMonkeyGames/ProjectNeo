namespace ProjectNeo.Run
{
    /// <summary>주점 영웅 매물을 잠그거나 풀 수 있는지(PREP-12). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum TavernLockCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 잠금을 바꿀 수 없다.</summary>
        BattlePending = 1,
        /// <summary>주점이 없거나 이번 준비의 매물을 아직 뽑지 않았다.</summary>
        NotStocked = 2,
        /// <summary>잘못된 매물 칸 순번이다.</summary>
        InvalidSlot = 3,
        /// <summary>현재 주점 레벨에서 잠글 수 있는 최대 개수에 찼다.</summary>
        LimitReached = 4,
    }
}
