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
    }
}
