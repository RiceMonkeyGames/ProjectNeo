using System;
using System.Collections.Generic;
using ProjectNeo.Stats;

namespace ProjectNeo.Economy
{
    /// <summary>대장간 UI/런과 독립적인 장비 강화 규칙. 보관함 장비만 강화한다.</summary>
    public sealed class EquipmentUpgrade
    {
        readonly EquipmentUpgradeSettings settings;

        public EquipmentUpgrade(EquipmentUpgradeSettings settings)
        {
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>다음 단계 비용. 골드 소수점은 판매가와 같이 버린다. 데이터가 없으면 false.</summary>
        public bool TryGetNextCost(ItemInstance item, out Cost cost)
        {
            cost = default;
            if (item == null || item.Definition.Kind != ItemKind.Equipment) return false;
            IReadOnlyList<ItemUpgradeMaterials> stages = item.Definition.UpgradeMaterials;
            if (item.UpgradeLevel >= stages.Count) return false;

            double gold = Math.Floor(Math.Max(0, item.Definition.Price.gold)
                * (double)settings.GoldPriceRatio * (item.UpgradeLevel + 1));
            if (double.IsNaN(gold) || gold < 0 || gold > int.MaxValue) return false;
            List<MaterialAmount> materials = stages[item.UpgradeLevel].materials;
            if (materials != null)
                foreach (MaterialAmount entry in materials)
                    if (entry.amount < 0 || (entry.amount > 0 && entry.material == null)) return false;

            cost = new Cost((int)gold, materials == null ? Array.Empty<MaterialAmount>() : materials.ToArray());
            return true;
        }

        public EquipmentUpgradeCheck Check(ItemInstance item, Inventory inventory, Wallet wallet, int smithyLevel)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (item == null || item.Definition.Kind != ItemKind.Equipment) return EquipmentUpgradeCheck.NotEquipment;
            if (!inventory.Contains(item)) return EquipmentUpgradeCheck.NotOwned;
            if (item.UpgradeLevel >= settings.GetMaxLevel(smithyLevel)) return EquipmentUpgradeCheck.MaximumLevel;
            if (!TryGetNextCost(item, out Cost cost)) return EquipmentUpgradeCheck.MissingCostData;
            return wallet.CanAfford(cost) ? EquipmentUpgradeCheck.Allowed : EquipmentUpgradeCheck.NotEnoughFunds;
        }

        public bool TryUpgrade(ItemInstance item, Inventory inventory, Wallet wallet, int smithyLevel)
        {
            if (Check(item, inventory, wallet, smithyLevel) != EquipmentUpgradeCheck.Allowed) return false;
            if (!TryGetNextCost(item, out Cost cost) || !wallet.TrySpend(cost)) return false;
            item.ApplyUpgrade(cost.gold);
            return true;
        }

        /// <summary>캐릭터 기본 스탯이 아닌 이 장비의 수정값만 단계당 보정한다. 원본 데이터는 바꾸지 않는다.</summary>
        public IReadOnlyList<StatModifier> GetEquipModifiers(ItemInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var modifiers = new List<StatModifier>();
            float multiplier = 1f + item.UpgradeLevel * settings.ModifierBonusPerLevel;
            foreach (StatModifier modifier in item.Definition.EquipModifiers)
                modifiers.Add(new StatModifier(modifier.stat, modifier.kind, modifier.value * multiplier));
            return modifiers;
        }

        /// <summary>기본 가격과 실제 강화 골드 지출을 합쳐 판매 비율을 적용한다. 재료는 환급하지 않는다.</summary>
        public static int GetSellPrice(ItemInstance item, EconomyConfig economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (item == null) return 0;
            double total = Math.Max(0, item.Definition.Price.gold) + (double)item.UpgradeGoldSpent;
            return (int)Math.Min(int.MaxValue, Math.Floor(total * economy.SellRatio));
        }
    }
}
