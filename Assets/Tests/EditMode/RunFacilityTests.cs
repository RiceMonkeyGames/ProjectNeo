using System;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    /// <summary>D-0: 런 데이터의 시설 상태.</summary>
    public class RunFacilityTests
    {
        FacilityDefinition tavern, shop;
        RunConfig config;

        [SetUp]
        public void SetUp()
        {
            var levels = new[]
            {
                new FacilityLevel { upgradeCost = new Cost(0) },
                new FacilityLevel { upgradeCost = new Cost(60) },
            };
            tavern = FacilityDefinition.Create(FacilityType.Tavern, "주점", levels);
            shop = FacilityDefinition.Create(FacilityType.Shop, "상점", levels);
            config = RunConfig.Create(SquadConfig.Create(4, 10), HeroDefinition.Create("player"), 100, null,
                new[] { tavern, null, shop });
        }

        [Test]
        public void 새_런은_설정의_시설을_모두_시작_레벨로_가진다()
        {
            RunState run = RunState.CreateNew(config);
            Assert.AreEqual(2, run.Facilities.Count, "빈 칸은 건너뛴다.");
            Assert.AreSame(tavern, run.Facility(FacilityType.Tavern).Definition);
            Assert.AreSame(shop, run.Facility(FacilityType.Shop).Definition);
            Assert.IsTrue(run.Facilities.All(facility => facility.Level == 0));
            Assert.IsNull(run.Facility(FacilityType.Smithy));
        }

        [Test]
        public void 시설_레벨은_런마다_따로이고_새_런은_초기화된다()
        {
            RunState first = RunState.CreateNew(config);
            Assert.IsTrue(first.Facility(FacilityType.Shop).TryUpgrade(first.Wallet));
            Assert.AreEqual(1, first.Facility(FacilityType.Shop).Level);
            Assert.AreEqual(40, first.Wallet.Gold);

            RunState second = RunState.CreateNew(config);
            Assert.AreEqual(0, second.Facility(FacilityType.Shop).Level);
            Assert.AreEqual(1, first.Facility(FacilityType.Shop).Level);
        }

        [Test]
        public void 같은_종류의_시설은_두_번_더하지_않는다()
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            run.AddFacility(tavern);
            Assert.Throws<ArgumentException>(() => run.AddFacility(FacilityDefinition.Create(FacilityType.Tavern,
                new FacilityLevel())));
            run.AddFacilities(new[] { tavern, shop });
            Assert.AreEqual(2, run.Facilities.Count);
        }

        [Test]
        public void 시설_업그레이드는_런_변경을_알린다()
        {
            RunState run = RunState.CreateNew(config);
            int changes = 0;
            run.Changed += () => changes++;
            run.Facility(FacilityType.Tavern).TryUpgrade(run.Wallet);
            Assert.Greater(changes, 0);
        }

        [Test]
        public void 시설_이름이_비어_있으면_종류_이름을_쓴다()
        {
            Assert.AreEqual("주점", tavern.DisplayName);
            Assert.AreEqual("Smithy", FacilityDefinition.Create(FacilityType.Smithy, new FacilityLevel()).DisplayName);
        }

        [Test]
        public void 지갑은_가진_재료_목록을_보여준다()
        {
            var bone = MaterialDefinition.Create("bone");
            var wallet = new Wallet();
            wallet.AddMaterial(bone, 2);
            Assert.AreEqual(2, wallet.Materials.Single(entry => entry.Key == bone).Value);
        }
    }
}
