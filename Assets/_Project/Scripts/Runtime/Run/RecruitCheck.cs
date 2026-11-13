namespace ProjectNeo.Run
{
    /// <summary>병영에서 잡졸 하나를 모집할 수 있는지(D-4). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum RecruitCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 모집할 수 없다.</summary>
        BattlePending = 1,
        /// <summary>지금 병영 레벨에서 모집할 수 없는 잡졸이다.</summary>
        NotRecruitable = 2,
        /// <summary>고른 부대가 꽉 찼거나, 모든 부대가 꽉 찼고 부대 수도 상한이다.</summary>
        NoSquad = 3,
        /// <summary>모집 비용이 모자란다.</summary>
        NotEnoughFunds = 4,
    }
}
