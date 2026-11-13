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
    /// <summary>C-3 전투 뒤 이어가기: 사상자·도주 반영 한 번, 빈 부대 정리(Q-50: 잡졸도 대장도 없을 때만), 대장만 남은 부대는 사기와 함께 남는다.</summary>
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
        public void 잡졸도_대장도_없는_부대만_없애고_대장만_남은_부대는_사기와_남는다()
        {
            var morale = new SquadMorale(200f, 80f);
            second.Morale = morale;
            Fight((a, UnitFate.Dead), (b, UnitFate.Dead), (c, UnitFate.Fled));

            CollectionAssert.AreEqual(new[] { 0 }, run.RemoveEmptySquads(), "잡졸이 모두 죽은 1번 부대만 없어진다.");
            Assert.AreEqual(2, run.Army.Squads.Count);
            Assert.AreSame(second, run.Army.Squads[0], "대장만 남은 부대는 남는다.");
            Assert.AreSame(captain, second.Leader);
            Assert.AreSame(morale, second.Morale);
            Assert.AreSame(third, run.Army.Squads[1]);
            CollectionAssert.AreEqual(new RunUnit[] { c, d }, third.Members, "도망친 잡졸은 부대에 남아 다음 전투에 돌아온다.");
        }

        [Test]
        public void 대장도_죽으면_그_부대도_없어지고_번호는_큰_것부터_돌려준다()
        {
            Fight((a, UnitFate.Dead), (b, UnitFate.Dead), (captain, UnitFate.Dead));

            CollectionAssert.AreEqual(new[] { 1, 0 }, run.RemoveEmptySquads());
            Assert.AreEqual(1, run.Army.Squads.Count);
            Assert.AreSame(third, run.Army.Squads[0]);
            CollectionAssert.Contains(run.FallenHeroes, captain);
        }

        [Test]
        public void 정리는_한_번만_바뀌고_결과도_두_번_반영되지_않는다()
        {
            BattleResult result = Fight((a, UnitFate.Dead));
            run.RemoveEmptySquads();
            int squads = run.Army.Squads.Count;
            int soldiers = run.Soldiers.Count;

            CollectionAssert.IsEmpty(run.RemoveEmptySquads());
            Assert.AreEqual(BattleResultIssue.NoBattlePending, run.Apply(result), "같은 결과를 다시 반영하지 않는다.");
            Assert.AreEqual(squads, run.Army.Squads.Count);
            Assert.AreEqual(soldiers, run.Soldiers.Count);
        }

        [Test]
        public void 전투_입력이_나간_동안에는_정리하지_않는다()
        {
            run.Unassign(a);
            run.CreateBattleInput(1);
            CollectionAssert.IsEmpty(run.RemoveEmptySquads());
            Assert.AreEqual(3, run.Army.Squads.Count);
        }
    }
}
