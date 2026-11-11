using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Meta;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class ItemAndShopTests
    {
        readonly List<Object> created = new List<Object>();
        EconomyConfig economy;
        Wallet wallet;
        Inventory inventory;
        ItemShop shop;
        ItemDefinition sword;
        ItemDefinition shield;
        ItemDefinition potion;

        [SetUp]
        public void SetUp()
        {
            economy = Track(EconomyConfig.Create(0.5f));
            wallet = new Wallet();
            inventory = new Inventory();
            shop = new ItemShop(economy);
            sword = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(100), true, StatModifier.Flat(StatType.Attack, 10f)));
            shield = Track(ItemDefinition.Create("shield", ItemKind.Equipment, new Cost(60), true, StatModifier.Flat(StatType.Defense, 5f)));
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

        static List<WeightedEntry<ItemDefinition>> Pool(params ItemDefinition[] items)
        {
            var pool = new List<WeightedEntry<ItemDefinition>>();
            foreach (ItemDefinition item in items) pool.Add(new WeightedEntry<ItemDefinition>(item));
            return pool;
        }

        [Test]
        public void Roll_ReturnsDistinctItems_UpToCount()
        {
            List<ItemDefinition> rolled = WeightedPool.Roll(Pool(sword, shield, potion), 2, new SeededRandom(1));

            Assert.AreEqual(2, rolled.Count);
            Assert.AreNotSame(rolled[0], rolled[1]);
        }

        [Test]
        public void Roll_WithSameSeed_GivesSameResult()
        {
            List<WeightedEntry<ItemDefinition>> pool = Pool(sword, shield, potion);

            List<ItemDefinition> first = WeightedPool.Roll(pool, 3, new SeededRandom(42));
            List<ItemDefinition> second = WeightedPool.Roll(pool, 3, new SeededRandom(42));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Roll_SkipsUnavailableAndZeroWeightItems()
        {
            List<WeightedEntry<ItemDefinition>> pool = Pool(sword, shield);
            pool.Add(new WeightedEntry<ItemDefinition>(potion, 0f));

            List<ItemDefinition> rolled = WeightedPool.Roll(pool, 5, new SeededRandom(1), item => item != shield);

            CollectionAssert.AreEqual(new[] { sword }, rolled);
        }

        [Test]
        public void Roll_PutsGuaranteedItemsFirst()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                List<ItemDefinition> rolled = WeightedPool.Roll(Pool(sword, shield, potion), 1, new SeededRandom(seed), null, new[] { potion });

                Assert.AreSame(potion, rolled[0]);
            }
        }

        [Test]
        public void Roll_HidesLockedHero_AndGuaranteesNewlyUnlockedOne()
        {
            HeroDefinition starter = Track(HeroDefinition.Create("starter"));
            HeroDefinition archer = Track(HeroDefinition.Create("archer", unlockedByDefault: false));
            var pool = new List<WeightedEntry<HeroDefinition>>
            {
                new WeightedEntry<HeroDefinition>(starter, 100f),
                new WeightedEntry<HeroDefinition>(archer, 0.001f),
            };
            var progress = new MetaProgress();

            List<HeroDefinition> before = WeightedPool.Roll(pool, 2, new SeededRandom(1), progress.IsUnlocked);
            CollectionAssert.AreEqual(new[] { starter }, before);

            progress.unlockedIds.Add("archer");
            progress.guaranteedNextRunIds.Add("archer");
            List<HeroDefinition> guaranteed = pool.ConvertAll(e => e.item).FindAll(h => progress.guaranteedNextRunIds.Contains(h.Id));
            List<HeroDefinition> after = WeightedPool.Roll(pool, 1, new SeededRandom(1), progress.IsUnlocked, guaranteed);

            CollectionAssert.AreEqual(new[] { archer }, after);
        }

        [Test]
        public void FacilityUpgrade_ChangesWhatTheShopOffers()
        {
            FacilityDefinition shopFacility = Track(FacilityDefinition.Create(FacilityType.Shop,
                new FacilityLevel { offerCount = 1, itemPool = Pool(potion) },
                new FacilityLevel { upgradeCost = new Cost(50), offerCount = 2, itemPool = Pool(sword, shield) }));
            var state = new FacilityState(shopFacility);
            var random = new SeededRandom(5);

            shop.Restock(state.Current.itemPool, state.Current.offerCount, random);
            CollectionAssert.AreEqual(new[] { potion }, shop.Stock);

            wallet.AddGold(50);
            Assert.IsTrue(state.TryUpgrade(wallet));
            shop.Restock(state.Current.itemPool, state.Current.offerCount, random);

            CollectionAssert.AreEquivalent(new[] { sword, shield }, shop.Stock);
        }

        [Test]
        public void TryBuy_PaysPrice_MovesItemFromStockToInventory()
        {
            shop.Restock(Pool(sword), 1, new SeededRandom(1));
            wallet.AddGold(120);

            Assert.IsTrue(shop.TryBuy(sword, wallet, inventory));

            Assert.AreEqual(20, wallet.Gold);
            Assert.AreEqual(0, shop.Stock.Count);
            Assert.AreEqual(1, inventory.CountOf(sword));
        }

        [Test]
        public void TryBuy_Fails_WhenTooPoorOrNotInStock()
        {
            shop.Restock(Pool(sword), 1, new SeededRandom(1));
            wallet.AddGold(99);

            Assert.IsFalse(shop.TryBuy(sword, wallet, inventory));
            Assert.IsFalse(shop.TryBuy(shield, wallet, inventory));
            Assert.AreEqual(99, wallet.Gold);
            Assert.AreEqual(0, inventory.Items.Count);
        }

        [Test]
        public void TrySell_ItemFromBattleLoot_GivesPartOfItsPrice()
        {
            ItemInstance loot = inventory.Add(sword);

            Assert.AreEqual(50, shop.GetSellPrice(sword));
            Assert.IsTrue(shop.TrySell(loot, wallet, inventory));

            Assert.AreEqual(50, wallet.Gold);
            Assert.AreEqual(0, inventory.Items.Count);
        }

        [Test]
        public void TrySell_Fails_ForItemNotInInventory()
        {
            var notOwned = new ItemInstance(sword);

            Assert.IsFalse(shop.TrySell(notOwned, wallet, inventory));
            Assert.AreEqual(0, wallet.Gold);
        }

        [Test]
        public void GetSellPrice_RoundsDown()
        {
            Assert.AreEqual(7, shop.GetSellPrice(potion));
        }
    }
}
