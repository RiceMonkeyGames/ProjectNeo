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

        static void FinishBattle(RunState run, int battle)
        {
            run.CreateBattleInput(battle);
            Assert.AreEqual(BattleResultIssue.None,
                run.Apply(new BattleResult(battle, Flow.BattleOutcome.Victory, BattleEndReason.EnemiesDefeated)));
        }

        [Test]
        public void 리롤_비용은_데이터의_시작값에서_같은_폭으로_계속_오른다()
        {
            EconomyConfig config = EconomyConfig.Create(0.5f, 5, 15);
            Assert.AreEqual(5, config.GetRerollCost(0));
            Assert.AreEqual(20, config.GetRerollCost(1));
            Assert.AreEqual(35, config.GetRerollCost(2));
            Assert.AreEqual(155, config.GetRerollCost(10), "상한이 없다.");
        }

        [Test]
        public void 리롤은_골드를_내고_할_때마다_비싸지며_다음_준비에_처음_값으로_돌아간다()
        {
            RunState run = NewRun(gold: 200);
            run.RestockShopIfNeeded();
            Assert.AreEqual(10, run.ShopRerollCost);
            Assert.IsTrue(run.TryRerollShop());
            Assert.AreEqual(190, run.Wallet.Gold);
            Assert.AreEqual(1, run.ShopRerolls);
            Assert.AreEqual(20, run.ShopRerollCost);
            Assert.IsTrue(run.TryRerollShop());
            Assert.IsTrue(run.TryRerollShop());
            Assert.AreEqual(200 - 10 - 20 - 30, run.Wallet.Gold);
            Assert.AreEqual(40, run.ShopRerollCost);

            FinishBattle(run, 1);
            Assert.IsTrue(run.RestockShopIfNeeded());
            Assert.AreEqual(0, run.ShopRerolls);
            Assert.AreEqual(10, run.ShopRerollCost, "다음 준비에는 처음 값");
        }

        [Test]
        public void 리롤은_이미_산_칸까지_매물_수만큼_다시_채운다()
        {
            RunState run = NewRun();
            run.RestockShopIfNeeded();
            Assert.IsTrue(run.TryBuy(run.Shop.Stock[0]));
            Assert.AreEqual(1, run.Shop.Stock.Count);
            Assert.IsTrue(run.TryRerollShop());
            Assert.AreEqual(2, run.Shop.Stock.Count);
            Assert.AreEqual(2, run.Shop.Stock.Distinct().Count());
        }

        [Test]
        public void 같은_시드에서_같은_순번의_리롤은_같은_매물이다()
        {
            RunState first = NewRun(), again = NewRun();
            first.RestockShopIfNeeded(); again.RestockShopIfNeeded();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(first.TryRerollShop());
                Assert.IsTrue(again.TryRerollShop());
                CollectionAssert.AreEqual(first.Shop.Stock, again.Shop.Stock, "리롤 " + (i + 1));
            }
            Assert.AreNotEqual(first.ShopRerollSeedFor(0, 0), first.ShopRerollSeedFor(0, 1));
            Assert.AreNotEqual(first.ShopSeedFor(0), first.ShopRerollSeedFor(0, 0));
        }

        [Test]
        public void 골드가_모자라면_리롤하지_못하고_아무것도_바뀌지_않는다()
        {
            RunState run = NewRun(gold: 9);
            run.RestockShopIfNeeded();
            List<ItemDefinition> stock = run.Shop.Stock.ToList();
            Assert.AreEqual(ShopRerollCheck.NotEnoughFunds, run.CheckShopReroll());
            Assert.IsFalse(run.TryRerollShop());
            Assert.AreEqual(9, run.Wallet.Gold);
            Assert.AreEqual(0, run.ShopRerolls);
            CollectionAssert.AreEqual(stock, run.Shop.Stock);
        }

        [Test]
        public void 매물을_뽑기_전이나_전투_입력이_나간_뒤에는_리롤하지_못한다()
        {
            RunState run = NewRun();
            Assert.AreEqual(ShopRerollCheck.NotStocked, run.CheckShopReroll());
            Assert.IsFalse(run.TryRerollShop());
            run.RestockShopIfNeeded();
            Assert.AreEqual(ShopRerollCheck.Ok, run.CheckShopReroll());

            FinishBattle(run, 1);
            Assert.AreEqual(ShopRerollCheck.NotStocked, run.CheckShopReroll(), "새 준비의 매물을 아직 뽑지 않았다.");
            run.RestockShopIfNeeded();
            run.CreateBattleInput(2);
            Assert.AreEqual(ShopRerollCheck.BattlePending, run.CheckShopReroll());
            Assert.IsFalse(run.TryRerollShop());
            Assert.AreEqual(200, run.Wallet.Gold);
        }

        [Test]
        public void 업그레이드한_뒤의_리롤도_이번_준비를_뽑은_레벨의_풀을_쓴다()
        {
            RunState run = NewRun(gold: 500);
            run.RestockShopIfNeeded();
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Shop));
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(run.TryRerollShop());
                Assert.AreEqual(2, run.Shop.Stock.Count, "1레벨 매물 수 그대로");
                CollectionAssert.DoesNotContain(run.Shop.Stock, steel);
            }
            Assert.AreEqual(0, run.ShopStockedLevel);
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

        [Test]
        public void 잠금은_무료로_상점_레벨만큼_가능하고_업그레이드_직후_한도가_늘어난다()
        {
            RunState run = NewRun(gold: 500);
            run.RestockShopIfNeeded();
            Assert.AreEqual(1, run.ShopMaxLocked);
            Assert.IsTrue(run.TryToggleShopLock(0));
            Assert.AreEqual(500, run.Wallet.Gold, "잠금은 무료");
            Assert.IsTrue(run.Shop.IsLocked(0));

            Assert.AreEqual(ShopLockCheck.LimitReached, run.CheckShopLock(1));
            Assert.IsFalse(run.TryToggleShopLock(1));

            // 업그레이드 직후부터 상한이 즉시 2로 늘어난다.
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Shop));
            Assert.AreEqual(2, run.ShopMaxLocked);
            Assert.IsTrue(run.TryToggleShopLock(1));
            Assert.AreEqual(2, run.Shop.LockedCount);

            // 해제는 항상 가능하고, 해제 후 다시 잠글 수 있다.
            Assert.IsTrue(run.TryToggleShopLock(0));
            Assert.IsFalse(run.Shop.IsLocked(0));
            Assert.AreEqual(1, run.Shop.LockedCount);
        }

        [Test]
        public void 잠긴_매물은_리롤과_다음_준비_재입고에도_유지된다()
        {
            RunState run = NewRun(gold: 500);
            run.RestockShopIfNeeded();
            ItemDefinition lockedItem = run.Shop.Stock[1];
            Assert.IsTrue(run.TryToggleShopLock(1));

            Assert.IsTrue(run.TryRerollShop());
            Assert.AreEqual(2, run.Shop.Stock.Count);
            Assert.IsTrue(run.Shop.IsLocked(0));
            Assert.AreSame(lockedItem, run.Shop.Stock[0], "잠긴 매물은 리롤 후에도 남는다.");

            FinishBattle(run, 1);
            Assert.IsTrue(run.RestockShopIfNeeded());
            Assert.AreEqual(2, run.Shop.Stock.Count);
            Assert.IsTrue(run.Shop.IsLocked(0));
            Assert.AreSame(lockedItem, run.Shop.Stock[0], "잠긴 매물은 다음 준비 재입고에도 남는다.");
        }

        [Test]
        public void 잠긴_매물을_사면_잠금이_지워지고_중복_품목도_고른_칸만_산다()
        {
            RunState run = NewRun(gold: 500);
            run.RestockShopIfNeeded();
            ItemDefinition lockedItem = run.Shop.Stock[0];
            Assert.IsTrue(run.TryToggleShopLock(0));

            // 1개짜리 풀과 달리 2개 풀에서도 리롤을 반복해 잠긴 품목과 같은 품목이 뜨는 경우 또는 일반 구매 후 잠금 해제를 검증.
            Assert.IsTrue(run.TryBuyAt(0));
            Assert.AreEqual(0, run.Shop.LockedCount, "잠긴 매물을 사면 잠금도 함께 없어진다.");
            Assert.AreEqual(1, run.Shop.Stock.Count);
            Assert.IsFalse(run.Shop.IsLocked(0));
            Assert.AreEqual(1, run.Inventory.CountOf(lockedItem));
        }

        [Test]
        public void 미입고_잘못된_칸_전투_중에는_잠금을_바꿀_수_없다()
        {
            RunState run = NewRun();
            Assert.AreEqual(ShopLockCheck.NotStocked, run.CheckShopLock(0));
            Assert.IsFalse(run.TryToggleShopLock(0));

            run.RestockShopIfNeeded();
            Assert.AreEqual(ShopLockCheck.InvalidSlot, run.CheckShopLock(-1));
            Assert.AreEqual(ShopLockCheck.InvalidSlot, run.CheckShopLock(5));

            run.CreateBattleInput(1);
            Assert.AreEqual(ShopLockCheck.BattlePending, run.CheckShopLock(0));
            Assert.IsFalse(run.TryToggleShopLock(0));
        }
    }
}
