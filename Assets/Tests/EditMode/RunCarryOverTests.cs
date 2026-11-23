using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>C-3 전투 뒤 이어가기: 사상자·도주 반영 한 번, 빈 부대는 남는다(UI-07).</summary>
    public class RunCarryOverTests
    {
        SoldierDefinition melee;
        HeroDefinition playerHero, knight;
        RunState run;
        Squad<RunUnit> first, second, third;
        RunSoldier a, b, c, d;
        RunHero captain;

        [SetUp]
        public void SetUp()
        {
            melee = SoldierDefinition.Create("melee", new Cost(15));
            playerHero = HeroDefinition.Create("player");
            knight = HeroDefinition.Create("knight");
            run = new RunState(SquadConfig.Create(4, 5));
            run.AddHero(playerHero, true);
            first = run.Army.TryAddSquad(); second = run.Army.TryAddSquad(); third = run.Army.TryAddSquad();
            a = run.AddSoldier(melee); b = run.AddSoldier(melee); c = run.AddSoldier(melee); d = run.AddSoldier(melee);
            run.TryAssign(a, first); run.TryAssign(b, second); run.TryAssign(c, third); run.TryAssign(d, third);
            captain = run.AddHero(knight);
            run.TrySetCaptain(second, captain);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in new Object[] { melee, playerHero, knight }) Object.DestroyImmediate(asset);
        }

        BattleResult Fight(params (RunUnit unit, UnitFate fate)[] fates)
        {
            BattleInput input = run.CreateBattleInput(1);
            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            foreach ((RunUnit unit, UnitFate fate) in fates) result.SetFate(unit.Id, fate);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
            return result;
        }

        [Test]
        public void 전투_뒤에도_빈_부대는_남고_도망친_잡졸은_부대에_남는다()
        {
            var morale = new SquadMorale(200f, 80f);
            second.Morale = morale;
            Fight((a, UnitFate.Dead), (b, UnitFate.Dead), (c, UnitFate.Fled), (captain, UnitFate.Dead));

            Assert.AreEqual(3, run.Army.Squads.Count, "빈 부대도 없애지 않는다(UI-07).");
            CollectionAssert.IsEmpty(first.Members);
            Assert.IsNull(second.Leader);
            Assert.AreSame(morale, second.Morale);
            CollectionAssert.Contains(run.FallenHeroes, captain);
            CollectionAssert.AreEqual(new RunUnit[] { c, d }, third.Members, "도망친 잡졸은 부대에 남아 다음 전투에 돌아온다.");
        }

        [Test]
        public void 결과는_두_번_반영되지_않는다()
        {
            BattleResult result = Fight((a, UnitFate.Dead));
            int squads = run.Army.Squads.Count;
            int soldiers = run.Soldiers.Count;

            Assert.AreEqual(BattleResultIssue.NoBattlePending, run.Apply(result), "같은 결과를 다시 반영하지 않는다.");
            Assert.AreEqual(squads, run.Army.Squads.Count);
            Assert.AreEqual(soldiers, run.Soldiers.Count);
        }
    }
}
