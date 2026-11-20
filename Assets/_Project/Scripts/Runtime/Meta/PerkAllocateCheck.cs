namespace ProjectNeo.Meta
{
    /// <summary>퍽을 지금 찍을 수 있는지 확인한 결과(META-09). 화면이 거절 이유를 보여 줄 때 쓴다.</summary>
    public enum PerkAllocateCheck
    {
        Ok = 0,
        UnknownPerk = 1,
        AlreadyAllocated = 2,
        Locked = 3,
        MissingPrerequisite = 4,
        NotEnoughPoints = 5,
        /// <summary>전투 중이라 찍을 수 없다(Q-06).</summary>
        BattlePending = 6,
        /// <summary>런에 퍽 묶음이 아직 없다.</summary>
        NoLoadout = 7,
    }
}
