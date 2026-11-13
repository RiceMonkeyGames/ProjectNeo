namespace ProjectNeo.Run
{
    /// <summary>상점 매물을 리롤할 수 있는지(PREP-02). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum ShopRerollCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 리롤할 수 없다.</summary>
        BattlePending = 1,
        /// <summary>상점이 없거나 이번 준비의 매물을 아직 뽑지 않았다.</summary>
        NotStocked = 2,
        /// <summary>리롤 골드가 모자란다.</summary>
        NotEnoughFunds = 3,
    }
}
