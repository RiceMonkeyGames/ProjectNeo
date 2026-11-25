namespace ProjectNeo.Run
{
    /// <summary>장인을 한 명 더 고용할 수 있는지(PREP-11). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum ArtisanHireCheck
    {
        Ok = 0,
        /// <summary>준비 시간 설정이 없어 장인을 쓰지 않는다.</summary>
        NotConfigured = 1,
        /// <summary>전투 입력이 나간 뒤라 고용할 수 없다.</summary>
        BattlePending = 2,
        /// <summary>지금 대장간 레벨의 장인 상한에 닿았다.</summary>
        AtCap = 3,
        /// <summary>고용 골드가 모자란다.</summary>
        NotEnoughFunds = 4,
    }
}
