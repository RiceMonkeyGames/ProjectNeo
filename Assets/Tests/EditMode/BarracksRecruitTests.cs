using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>D-4·Q-37 병영 모집(모집 가능 종류, 비용, 배치)과 레벨별 훈련 보너스.</summary>
    public class BarracksRecruitTests
    {
        SoldierDefinition melee;
        SoldierDefinition ranged;
        SoldierDefinition locked;
        HeroDefinition playerHero;
        FacilityDefinition trainingGround;

        [SetUp]
        public void SetUp()
        {
            melee = SoldierDefinition.Create("melee", new Cost(15));
            ranged = SoldierDefinition.Create("ranged", new Cost(20));
            locked = SoldierDefinition.Create("locked", new Cost(1));
            playerHero = HeroDefinition.Create("player");
            trainingGround = FacilityDefinition.Create(FacilityType.TrainingGround,
                Level(0, 0f, melee, ranged), Level(60, 0.1f, melee, ranged), Level(120, 0.2f, melee, ranged));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in new Object[] { melee, ranged, locked, playerHero, trainingGround }) Object.DestroyImmediate(asset);
        }

        static FacilityLevel Level(int gold, float bonus, params SoldierDefinition[] soldiers) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            recruitableSoldiers = new List<SoldierDefinition>(soldiers),
            soldierModifiers = bonus > 0f
                ? new List<StatModifier> { StatModifier.Percent(StatType.MaxHealth, bonus), StatModifier.Percent(StatType.Attack, bonus) }
                : new List<StatModifier>(),
        };

        RunState NewRun(int gold, int maxSquads, int maxMembers, params RunSquadSetup[] squads)
        {
            RunState run = RunState.CreateNew(RunConfig.Create(SquadConfig.Create(maxSquads, maxMembers), playerHero, gold, null, squads));
            run.AddFacility(trainingGround);
            return run;
        }

        [Test]
        public void 병영이_없으면_모집할_수_있는_잡졸이_없다()
        {
            RunState run = RunState.CreateNew(RunConfig.Create(SquadConfig.Create(4, 4), playerHero, 100, null, new RunSquadSetup()));
            Assert.AreEqual(0, run.RecruitableSoldiers.Count);
            Assert.AreEqual(RecruitCheck.NotRecruitable, run.CheckBarracksRecruit(melee));
            Assert.IsNull(run.TryRecruitAtBarracks(melee));
        }

        [Test]
        public void 모집하면_비용을_내고_보관_병사가_된다()
        {
            RunState run = NewRun(100, 4, 2, new RunSquadSetup(melee, melee), new RunSquadSetup(melee));
            CollectionAssert.AreEqual(new[] { melee, ranged }, run.RecruitableSoldiers);

            RunSoldier soldier = run.TryRecruitAtBarracks(ranged);

            Assert.IsNotNull(soldier);
            Assert.AreSame(ranged, soldier.Definition);
            Assert.IsNull(run.SquadOf(soldier));
            CollectionAssert.AreEqual(new[] { soldier }, run.StoredSoldiers);
            Assert.AreEqual(80, run.Wallet.Gold);
        }

        [Test]
        public void 부대가_모두_찼어도_모집하고_부대를_새로_만들지_않는다()
        {
            RunState run = NewRun(100, 1, 1, new RunSquadSetup(melee));
            Assert.AreEqual(RecruitCheck.Ok, run.CheckBarracksRecruit(melee));
            Assert.IsNotNull(run.TryRecruitAtBarracks(melee));
            Assert.AreEqual(1, run.Army.Squads.Count);
            Assert.AreEqual(85, run.Wallet.Gold);
        }

        [Test]
        public void 비용이_모자라거나_모집할_수_없는_종류거나_전투_중이면_거부한다()
        {
            RunState run = NewRun(19, 4, 4, new RunSquadSetup());
            Assert.AreEqual(RecruitCheck.NotEnoughFunds, run.CheckBarracksRecruit(ranged));
            Assert.IsNull(run.TryRecruitAtBarracks(ranged));
            Assert.AreEqual(RecruitCheck.NotRecruitable, run.CheckBarracksRecruit(locked));
            Assert.AreEqual(RecruitCheck.NotRecruitable, run.CheckBarracksRecruit(null));
            Assert.AreEqual(19, run.Wallet.Gold);
            Assert.AreEqual(0, run.Soldiers.Count);

            run.CreateBattleInput(1);
            Assert.AreEqual(RecruitCheck.BattlePending, run.CheckBarracksRecruit(melee));
            Assert.IsNull(run.TryRecruitAtBarracks(melee));
            Assert.AreEqual(19, run.Wallet.Gold);
        }

        [Test]
        public void 훈련_보너스는_병영_레벨을_따르고_다시_적용해도_겹치지_않는다()
        {
            RunState run = NewRun(500, 4, 4, new RunSquadSetup());
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, 100f);
            stats.SetBase(StatType.Attack, 10f);
            stats.AddModifier(StatModifier.Flat(StatType.Attack, 5f), this);

            SoldierTraining.Apply(stats, run.TrainingGround);
            Assert.AreEqual(100f, stats.GetValue(StatType.MaxHealth), 0.001f, "1레벨은 보너스가 없다.");

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.TrainingGround));
            SoldierTraining.Apply(stats, run.TrainingGround);
            SoldierTraining.Apply(stats, run.TrainingGround);
            Assert.AreEqual(110f, stats.GetValue(StatType.MaxHealth), 0.001f);
            Assert.AreEqual(16.5f, stats.GetValue(StatType.Attack), 0.001f, "다른 출처의 수정값은 그대로 두고 비율을 더한다.");

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.TrainingGround));
            SoldierTraining.Apply(stats, run.TrainingGround);
            Assert.AreEqual(120f, stats.GetValue(StatType.MaxHealth), 0.001f, "3레벨 보너스로 바뀌고 2레벨 것과 겹치지 않는다.");

            SoldierTraining.Apply(stats, null);
            Assert.AreEqual(100f, stats.GetValue(StatType.MaxHealth), 0.001f, "병영이 없으면 훈련 보너스를 지운다.");
            Assert.AreEqual(15f, stats.GetValue(StatType.Attack), 0.001f);
        }
    }
}
