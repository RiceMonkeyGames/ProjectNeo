namespace ProjectNeo.Run
{
    /// <summary>시설을 한 단계 올릴 수 있는지(D-1, PREP-11). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum FacilityUpgradeCheck
    {
        Ok = 0,
        /// <summary>이 런에 그 시설이 없다.</summary>
        NoFacility = 1,
        /// <summary>이미 최대 레벨이다.</summary>
        MaxLevel = 2,
        /// <summary>업그레이드 비용이 모자란다.</summary>
        NotEnoughFunds = 3,
        /// <summary>이번 준비에 남은 시간이 시설 강화 시간보다 적다. 골드는 쓰지 않는다.</summary>
        NotEnoughTime = 4,
    }
}
