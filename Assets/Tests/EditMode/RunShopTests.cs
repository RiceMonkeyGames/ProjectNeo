using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    /// <summary>D-5 상점 매물·구매·판매와 D-1 시설 업그레이드의 런 규칙.</summary>
    public class RunShopTests
    {
        ItemDefinition potion, sword, armor, steel;
        FacilityDefinition shop;

        [SetUp]
        public void SetUp()
        {
            potion = ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10));
            sword = ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40));
            armor = ItemDefinition.Create("armor", ItemKind.Equipment, new Cost(30));
            steel = ItemDefinition.Create("steel", ItemKind.Equipment, new Cost(80), false);
            shop = FacilityDefinition.Create(FacilityType.Shop, "상점",
                Level(0, 2, potion, sword, armor),
                Level(60, 3, potion, sword, armor, steel));
        }

        static FacilityLevel Level(int gold, int offers, params ItemDefinition[] items) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            offerCount = offers,
            itemPool = items.Select(item => new WeightedEntry<ItemDefinition>(item)).ToList(),
        };

        RunState NewRun(int seed = 7, int gold = 200)
        {
            var run = new RunState(SquadConfig.Create(4, 10), seed);
            run.AddFacility(shop);
            run.SetupShop(EconomyConfig.Create(0.5f));
            run.Wallet.AddGold(gold);
            return run;
        }

        [Test]
        public void 준비마다_한_번_레벨_풀에서_매물_수만큼_뽑고_같은_시드면_같다()
        {
            RunState first = NewRun();
            Assert.IsTrue(first.RestockShopIfNeeded());
            Assert.AreEqual(2, first.Shop.Stock.Count);
            Assert.AreEqual(2, first.Shop.Stock.Distinct().Count());
            Assert.IsTrue(first.Shop.Stock.All(item => item != steel));
            List<ItemDefinition> stock = first.Shop.Stock.ToList();
            Assert.IsFalse(first.RestockShopIfNeeded(), "같은 준비에서는 다시 뽑지 않는다.");
            CollectionAssert.AreEqual(stock, first.Shop.Stock);

            RunState again = NewRun();
            again.RestockShopIfNeeded();
            CollectionAssert.AreEqual(stock, again.Shop.Stock, "같은 런 시드·같은 준비면 같은 매물");
        }

        [Test]
        public void 해금_안_된_아이템은_매물에_나오지_않는다()
        {
            RunState run = NewRun();
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Shop));
            run.RestockShopIfNeeded(item => item.UnlockedByDefault);
            Assert.AreEqual(3, run.Shop.Stock.Count);
            CollectionAssert.DoesNotContain(run.Shop.Stock, steel);
        }

        [Test]
        public void 매물을_사면_골드가_빠지고_보관함에_들어가며_매물에서_사라진다()
        {
            RunState run = NewRun(gold: 100);
            run.RestockShopIfNeeded();
            ItemDefinition item = run.Shop.Stock[0];
            Assert.IsTrue(run.TryBuy(item));
            Assert.AreEqual(100 - item.Price.gold, run.Wallet.Gold);
            Assert.AreEqual(1, run.Inventory.CountOf(item));
            CollectionAssert.DoesNotContain(run.Shop.Stock, item);
            Assert.IsFalse(run.TryBuy(item), "산 매물은 다시 살 수 없다.");
        }

        [Test]
        public void 골드가_모자라면_사지_못하고_아무것도_바뀌지_않는다()
        {
            RunState run = NewRun(gold: 5);
            run.RestockShopIfNeeded();
            ItemDefinition item = run.Shop.Stock[0];
            Assert.IsFalse(run.TryBuy(item));
            Assert.AreEqual(5, run.Wallet.Gold);
            Assert.AreEqual(0, run.Inventory.Items.Count);
            CollectionAssert.Contains(run.Shop.Stock, item);
        }

        [Test]
        public void 보상으로_얻은_아이템도_구매가의_비율로_판다()
        {
            RunState run = NewRun(gold: 0);
            ItemInstance reward = run.Inventory.Add(sword);
            Assert.IsTrue(run.TrySell(reward));
            Assert.AreEqual(20, run.Wallet.Gold);
            Assert.AreEqual(0, run.Inventory.Items.Count);
            Assert.IsFalse(run.TrySell(reward), "이미 판 아이템");
        }

        [Test]
        public void 상점_창구가_없으면_거래하지_않는다()
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            run.AddFacility(shop);
            Assert.IsFalse(run.RestockShopIfNeeded());
            Assert.IsFalse(run.TryBuy(potion));
            Assert.IsFalse(run.TrySell(run.Inventory.Add(potion)));
        }

        [Test]
        public void 시설_업그레이드는_비용을_내고_레벨을_올리며_최대_레벨과_골드_부족은_거부한다()
        {
            RunState poor = NewRun(gold: 59);
            Assert.IsFalse(poor.TryUpgradeFacility(FacilityType.Shop));
            Assert.AreEqual(0, poor.Facility(FacilityType.Shop).Level);
            Assert.AreEqual(59, poor.Wallet.Gold);

            RunState run = NewRun(gold: 100);
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Shop));
            Assert.AreEqual(1, run.Facility(FacilityType.Shop).Level);
            Assert.AreEqual(40, run.Wallet.Gold);
            Assert.IsFalse(run.TryUpgradeFacility(FacilityType.Shop), "최대 레벨");
            Assert.AreEqual(40, run.Wallet.Gold);
            Assert.IsFalse(run.TryUpgradeFacility(FacilityType.Tavern), "없는 시설");
        }

        [Test]
        public void 업그레이드한_레벨은_다음_준비의_매물부터_반영된다()
        {
            RunState run = NewRun(gold: 500);
            run.RestockShopIfNeeded();
            Assert.AreEqual(2, run.Shop.Stock.Count);
            run.TryUpgradeFacility(FacilityType.Shop);
            Assert.IsFalse(run.RestockShopIfNeeded());
            Assert.AreEqual(2, run.Shop.Stock.Count, "이번 준비의 매물은 그대로");

            run.CreateBattleInput(1);
            Assert.AreEqual(BattleResultIssue.None,
                run.Apply(new BattleResult(1, Flow.BattleOutcome.Victory, BattleEndReason.EnemiesDefeated)));
            Assert.IsTrue(run.RestockShopIfNeeded(), "다음 준비에는 새로 뽑는다.");
            Assert.AreEqual(3, run.Shop.Stock.Count);
            Assert.AreEqual(1, run.ShopStockedFor);
        }

        [Test]
        public void 새_런은_설정의_거래_수치로_상점을_만든다()
        {
            var config = RunConfig.Create(SquadConfig.Create(4, 10), HeroDefinition.Create("player"), 0, null,
                new[] { shop }).WithEconomy(EconomyConfig.Create(0.5f));
            RunState run = RunState.CreateNew(config);
            Assert.IsNotNull(run.Shop);
            Assert.AreEqual(-1, run.ShopStockedFor);
        }
    }
}
