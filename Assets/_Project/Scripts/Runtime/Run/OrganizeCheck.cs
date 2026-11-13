namespace ProjectNeo.Run
{
    /// <summary>부대 편성(D-8)에서 잡졸 이동이나 대장 지정을 할 수 있는지. 저장하지 않지만 값은 고정해 두고 새 값은 끝에 더한다.</summary>
    public enum OrganizeCheck
    {
        Ok = 0,
        /// <summary>전투 입력이 나간 뒤라 편성을 바꿀 수 없다.</summary>
        BattlePending = 1,
        /// <summary>이 런의 잡졸·영웅·부대가 아니다.</summary>
        NotInRun = 2,
        /// <summary>이미 그 부대에 있다(혼자인 잡졸을 새 부대로 옮기는 것도 포함).</summary>
        SameSquad = 3,
        /// <summary>받을 부대가 꽉 찼거나, 새 부대를 만들 수 없다(부대 수 상한).</summary>
        SquadFull = 4,
        /// <summary>플레이어는 아직 대장을 맡을 수 없다(Q-32 임시 결정).</summary>
        PlayerNotAllowed = 5,
        /// <summary>대기 중인 영웅이 아니다(이미 다른 부대의 대장).</summary>
        NotReserve = 6,
    }
}
