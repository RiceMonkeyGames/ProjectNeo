using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Economy
{
    /// <summary>실제로 가진 아이템 한 개. 착용/해제 시에도 같은 인스턴스의 강화 상태를 유지한다.</summary>
    public sealed class ItemInstance
    {
        EquipmentUpgradeSettings upgradeSettings;
        public ItemDefinition Definition { get; }
        public string DisplayName => UpgradeLevel > 0 ? $"{Definition.DisplayName} +{UpgradeLevel}" : Definition.DisplayName;
        public IReadOnlyList<StatModifier> EquipModifiers => upgradeSettings != null
            ? EquipmentUpgrade.ScaleEquipModifiers(this, upgradeSettings.ModifierBonusPerLevel) : Definition.EquipModifiers;
        public int UpgradeLevel { get; private set; }
        /// <summary>판매 환급에 쓰는 실제 강화 지출. 재료는 환급하지 않는다.</summary>
        public long UpgradeGoldSpent { get; private set; }

        public ItemInstance(ItemDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        }

        internal void ApplyUpgrade(int goldSpent, EquipmentUpgradeSettings settings)
        {
            upgradeSettings = settings;
            UpgradeLevel++;
            UpgradeGoldSpent += goldSpent;
        }

        /// <summary>저장된 강화 상태를 그대로 되살린다(FLOW-02). 비용을 내거나 조건을 보지 않는다.</summary>
        internal void RestoreUpgrade(int level, long goldSpent, EquipmentUpgradeSettings settings)
        {
            UpgradeLevel = Math.Max(0, level);
            UpgradeGoldSpent = Math.Max(0L, goldSpent);
            if (UpgradeLevel > 0) upgradeSettings = settings;
        }
    }
}
