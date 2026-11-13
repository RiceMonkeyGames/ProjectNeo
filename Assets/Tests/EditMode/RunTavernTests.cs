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
    /// <summary>D-3 주점: 준비마다 영웅 매물 뽑기, 등급별 골드 고용, 대장 맡기기와 대기 영웅(Q-36·Q-48).</summary>
    public class RunTavernTests
    {
        HeroDefinition playerHero, swordsman, archer, knight, locked;
        FacilityDefinition tavern;

        [SetUp]
        public void SetUp()
        {
            playerHero = HeroDefinition.Create("player");
            swordsman = HeroDefinition.Create("swordsman", 1, new Cost(60));
            archer = HeroDefinition.Create("archer", 1, new Cost(60));
            knight = HeroDefinition.Create("knight", 2, new Cost(120));
            locked = HeroDefinition.Create("locked", 1, new Cost(10), false);
            tavern = FacilityDefinition.Create(FacilityType.Tavern, "주점",
                Level(0, 2, swordsman, archer, locked), Level(60, 3, swordsman, archer, knight));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in new Object[] { playerHero, swordsman, archer, knight, locked, tavern }) Object.DestroyImmediate(asset);
        }

        static FacilityLevel Level(int gold, int offers, params HeroDefinition[] heroes) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            offerCount = offers,
            heroPool = heroes.Select(hero => new WeightedEntry<HeroDefinition>(hero)).ToList(),
        };

        RunState NewRun(int gold = 300, int seed = 5)
        {
            var run = new RunState(SquadConfig.Create(4, 10), seed);
            run.AddHero(playerHero, true);
            run.AddFacility(tavern);
            run.Army.TryAddSquad();
            run.Army.TryAddSquad();
            run.Wallet.AddGold(gold);
            return run;
        }

        static bool Unlocked(HeroDefinition hero) => hero.UnlockedByDefault;

        [Test]
        public void 준비마다_한_번_레벨_풀에서_뽑고_같은_시드면_같으며_잠긴_영웅은_빠진다()
        {
            RunState run = NewRun();
            Assert.IsTrue(run.RestockTavernIfNeeded(Unlocked));
            CollectionAssert.AreEquivalent(new[] { swordsman, archer }, run.TavernOffers);
            Assert.IsFalse(run.RestockTavernIfNeeded(Unlocked), "같은 준비에서는 다시 뽑지 않는다.");

            RunState again = NewRun();
            again.RestockTavernIfNeeded(Unlocked);
            CollectionAssert.AreEqual(run.TavernOffers, again.TavernOffers);
        }

        [Test]
        public void 대기_영웅으로_고용하면_비용을_내고_매물에서_빠지며_대장은_그대로다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded(Unlocked);
            int changed = 0;
            run.Changed += () => changed++;

            RunHero hired = run.TryHire(swordsman, null);

            Assert.IsNotNull(hired);
            Assert.AreEqual(240, run.Wallet.Gold);
            CollectionAssert.DoesNotContain(run.TavernOffers, swordsman);
            CollectionAssert.Contains(run.ReserveHeroes, hired);
            Assert.IsTrue(run.Army.Squads.All(squad => squad.Leader == null));
            Assert.AreEqual(HireCheck.NotOffered, run.CheckHire(swordsman, null), "같은 영웅을 두 번 고용하지 않는다.");
            Assert.Greater(changed, 0);
        }

        [Test]
        public void 부대_대장으로_고용하면_원래_대장은_대기로_간다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded(Unlocked);
            Squad<RunUnit> squad = run.Army.Squads[1];
            RunHero first = run.TryHire(swordsman, squad);
            Assert.AreSame(first, squad.Leader);

            RunHero second = run.TryHire(archer, squad);

            Assert.AreSame(second, squad.Leader);
            CollectionAssert.AreEqual(new[] { first }, run.ReserveHeroes);
        }

        [Test]
        public void 씬_캐릭터를_넘기면_런_영웅이_같은_캐릭터를_쓴다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded(Unlocked);
            var character = new Character(archer);
            RunHero hired = run.TryHire(archer, null, character);
            Assert.AreSame(character, hired.Character);
            Assert.Throws<System.ArgumentException>(() => run.TryHire(swordsman, null, new Character(knight)));
        }

        [Test]
        public void 비용이_모자라거나_전투_중이면_고용하지_않는다()
        {
            RunState poor = NewRun(gold: 30);
            poor.RestockTavernIfNeeded(Unlocked);
            Assert.AreEqual(HireCheck.NotEnoughFunds, poor.CheckHire(swordsman, null));
            Assert.IsNull(poor.TryHire(swordsman, null));
            Assert.AreEqual(30, poor.Wallet.Gold);
            Assert.AreEqual(1, poor.Heroes.Count);

            RunState busy = NewRun();
            busy.RestockTavernIfNeeded(Unlocked);
            busy.CreateBattleInput(1);
            Assert.AreEqual(HireCheck.BattlePending, busy.CheckHire(swordsman, null));
        }

        [Test]
        public void 고용했거나_영구_사망한_영웅은_다음_매물에_다시_나오지_않는다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded(Unlocked);
            run.TryHire(swordsman, null);
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Tavern));
            run.CreateBattleInput(1);
            var result = new BattleResult(1, Flow.BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));

            Assert.IsTrue(run.RestockTavernIfNeeded(Unlocked));
            CollectionAssert.DoesNotContain(run.TavernOffers, swordsman);
            CollectionAssert.AreEquivalent(new[] { archer, knight }, run.TavernOffers, "업그레이드한 레벨의 풀");
        }

        [Test]
        public void 대장으로_고용한_영웅은_전투_입력의_부대장이다()
        {
            RunState run = NewRun();
            run.RestockTavernIfNeeded(Unlocked);
            RunHero hired = run.TryHire(swordsman, run.Army.Squads[0]);
            BattleInput input = run.CreateBattleInput(1);
            Assert.AreSame(hired, input.Squads[0].Leader);
        }
    }
}
