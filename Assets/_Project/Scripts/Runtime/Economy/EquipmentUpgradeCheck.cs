namespace ProjectNeo.Economy
{
    public enum EquipmentUpgradeCheck
    {
        Allowed = 0,
        NotEquipment = 1,
        NotOwned = 2,
        MaximumLevel = 3,
        MissingCostData = 4,
        NotEnoughFunds = 5,
        BattlePending = 6,
        NotConfigured = 7,
        NoSmithy = 8,
        /// <summary>이번 준비에 남은 시간이 강화 시간보다 적다(PREP-06).</summary>
        NotEnoughTime = 9,
    }
}
