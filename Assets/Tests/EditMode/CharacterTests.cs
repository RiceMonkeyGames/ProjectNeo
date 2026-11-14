using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class CharacterTests
    {
        readonly List<Object> created = new List<Object>();
        Inventory inventory;
        ItemDefinition sword;
        ItemDefinition potion;

        [SetUp]
        public void SetUp()
        {
            inventory = new Inventory();
            sword = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(100), true, StatModifier.Flat(StatType.Attack, 10f)));
            potion = Track(ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(15)));
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

        Character NewHero(int slots = 3)
        {
            HeroDefinition definition = Track(HeroDefinition.Create("knight", true, slots,
                new StatValue(StatType.MaxHealth, 200f), new StatValue(StatType.Attack, 20f)));
            return new Character(definition);
        }

        [Test]
        public void NewCharacter_StartsWithBaseStatsFromDefinition()
        {
            Character hero = NewHero();

            Assert.AreEqual(200f, hero.Stats.GetValue(StatType.MaxHealth));
            Assert.AreEqual(20f, hero.Stats.GetValue(StatType.Attack));
        }

        [Test]
        public void TryEquip_MovesItemOutOfInventory_AndAddsItsStats()
        {
            Character hero = NewHero();
            ItemInstance item = inventory.Add(sword);

            Assert.IsTrue(hero.TryEquip(item, inventory));

            Assert.AreEqual(30f, hero.Stats.GetValue(StatType.Attack));
            Assert.AreEqual(0, inventory.Items.Count);
            Assert.AreEqual(1, hero.Equipment.Count);
        }

        [Test]
        public void TryUnequip_ReturnsItemToInventory_AndRemovesItsStats()
        {
            Character hero = NewHero();
            ItemInstance item = inventory.Add(sword);
            hero.TryEquip(item, inventory);

            Assert.IsTrue(hero.TryUnequip(item, inventory));

            Assert.AreEqual(20f, hero.Stats.GetValue(StatType.Attack));
            Assert.IsTrue(inventory.Contains(item));
        }

        [Test]
        public void TwoCopiesOfSameItem_StackAndUnequipSeparately()
        {
            Character hero = NewHero();
            ItemInstance first = inventory.Add(sword);
            ItemInstance second = inventory.Add(sword);
            hero.TryEquip(first, inventory);
            hero.TryEquip(second, inventory);
            Assert.AreEqual(40f, hero.Stats.GetValue(StatType.Attack));

            hero.TryUnequip(first, inventory);

            Assert.AreEqual(30f, hero.Stats.GetValue(StatType.Attack));
        }

        [Test]
        public void TryEquip_Fails_WhenSlotsAreFull_OrItemIsNotEquipment_OrNotOwned()
        {
            Character hero = NewHero(slots: 1);
            ItemInstance first = inventory.Add(sword);
            ItemInstance second = inventory.Add(sword);
            ItemInstance consumable = inventory.Add(potion);

            Assert.IsFalse(hero.TryEquip(consumable, inventory));
            Assert.IsFalse(hero.TryEquip(new ItemInstance(sword), inventory));
            Assert.IsTrue(hero.TryEquip(first, inventory));
            Assert.IsFalse(hero.TryEquip(second, inventory));
            Assert.AreEqual(2, inventory.Items.Count);
        }

        [Test]
        public void Soldier_CreatesStatsFromDefinition()
        {
            SoldierDefinition spearman = Track(SoldierDefinition.Create("spearman", new Cost(20), new StatValue(StatType.Attack, 8f)));

            StatSheet stats = spearman.CreateStats();

            Assert.AreEqual(8f, stats.GetValue(StatType.Attack));
            Assert.AreEqual(20, spearman.RecruitCost.gold);
        }

        [Test]
        public void JumpRules_SpeedReachesHeight_AirBlendsTakeoffAndInput_AndBlocksInvalidStates()
        {
            float speed = PlayerLocomotionRules.JumpSpeed(1.2f, 25f);
            Assert.That(speed * speed / (2f * 25f), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.AreEqual(0f, PlayerLocomotionRules.JumpSpeed(0f, 25f));
            Assert.AreEqual(0f, PlayerLocomotionRules.JumpSpeed(1.2f, 0f));

            Vector3 air = PlayerLocomotionRules.AirHorizontal(new Vector3(0f, 0f, 10f), new Vector3(5f, 0f, 0f), 0.3f);
            Assert.That(air.z, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(air.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.AreEqual(Vector3.forward, PlayerLocomotionRules.AirHorizontal(Vector3.forward, Vector3.right, -1f));
            Assert.AreEqual(Vector3.right, PlayerLocomotionRules.AirHorizontal(Vector3.forward, Vector3.right, 2f));

            Assert.IsTrue(PlayerLocomotionRules.CanJump(true, false, false, false));
            Assert.IsFalse(PlayerLocomotionRules.CanJump(false, false, false, false));
            Assert.IsFalse(PlayerLocomotionRules.CanJump(true, true, false, false));
            Assert.IsFalse(PlayerLocomotionRules.CanJump(true, false, true, false));
            Assert.IsFalse(PlayerLocomotionRules.CanJump(true, false, false, true));

            var settings = PlayerControlSettings.Create();
            Assert.AreEqual(1.2f, settings.JumpHeight);
            Assert.AreEqual(0.3f, settings.AirControl);
            Object.DestroyImmediate(settings);
        }
    }
}
