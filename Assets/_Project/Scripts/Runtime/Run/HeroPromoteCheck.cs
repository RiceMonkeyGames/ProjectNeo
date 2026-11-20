namespace ProjectNeo.Run
{
    /// <summary>영웅을 승급할 수 있는지(PREP-10). 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum HeroPromoteCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 승급할 수 없다.</summary>
        BattlePending = 1,
        /// <summary>승급 수치나 영웅의 전당(Hero Hall)이 없다.</summary>
        NotConfigured = 2,
        /// <summary>이 런의 살아 있는 영웅이 아니거나 플레이어다.</summary>
        NotPromotable = 3,
        /// <summary>이미 최고 등급이다.</summary>
        MaxGrade = 4,
        /// <summary>경험치가 모자란다.</summary>
        NotEnoughExp = 5,
        /// <summary>승급 비용이 모자란다.</summary>
        NotEnoughFunds = 6,
        /// <summary>이번 준비에 남은 시간이 승급 시간보다 적다(Q-53).</summary>
        NotEnoughTime = 7,
    }
}
