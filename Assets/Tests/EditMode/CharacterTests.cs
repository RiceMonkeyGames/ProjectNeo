using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
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
        public void TryEquip_SameSlot_ReplacesWornItem_AndReturnsItToInventory()
        {
            Character hero = NewHero();
            ItemInstance first = inventory.Add(sword);
            ItemInstance second = inventory.Add(sword);
            hero.TryEquip(first, inventory);

            Assert.IsTrue(hero.TryEquip(second, inventory, out ItemInstance replaced));

            Assert.AreSame(first, replaced);
            Assert.AreSame(second, hero.EquippedIn(EquipmentSlot.Weapon));
            Assert.IsTrue(inventory.Contains(first));
            Assert.AreEqual(30f, hero.Stats.GetValue(StatType.Attack), "UI-07: 한 칸에 하나만, 바꿔 끼면 이전 장비 효과는 빠진다.");
        }

        [Test]
        public void TryEquip_SameSlot_PutsWornItemIntoTheNewItemsStoragePlace()
        {
            Character hero = NewHero();
            ItemInstance worn = inventory.Add(sword);
            hero.TryEquip(worn, inventory);
            ItemInstance before = inventory.Add(potion);
            ItemInstance replacement = inventory.Add(sword);
            ItemInstance after = inventory.Add(potion);

            Assert.IsTrue(hero.TryEquip(replacement, inventory, out _));

            CollectionAssert.AreEqual(new[] { before, worn, after }, inventory.Items, "UI-07: 끼고 있던 장비가 새 장비 자리로 간다.");
        }

        [Test]
        public void TryEquip_DifferentSlots_StackTheirStats()
        {
            Character hero = NewHero();
            ItemDefinition shield = Track(ItemDefinition.CreateEquipment("shield", EquipmentSlot.Offhand, new Cost(20), StatModifier.Flat(StatType.Attack, 5f)));
            hero.TryEquip(inventory.Add(sword), inventory);

            Assert.IsTrue(hero.TryEquip(inventory.Add(shield), inventory, out ItemInstance replaced));

            Assert.IsNull(replaced);
            Assert.AreEqual(2, hero.Equipment.Count);
            Assert.AreEqual(35f, hero.Stats.GetValue(StatType.Attack));
        }

        [Test]
        public void TryEquip_Fails_WithoutSlots_OrItemIsNotEquipment_OrNotOwned()
        {
            Character hero = NewHero();
            Character dummy = NewHero(slots: 0);
            ItemInstance first = inventory.Add(sword);
            ItemInstance consumable = inventory.Add(potion);

            Assert.IsFalse(hero.TryEquip(consumable, inventory));
            Assert.IsFalse(hero.TryEquip(new ItemInstance(sword), inventory));
            Assert.IsFalse(dummy.TryEquip(first, inventory), "칸 수 0이면 장비를 못 낀다.");
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

        [Test]
        public void ActionInterruptRules_ComparesStaggerAgainstPoiseAndInterruptibility()
        {
            ActionPoise push = ActionPoise.Interruptible(10f);
            Assert.IsFalse(ActionInterruptRules.ShouldInterrupt(push, 9.9f));
            Assert.IsTrue(ActionInterruptRules.ShouldInterrupt(push, 10f));
            Assert.IsTrue(ActionInterruptRules.ShouldInterrupt(push, 25f));
            Assert.IsFalse(ActionInterruptRules.ShouldInterrupt(ActionPoise.Uninterruptible, 999f));

            CombatProfile ranged = Track(CombatProfile.Create(attackKind: AttackKind.Projectile));
            CombatProfile melee = Track(CombatProfile.Create(attackKind: AttackKind.Melee));
            CombatProfile custom = Track(CombatProfile.Create(attackKind: AttackKind.Melee, staggerPower: 40f));
            Assert.AreEqual(10f, ranged.StaggerPower);
            Assert.AreEqual(25f, melee.StaggerPower);
            Assert.AreEqual(40f, custom.StaggerPower);
        }
    }
}
