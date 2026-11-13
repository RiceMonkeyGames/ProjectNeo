namespace ProjectNeo.Run
{
    /// <summary>대장간에서 장비 하나를 만들 수 있는지(D-6). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum CraftCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 만들 수 없다.</summary>
        BattlePending = 1,
        /// <summary>지금 대장간 레벨의 레시피에 없거나 해금되지 않았다.</summary>
        NotCraftable = 2,
        /// <summary>제작 비용이 모자란다.</summary>
        NotEnoughFunds = 3,
    }
}
