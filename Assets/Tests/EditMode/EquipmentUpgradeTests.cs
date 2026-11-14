using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class EquipmentUpgradeTests
    {
        readonly List<Object> created = new List<Object>();
        EquipmentUpgrade upgrade;
        Inventory inventory;
        Wallet wallet;
        MaterialDefinition hide;
        ItemDefinition sword;

        [SetUp]
        public void SetUp()
        {
            upgrade = new EquipmentUpgrade(Track(EquipmentUpgradeSettings.Create(0.1f, 0.5f, 1, 3, 5)));
            inventory = new Inventory();
            wallet = new Wallet();
            wallet.AddGold(1000);
            hide = Track(MaterialDefinition.Create("hide"));
            wallet.AddMaterial(hide, 100);
            var stages = new ItemUpgradeMaterials[5];
            for (int i = 0; i < stages.Length; i++)
                stages[i] = new ItemUpgradeMaterials(new MaterialAmount(hide, i + 1));
            sword = Track(ItemDefinition.CreateUpgradable("sword", new Cost(100), stages, true,
                StatModifier.Flat(StatType.Attack, 10f), StatModifier.Percent(StatType.MaxHealth, 0.2f)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [Test]
        public void Upgrade_ChargesTargetLevelGoldAndItemMaterials_OnlyForSelectedCopy()
        {
            ItemInstance first = inventory.Add(sword);
            ItemInstance second = inventory.Add(sword);

            Assert.IsTrue(upgrade.TryUpgrade(first, inventory, wallet, 1));
            Assert.IsTrue(upgrade.TryUpgrade(first, inventory, wallet, 1));

            Assert.AreEqual(2, first.UpgradeLevel);
            Assert.AreEqual(150, first.UpgradeGoldSpent);
            Assert.AreEqual(850, wallet.Gold);
            Assert.AreEqual(97, wallet.GetMaterial(hide));
            Assert.AreEqual(0, second.UpgradeLevel);
            Assert.AreEqual(0, second.UpgradeGoldSpent);
            Assert.AreEqual(2, inventory.Items.Count);
        }

        [TestCase(0, 1)]
        [TestCase(1, 3)]
        [TestCase(2, 5)]
        public void SmithyLevel_EnforcesCapWithoutChargingOnFailure(int smithyLevel, int cap)
        {
            ItemInstance item = inventory.Add(sword);
            for (int i = 0; i < cap; i++) Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, smithyLevel));
            int gold = wallet.Gold;
            int material = wallet.GetMaterial(hide);

            Assert.AreEqual(EquipmentUpgradeCheck.MaximumLevel, upgrade.Check(item, inventory, wallet, smithyLevel));
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, smithyLevel));
            Assert.AreEqual(cap, item.UpgradeLevel);
            Assert.AreEqual(gold, wallet.Gold);
            Assert.AreEqual(material, wallet.GetMaterial(hide));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void UnsupportedSmithyLevel_CannotUpgrade(int smithyLevel)
        {
            ItemInstance item = inventory.Add(sword);
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, smithyLevel));
            Assert.AreEqual(1000, wallet.Gold);
            Assert.AreEqual(0, item.UpgradeLevel);
        }

        [TestCase(49, 10)]
        [TestCase(100, 0)]
        public void InsufficientGoldOrMaterials_DoesNotPartiallyPayOrUpgrade(int gold, int materials)
        {
            var poor = new Wallet();
            poor.AddGold(gold);
            poor.AddMaterial(hide, materials);
            ItemInstance item = inventory.Add(sword);

            Assert.AreEqual(EquipmentUpgradeCheck.NotEnoughFunds, upgrade.Check(item, inventory, poor, 0));
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, poor, 0));
            Assert.AreEqual(gold, poor.Gold);
            Assert.AreEqual(materials, poor.GetMaterial(hide));
            Assert.AreEqual(0, item.UpgradeLevel);
            Assert.AreEqual(0, item.UpgradeGoldSpent);
        }

        [Test]
        public void DuplicateMaterials_AreSummedByWallet()
        {
            ItemDefinition definition = Track(ItemDefinition.CreateUpgradable("duplicate", new Cost(100),
                new[] { new ItemUpgradeMaterials(new MaterialAmount(hide, 60), new MaterialAmount(hide, 50)) }));
            ItemInstance item = inventory.Add(definition);

            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, 0));
            Assert.AreEqual(100, wallet.GetMaterial(hide));
            wallet.AddMaterial(hide, 10);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 0));
            Assert.AreEqual(0, wallet.GetMaterial(hide));
            Assert.AreEqual(950, wallet.Gold);
        }

        [Test]
        public void MissingStageData_IsBlocked_ButExplicitEmptyStageCostsOnlyGold()
        {
            ItemDefinition definition = Track(ItemDefinition.CreateUpgradable("one-stage", new Cost(100),
                new[] { new ItemUpgradeMaterials() }));
            ItemInstance item = inventory.Add(definition);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 1));
            Assert.AreEqual(100, wallet.GetMaterial(hide));
            Assert.AreEqual(EquipmentUpgradeCheck.MissingCostData, upgrade.Check(item, inventory, wallet, 1));
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, 1));
            Assert.AreEqual(950, wallet.Gold);
            Assert.AreEqual(1, item.UpgradeLevel);
        }

        [Test]
        public void InvalidMaterialData_CannotBecomeFreeUpgrade()
        {
            ItemDefinition definition = Track(ItemDefinition.CreateUpgradable("invalid", new Cost(100),
                new[] { new ItemUpgradeMaterials(new MaterialAmount(null, 2)) }));
            ItemInstance item = inventory.Add(definition);
            Assert.AreEqual(EquipmentUpgradeCheck.MissingCostData, upgrade.Check(item, inventory, wallet, 0));
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, 0));
            Assert.AreEqual(1000, wallet.Gold);
        }

        [Test]
        public void ConsumableUnownedAndNullItems_AreRejected()
        {
            ItemInstance potion = inventory.Add(Track(ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10))));
            Assert.AreEqual(EquipmentUpgradeCheck.NotEquipment, upgrade.Check(potion, inventory, wallet, 0));
            Assert.IsFalse(upgrade.TryUpgrade(potion, inventory, wallet, 0));
            Assert.IsFalse(upgrade.TryUpgrade(null, inventory, wallet, 0));
            Assert.AreEqual(EquipmentUpgradeCheck.NotOwned, upgrade.Check(new ItemInstance(sword), inventory, wallet, 0));
            Assert.IsFalse(upgrade.TryUpgrade(new ItemInstance(sword), inventory, wallet, 0));
            Assert.AreEqual(1000, wallet.Gold);
        }

        [Test]
        public void Modifiers_ScaleItemFlatAndPercentOnly_WithoutChangingDefinitionOrOtherCopy()
        {
            ItemInstance item = inventory.Add(sword);
            ItemInstance other = inventory.Add(sword);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 1));
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 1));

            IReadOnlyList<StatModifier> modifiers = upgrade.GetEquipModifiers(item);
            var stats = new StatSheet();
            stats.SetBase(StatType.Attack, 20f);
            stats.AddModifiers(modifiers, item);
            Assert.AreEqual(32f, stats.GetValue(StatType.Attack), 0.0001f);
            Assert.AreEqual(ModifierKind.Flat, modifiers[0].kind);
            Assert.AreEqual(ModifierKind.Percent, modifiers[1].kind);
            Assert.AreEqual(0.24f, modifiers[1].value, 0.0001f);
            Assert.AreEqual(10f, sword.EquipModifiers[0].value);
            Assert.AreEqual(10f, upgrade.GetEquipModifiers(other)[0].value);
        }

        [Test]
        public void EquipUnequip_RetainsInstanceState_EquippedItemCannotBeUpgradedViaStash()
        {
            var hero = new Character(Track(HeroDefinition.Create("hero", true, 1,
                new StatValue(StatType.Attack, 20f))));
            ItemInstance item = inventory.Add(sword);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 0));
            Assert.IsTrue(hero.TryEquip(item, inventory));
            Assert.AreEqual(31f, hero.Stats.GetValue(StatType.Attack), 0.0001f);
            Assert.AreSame(item, hero.Equipment[0]);
            Assert.IsFalse(upgrade.TryUpgrade(item, inventory, wallet, 1));
            Assert.IsTrue(hero.TryUnequip(item, inventory));
            Assert.AreEqual(20f, hero.Stats.GetValue(StatType.Attack), 0.0001f);
            Assert.AreSame(item, inventory.Items[0]);
            Assert.AreEqual(1, item.UpgradeLevel);
            Assert.AreEqual(50, item.UpgradeGoldSpent);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 1));
            Assert.AreEqual(2, item.UpgradeLevel);
            Assert.IsTrue(hero.TryEquip(item, inventory));
            Assert.AreEqual(32f, hero.Stats.GetValue(StatType.Attack), 0.0001f);
        }

        [Test]
        public void SellPrice_UsesActualUpgradeSpend_EvenIfSettingsChange()
        {
            var economy = Track(EconomyConfig.Create(0.5f));
            ItemInstance item = inventory.Add(sword);
            Assert.IsTrue(upgrade.TryUpgrade(item, inventory, wallet, 1));
            var cheaper = new EquipmentUpgrade(Track(EquipmentUpgradeSettings.Create(0.1f, 0.25f, 1, 3, 5)));
            Assert.IsTrue(cheaper.TryUpgrade(item, inventory, wallet, 1));

            Assert.AreEqual(100, item.UpgradeGoldSpent);
            Assert.AreEqual(100, EquipmentUpgrade.GetSellPrice(item, economy));
            Assert.AreEqual(50, EquipmentUpgrade.GetSellPrice(new ItemInstance(sword), economy));
            Assert.AreEqual(0, EquipmentUpgrade.GetSellPrice(null, economy));
        }

        [Test]
        public void SettingsControlCapsBonusAndGoldRatio_WithFractionalGoldRoundedDown()
        {
            var custom = new EquipmentUpgrade(Track(EquipmentUpgradeSettings.Create(0.2f, 0.25f, 2)));
            ItemDefinition definition = Track(ItemDefinition.CreateUpgradable("odd-price", new Cost(101),
                new[] { new ItemUpgradeMaterials(), new ItemUpgradeMaterials() }, true,
                StatModifier.Flat(StatType.Attack, 10f)));
            ItemInstance item = inventory.Add(definition);
            Assert.IsTrue(custom.TryUpgrade(item, inventory, wallet, 0));
            Assert.IsTrue(custom.TryUpgrade(item, inventory, wallet, 0));
            Assert.AreEqual(75, item.UpgradeGoldSpent);
            Assert.AreEqual(925, wallet.Gold);
            Assert.AreEqual(14f, custom.GetEquipModifiers(item)[0].value, 0.0001f);
            Assert.IsFalse(custom.TryUpgrade(item, inventory, wallet, 0));
        }
    }
}
