namespace ProjectNeo.Run
{
    /// <summary>주점에서 영웅 하나를 고용할 수 있는지(D-3). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum HireCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 고용할 수 없다.</summary>
        BattlePending = 1,
        /// <summary>이번 준비의 주점 매물에 없다(이미 고용했거나 등장하지 않았다).</summary>
        NotOffered = 2,
        /// <summary>대장을 맡길 부대가 이 런의 부대가 아니다.</summary>
        NoSquad = 3,
        /// <summary>고용 비용이 모자란다.</summary>
        NotEnoughFunds = 4,
    }
}
