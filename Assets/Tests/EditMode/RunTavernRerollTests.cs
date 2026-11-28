using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PREP-12 주점 리롤·잠금: 상점(Q-46) 규칙과 같이 오르는 골드, 준비마다 초기화, 레벨만큼 잠금, 고용하면 잠금 해제.</summary>
    public class RunTavernRerollTests
    {
        HeroDefinition playerHero, swordsman, archer, spearman, mage, knight, solo;
        FacilityDefinition tavern, soloTavern;
        EconomyConfig economy;

        [SetUp]
        public void SetUp()
        {
            playerHero = HeroDefinition.Create("player");
            swordsman = HeroDefinition.Create("swordsman", 1, new Cost(60));
            archer = HeroDefinition.Create("archer", 1, new Cost(60));
            spearman = HeroDefinition.Create("spearman", 1, new Cost(60));
            mage = HeroDefinition.Create("mage", 1, new Cost(60));
            knight = HeroDefinition.Create("knight", 2, new Cost(120));
            solo = HeroDefinition.Create("solo", 1, new Cost(60));
            tavern = FacilityDefinition.Create(FacilityType.Tavern, "주점",
                Level(0, 2, swordsman, archer, spearman, mage), Level(60, 3, swordsman, archer, spearman, mage, knight));
            // 영웅이 하나뿐인 풀: 잠근 뒤 리롤하면 같은 영웅이 반드시 다시 나온다.
            soloTavern = FacilityDefinition.Create(FacilityType.Tavern, "주점", Level(0, 2, solo));
            economy = EconomyConfig.Create(0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in new Object[] { playerHero, swordsman, archer, spearman, mage, knight, solo, tavern, soloTavern, economy })
                Object.DestroyImmediate(asset);
        }

        static FacilityLevel Level(int gold, int offers, params HeroDefinition[] heroes) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            offerCount = offers,
            heroPool = heroes.Select(hero => new WeightedEntry<HeroDefinition>(hero)).ToList(),
        };

        RunState NewRun(int gold = 1000, FacilityDefinition facility = null, bool withEconomy = true)
        {
            var run = new RunState(SquadConfig.Create(4, 10), 5);
            run.AddHero(playerHero, true);
            run.AddFacility(facility != null ? facility : tavern);
            if (withEconomy) run.SetupShop(economy);
            run.Wallet.AddGold(gold);
            return run;
        }

        static void NextPreparation(RunState run)
        {
            run.CreateBattleInput(1);
            var result = new BattleResult(1, Flow.BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
        }

        [Test]
        public void 리롤_골드는_상점처럼_10씩_오르고_다음_준비에_처음_값으로_돌아간다()
        {
            Assert.AreEqual(10, economy.GetTavernRerollCost(0));
            Assert.AreEqual(20, economy.GetTavernRerollCost(1));
            Assert.AreEqual(110, economy.GetTavernRerollCost(10), "상한이 없다.");

            RunState run = NewRun();
            Assert.IsTrue(run.RestockTavernIfNeeded());
            Assert.AreEqual(10, run.TavernRerollCost);
            Assert.IsTrue(run.TryRerollTavern());
            Assert.AreEqual(990, run.Wallet.Gold);
            Assert.IsTrue(run.TryRerollTavern());
            Assert.IsTrue(run.TryRerollTavern());
            Assert.AreEqual(940, run.Wallet.Gold, "10 + 20 + 30");
            Assert.AreEqual(3, run.TavernRerolls);
            Assert.AreEqual(40, run.TavernRerollCost);
            Assert.AreEqual(2, run.TavernOffers.Count, "리롤은 매물 수만큼 채운다.");

            NextPreparation(run);
            Assert.IsTrue(run.RestockTavernIfNeeded());
            Assert.AreEqual(0, run.TavernRerolls);
            Assert.AreEqual(10, run.TavernRerollCost, "다음 준비에는 처음 값");
        }

        [Test]
        public void 같은_시드와_순번이면_같은_리롤이고_고용한_칸도_다시_채운다()
        {
            RunState first = NewRun(), again = NewRun();
            first.RestockTavernIfNeeded();
            again.RestockTavernIfNeeded();
            Assert.IsNotNull(first.TryHire(first.TavernOffers[0], null));
            Assert.AreEqual(1, first.TavernOffers.Count);

            Assert.IsTrue(first.TryRerollTavern());
            Assert.IsTrue(again.TryRerollTavern());
            Assert.AreEqual(2, first.TavernOffers.Count, "고용한 칸도 다시 채운다.");
            Assert.AreNotEqual(first.TavernSeedFor(0), first.TavernRerollSeedFor(0, 0));
            Assert.AreNotEqual(first.TavernRerollSeedFor(0, 0), first.TavernRerollSeedFor(0, 1));
            CollectionAssert.DoesNotContain(first.TavernOffers, first.Heroes[1].Definition, "고용한 영웅은 다시 나오지 않는다.");
        }

        [Test]
        public void 리롤은_이번_준비를_뽑은_레벨의_풀을_쓴다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded();
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Tavern));
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(run.TryRerollTavern());
                Assert.AreEqual(2, run.TavernOffers.Count, "레벨 1의 매물 수");
                CollectionAssert.DoesNotContain(run.TavernOffers, knight, "업그레이드는 다음 준비부터");
            }

            NextPreparation(run);
            run.RestockTavernIfNeeded();
            Assert.AreEqual(3, run.TavernOffers.Count);
        }

        [Test]
        public void 잠금은_주점_레벨만큼이고_업그레이드하면_바로_늘며_해제는_언제나_된다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded();
            Assert.AreEqual(1, run.TavernMaxLocked);
            Assert.IsTrue(run.TryToggleTavernLock(0));
            Assert.AreEqual(TavernLockCheck.LimitReached, run.CheckTavernLock(1));
            Assert.IsFalse(run.TryToggleTavernLock(1));
            Assert.AreEqual(TavernLockCheck.InvalidSlot, run.CheckTavernLock(5));

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Tavern));
            Assert.AreEqual(2, run.TavernMaxLocked);
            Assert.IsTrue(run.TryToggleTavernLock(1));
            Assert.AreEqual(2, run.TavernLockedCount);
            Assert.AreEqual(TavernRerollCheck.NoOpenSlot, run.CheckTavernReroll(), "모든 칸이 잠기면 리롤할 칸이 없다.");
            Assert.IsFalse(run.TryRerollTavern());

            Assert.IsTrue(run.TryToggleTavernLock(0));
            Assert.IsFalse(run.IsTavernLocked(0));
            Assert.AreEqual(1, run.TavernLockedCount);
        }

        [Test]
        public void 잠근_영웅은_리롤과_다음_준비에도_남는다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded();
            HeroDefinition kept = run.TavernOffers[1];
            Assert.IsTrue(run.TryToggleTavernLock(1));

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(run.TryRerollTavern());
                Assert.AreSame(kept, run.TavernOffers[0], "잠근 매물은 앞에 남는다.");
                Assert.IsTrue(run.IsTavernLocked(0));
                Assert.IsFalse(run.IsTavernLocked(1));
            }

            NextPreparation(run);
            Assert.IsTrue(run.RestockTavernIfNeeded());
            Assert.AreSame(kept, run.TavernOffers[0]);
            Assert.IsTrue(run.IsTavernLocked(0));
            Assert.AreEqual(2, run.TavernOffers.Count);
        }

        [Test]
        public void 잠긴_영웅과_같은_영웅이_다시_나올_수_있고_고용하면_둘_다_빠지며_잠금도_풀린다()
        {
            RunState run = NewRun(facility: soloTavern);
            run.RestockTavernIfNeeded();
            CollectionAssert.AreEqual(new[] { solo }, run.TavernOffers);
            Assert.IsTrue(run.TryToggleTavernLock(0));

            Assert.IsTrue(run.TryRerollTavern());
            CollectionAssert.AreEqual(new[] { solo, solo }, run.TavernOffers, "새 매물은 잠긴 영웅과 겹칠 수 있다(Q-46).");

            Assert.IsNotNull(run.TryHire(solo, null));
            CollectionAssert.IsEmpty(run.TavernOffers);
            Assert.AreEqual(0, run.TavernLockedCount);
            Assert.AreEqual(HireCheck.NotOffered, run.CheckHire(solo, null), "같은 영웅을 두 번 고용하지 않는다.");
        }

        [Test]
        public void 고용한_잠금_칸이_빠지면_다른_칸의_잠금은_제자리에_남는다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded();
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Tavern));
            HeroDefinition hired = run.TavernOffers[0], other = run.TavernOffers[1];
            Assert.IsTrue(run.TryToggleTavernLock(0));
            Assert.IsTrue(run.TryToggleTavernLock(1));

            Assert.IsNotNull(run.TryHire(hired, null));
            CollectionAssert.AreEqual(new[] { other }, run.TavernOffers);
            Assert.IsTrue(run.IsTavernLocked(0));
            Assert.AreEqual(1, run.TavernLockedCount);
        }

        [Test]
        public void 골드가_모자라거나_전투_중이거나_뽑기_전이면_리롤과_잠금을_막는다()
        {
            RunState poor = NewRun(gold: 5);
            poor.RestockTavernIfNeeded();
            Assert.AreEqual(TavernRerollCheck.NotEnoughFunds, poor.CheckTavernReroll());
            Assert.IsFalse(poor.TryRerollTavern());
            Assert.AreEqual(5, poor.Wallet.Gold);
            Assert.AreEqual(0, poor.TavernRerolls);

            RunState fresh = NewRun();
            Assert.AreEqual(TavernRerollCheck.NotStocked, fresh.CheckTavernReroll());
            Assert.AreEqual(TavernLockCheck.NotStocked, fresh.CheckTavernLock(0));
            fresh.RestockTavernIfNeeded();
            Assert.AreEqual(TavernRerollCheck.Ok, fresh.CheckTavernReroll());
            fresh.CreateBattleInput(1);
            Assert.AreEqual(TavernRerollCheck.BattlePending, fresh.CheckTavernReroll());
            Assert.AreEqual(TavernLockCheck.BattlePending, fresh.CheckTavernLock(0));
            Assert.IsFalse(fresh.TryRerollTavern());

            RunState noEconomy = NewRun(withEconomy: false);
            noEconomy.RestockTavernIfNeeded();
            Assert.AreEqual(TavernRerollCheck.NotStocked, noEconomy.CheckTavernReroll(), "거래 설정이 없으면 리롤 골드를 모른다.");
            Assert.AreEqual(TavernLockCheck.Ok, noEconomy.CheckTavernLock(0), "잠금은 무료라 거래 설정이 없어도 된다.");
        }
    }
}
