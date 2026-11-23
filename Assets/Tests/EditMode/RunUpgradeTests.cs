using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class RunUpgradeTests
    {
        readonly List<Object> created = new List<Object>();
        RunState run;
        ItemDefinition sword;
        MaterialDefinition hide;
        EquipmentUpgradeSettings settings;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            hide = Track(MaterialDefinition.Create("hide"));
            var materials = new ItemUpgradeMaterials[5];
            for (int i = 0; i < 5; i++) materials[i] = new ItemUpgradeMaterials(new MaterialAmount(hide, (i + 1) * 2));
            sword = Track(ItemDefinition.CreateUpgradable("sword", new Cost(40), materials, true,
                StatModifier.Flat(StatType.Attack, 10f)));
            settings = Track(EquipmentUpgradeSettings.Create(0.1f, 0.5f, 1, 3, 5));
            var smithy = Track(FacilityDefinition.Create(FacilityType.Smithy,
                new FacilityLevel { upgradeCost = new Cost(0) },
                new FacilityLevel { upgradeCost = new Cost(10) },
                new FacilityLevel { upgradeCost = new Cost(10) }));
            var hero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            var config = Track(RunConfig.Create(Track(SquadConfig.Create(4, 10)), hero, 1000,
                new[] { new MaterialAmount(hide, 100) }, new[] { smithy }))
                .WithEconomy(Track(EconomyConfig.Create(0.5f))).WithEquipmentUpgrades(settings);
            run = RunState.CreateNew(config);
        }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        [Test]
        public void UpgradedEquipment_AppliesScaledStats_AndSurvivesNextPreparation()
        {
            ItemInstance item = run.Inventory.Add(sword);
            int changes = 0;
            run.Changed += () => changes++;
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(980, run.Wallet.Gold);
            Assert.AreEqual(98, run.Wallet.GetMaterial(hide));
            Assert.IsTrue(run.TryEquip(run.Player, item));
            Assert.AreEqual(31f, run.Player.Character.Stats.GetValue(StatType.Attack), 0.0001f);
            Assert.AreEqual(EquipmentUpgradeCheck.NotOwned, run.CheckUpgradeEquipment(item));
            Assert.IsFalse(run.TryUpgradeEquipment(item));

            run.CreateBattleInput(1);
            Assert.AreEqual(EquipmentUpgradeCheck.BattlePending, run.CheckUpgradeEquipment(item));
            Assert.IsFalse(run.TryUpgradeEquipment(item));
            Assert.AreEqual(BattleResultIssue.None, run.Apply(new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated)));
            Assert.IsTrue(run.TryUnequip(run.Player, item));
            Assert.AreEqual(20f, run.Player.Character.Stats.GetValue(StatType.Attack));
            Assert.AreEqual(1, item.UpgradeLevel);
            Assert.AreEqual(20, item.UpgradeGoldSpent);
            Assert.IsTrue(run.TryEquip(run.Player, item));
            Assert.AreEqual(31f, run.Player.Character.Stats.GetValue(StatType.Attack), 0.0001f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpgradedEquipment_SellsForBaseAndActualUpgradeSpend_Once(bool smithy)
        {
            ItemInstance item = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.AreEqual(30, run.Shop.GetInstanceSellPrice(item));
            int gold = run.Wallet.Gold;
            Assert.IsTrue(smithy ? run.TrySellAtSmithy(item) : run.TrySell(item));
            Assert.AreEqual(gold + 30, run.Wallet.Gold);
            Assert.AreEqual(98, run.Wallet.GetMaterial(hide), "판매는 재료를 환급하지 않는다.");
            Assert.IsFalse(smithy ? run.TrySellAtSmithy(item) : run.TrySell(item));
            Assert.AreEqual(gold + 30, run.Wallet.Gold);
        }

        [Test]
        public void SmithyUpgrade_ImmediatelyRaisesEquipmentCap_WithoutChangingOtherCopy()
        {
            ItemInstance item = run.Inventory.Add(sword);
            ItemInstance other = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.AreEqual(EquipmentUpgradeCheck.MaximumLevel, run.CheckUpgradeEquipment(item));
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            Assert.AreEqual(3, run.MaxEquipmentUpgrade);
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.IsFalse(run.TryUpgradeEquipment(item));
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.IsTrue(run.TryUpgradeEquipment(item));
            Assert.AreEqual(5, item.UpgradeLevel);
            Assert.AreEqual(300, item.UpgradeGoldSpent);
            Assert.IsFalse(run.TryUpgradeEquipment(item));
            Assert.AreEqual(0, other.UpgradeLevel);
            // 장비는 종류마다 한 칸이라 같은 무기 둘을 끼면 나중 것이 앞의 것과 자리를 바꾼다(UI-07).
            Assert.IsTrue(run.TryEquip(run.Player, other));
            float withOther = run.Player.Character.Stats.GetValue(StatType.Attack);
            Assert.IsTrue(run.TryEquip(run.Player, item));
            Assert.Greater(run.Player.Character.Stats.GetValue(StatType.Attack), withOther);
            Assert.AreEqual(0, other.UpgradeLevel);
        }

        [Test]
        public void UnconfiguredOrMissingSmithy_CannotUpgrade_AndDoesNotPay()
        {
            var empty = new RunState(Track(SquadConfig.Create(4, 10)));
            ItemInstance item = empty.Inventory.Add(sword);
            empty.Wallet.AddGold(100);
            Assert.AreEqual(EquipmentUpgradeCheck.NotConfigured, empty.CheckUpgradeEquipment(item));
            Assert.IsFalse(empty.TryUpgradeEquipment(item));
            empty.SetupEquipmentUpgrades(settings);
            Assert.AreEqual(EquipmentUpgradeCheck.NoSmithy, empty.CheckUpgradeEquipment(item));
            Assert.IsFalse(empty.TryUpgradeEquipment(item));
            Assert.AreEqual(100, empty.Wallet.Gold);
            Assert.AreEqual(0, item.UpgradeLevel);
        }

        [Test]
        public void PoorRun_AndPendingBattle_AreRejectedWithoutMutation()
        {
            ItemInstance item = run.Inventory.Add(sword);
            run.Wallet.TrySpend(new Cost(990));
            Assert.AreEqual(EquipmentUpgradeCheck.NotEnoughFunds, run.CheckUpgradeEquipment(item));
            Assert.IsFalse(run.TryUpgradeEquipment(item));
            Assert.AreEqual(10, run.Wallet.Gold);
            run.CreateBattleInput(1);
            Assert.AreEqual(EquipmentUpgradeCheck.BattlePending, run.CheckUpgradeEquipment(item));
            Assert.IsFalse(run.TryUpgradeEquipment(item));
            Assert.AreEqual(100, run.Wallet.GetMaterial(hide));
            Assert.AreEqual(0, item.UpgradeLevel);
        }
    }
}
