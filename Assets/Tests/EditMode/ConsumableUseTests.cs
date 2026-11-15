using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>전투 소모품 사용 규칙(PREP-04).</summary>
    public class ConsumableUseTests
    {
        readonly List<Object> assets = new List<Object>();
        ItemDefinition potion, weak, sword, inert;

        [SetUp]
        public void SetUp()
        {
            potion = Track(ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10)));
            weak = Track(ItemDefinition.CreateConsumable("weak_potion", new Cost(5), 0.1f));
            inert = Track(ItemDefinition.CreateConsumable("inert", new Cost(5), 0f));
            sword = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [Test]
        public void HealRatio_DefaultsToThirtyFivePercent_AndOnlyConsumablesHaveIt()
        {
            Assert.AreEqual(0.35f, potion.UseHealRatio, 0.0001f, "기존 회복약 에셋이 쓰는 기본값이다.");
            Assert.AreEqual(0.1f, weak.UseHealRatio, 0.0001f);
            Assert.AreEqual(0f, sword.UseHealRatio, "장비는 써서 회복하지 못한다.");
            Assert.IsTrue(ConsumableUse.IsUsable(potion));
            Assert.IsFalse(ConsumableUse.IsUsable(sword));
            Assert.IsFalse(ConsumableUse.IsUsable(inert), "회복 비율이 0이면 쓸 수 있는 소모품이 아니다.");
            Assert.IsFalse(ConsumableUse.IsUsable(null));
        }

        [Test]
        public void HealAmount_IsRatioOfMaximumHealth()
        {
            Assert.AreEqual(35f, ConsumableUse.HealAmount(potion, 100f), 0.001f);
            Assert.AreEqual(70f, ConsumableUse.HealAmount(potion, 200f), 0.001f);
            Assert.AreEqual(0f, ConsumableUse.HealAmount(sword, 100f));
            Assert.AreEqual(0f, ConsumableUse.HealAmount(potion, 0f));
            Assert.AreEqual(0f, ConsumableUse.HealAmount(potion, float.NaN));
        }

        [Test]
        public void FindUsable_SkipsEquipment_AndTakesOldestFirst()
        {
            var inventory = new Inventory();
            Assert.IsNull(ConsumableUse.FindUsable(inventory));
            Assert.IsNull(ConsumableUse.FindUsable(null));
            inventory.Add(sword);
            inventory.Add(inert);
            Assert.IsNull(ConsumableUse.FindUsable(inventory));
            Assert.AreEqual(0, ConsumableUse.CountUsable(inventory));
            ItemInstance first = inventory.Add(weak);
            inventory.Add(potion);
            Assert.AreSame(first, ConsumableUse.FindUsable(inventory));
            Assert.AreEqual(2, ConsumableUse.CountUsable(inventory));
        }

        [Test]
        public void Check_BlocksInOrder_DeadThenNoItemThenCooldownThenFullHealth()
        {
            var item = new ItemInstance(potion);
            Assert.AreEqual(ConsumableUseCheck.Ok, ConsumableUse.Check(item, true, 40f, 100f, 0f));
            Assert.AreEqual(ConsumableUseCheck.Dead, ConsumableUse.Check(item, false, 0f, 100f, 0f));
            Assert.AreEqual(ConsumableUseCheck.Dead, ConsumableUse.Check(null, false, 0f, 100f, 3f));
            Assert.AreEqual(ConsumableUseCheck.NoItem, ConsumableUse.Check(null, true, 40f, 100f, 3f));
            Assert.AreEqual(ConsumableUseCheck.NoItem, ConsumableUse.Check(new ItemInstance(sword), true, 40f, 100f, 0f));
            Assert.AreEqual(ConsumableUseCheck.CoolingDown, ConsumableUse.Check(item, true, 100f, 100f, 0.1f));
            Assert.AreEqual(ConsumableUseCheck.FullHealth, ConsumableUse.Check(item, true, 100f, 100f, 0f));
        }

        [Test]
        public void Health_Heal_ClampsToMaximum_AndNeverRevives()
        {
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, 100f);
            var health = new Health(stats);
            int changed = 0;
            health.Changed += () => changed++;
            Assert.AreEqual(0f, health.Heal(10f), "가득 차 있으면 회복하지 않는다.");
            Assert.AreEqual(0, changed);
            health.TakeDamage(50f);
            changed = 0;
            Assert.AreEqual(35f, health.Heal(35f), 0.001f);
            Assert.AreEqual(85f, health.Current, 0.001f);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(15f, health.Heal(35f), 0.001f, "최대 체력을 넘지 않는다.");
            Assert.AreEqual(100f, health.Current, 0.001f);
            Assert.AreEqual(0f, health.Heal(-5f));
            Assert.AreEqual(0f, health.Heal(float.NaN));
            health.TakeDamage(1000f);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Heal(50f), "죽은 유닛은 되살리지 않는다.");
            Assert.AreEqual(0f, health.Current);
        }

        [Test]
        public void BattleSettings_DefaultKeyIsQ_AndCooldownTenSeconds()
        {
            GameBattleSettings settings = Track(GameBattleSettings.Create(null, null));
            Assert.AreEqual("<Keyboard>/q", GameBattleSettings.DefaultUseConsumableBinding);
            Assert.AreEqual(10f, settings.ConsumableCooldown);
            GameBattleSettings quick = Track(GameBattleSettings.Create(null, null, null, 2.5f));
            Assert.AreEqual(2.5f, quick.ConsumableCooldown);
        }
    }
}
