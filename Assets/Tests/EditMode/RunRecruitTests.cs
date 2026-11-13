using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    /// <summary>D-0b·Q-36 잡졸 모집 배치 규칙과 새 유닛 짝짓기.</summary>
    public class RunRecruitTests
    {
        SoldierDefinition melee;
        HeroDefinition playerHero;

        [SetUp]
        public void SetUp()
        {
            melee = SoldierDefinition.Create("melee", new Cost(0));
            playerHero = HeroDefinition.Create("player");
        }

        RunState NewRun(int maxSquads, int maxMembers, params RunSquadSetup[] squads) =>
            RunState.CreateNew(RunConfig.Create(SquadConfig.Create(maxSquads, maxMembers), playerHero, 0, null, squads));

        [Test]
        public void 부대를_고르지_않으면_자리가_남은_첫_부대에_들어간다()
        {
            RunState run = NewRun(4, 2, new RunSquadSetup(melee, melee), new RunSquadSetup(melee));
            RunSoldier soldier = run.TryRecruit(melee, null, out Squad<RunUnit> squad);
            Assert.IsNotNull(soldier);
            Assert.AreSame(run.Army.Squads[1], squad);
            Assert.AreSame(squad, run.SquadOf(soldier));
            Assert.AreEqual(1, run.IndexOf(squad));
            Assert.AreEqual(2, run.Army.Squads.Count);
        }

        [Test]
        public void 고른_부대에_자리가_있으면_그_부대에_들어간다()
        {
            RunState run = NewRun(4, 3, new RunSquadSetup(melee), new RunSquadSetup(melee));
            Squad<RunUnit> chosen = run.Army.Squads[1];
            run.TryRecruit(melee, chosen, out Squad<RunUnit> squad);
            Assert.AreSame(chosen, squad);
            Assert.AreEqual(2, chosen.Members.Count);
        }

        [Test]
        public void 고른_부대가_꽉_찼거나_다른_런의_부대면_모집하지_않는다()
        {
            RunState run = NewRun(4, 1, new RunSquadSetup(melee), new RunSquadSetup());
            int before = run.Soldiers.Count;
            Assert.IsNull(run.TryRecruit(melee, run.Army.Squads[0], out _));
            RunState other = NewRun(4, 1, new RunSquadSetup());
            Assert.IsNull(run.TryRecruit(melee, other.Army.Squads[0], out _));
            Assert.AreEqual(before, run.Soldiers.Count);
        }

        [Test]
        public void 모든_부대가_꽉_찼으면_상한까지_새_부대를_만든다()
        {
            RunState run = NewRun(2, 1, new RunSquadSetup(melee));
            Assert.IsTrue(run.TryFindRecruitSquad(null, out Squad<RunUnit> found, out bool needsNew));
            Assert.IsNull(found);
            Assert.IsTrue(needsNew);
            Assert.AreEqual(1, run.Army.Squads.Count, "찾기만 해서는 부대를 만들지 않는다.");

            run.TryRecruit(melee, null, out Squad<RunUnit> squad);
            Assert.AreEqual(2, run.Army.Squads.Count);
            Assert.AreSame(run.Army.Squads[1], squad);
        }

        [Test]
        public void 부대_수_상한까지_꽉_찼으면_모집하지_않는다()
        {
            RunState run = NewRun(1, 1, new RunSquadSetup(melee));
            Assert.IsFalse(run.TryFindRecruitSquad(null, out _, out bool needsNew));
            Assert.IsFalse(needsNew);
            Assert.IsNull(run.TryRecruit(melee, null, out _));
            Assert.AreEqual(1, run.Soldiers.Count);
        }

        [Test]
        public void 전투_중에는_모집하지_않는다()
        {
            RunState run = NewRun(4, 4, new RunSquadSetup(melee));
            run.CreateBattleInput(1);
            Assert.IsNull(run.TryRecruit(melee, null, out _));
            Assert.AreEqual(1, run.Soldiers.Count);
        }

        [Test]
        public void 모집한_잡졸은_다음_전투_입력에_들어간다()
        {
            RunState run = NewRun(4, 4, new RunSquadSetup(melee));
            RunSoldier soldier = run.TryRecruit(melee, null, out _);
            BattleInput input = run.CreateBattleInput(1);
            CollectionAssert.Contains(input.Squads[0].Members, soldier);
        }

        [Test]
        public void 새_유닛은_한_번만_씬_유닛과_짝짓는다()
        {
            var player = new object();
            RunUnitBinding<object> binding = RunUnitBinding<object>.Create(SquadConfig.Create(4, 4), playerHero, player,
                new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<
                    System.Collections.Generic.KeyValuePair<object, SoldierDefinition>>>());
            RunSoldier soldier = binding.State.TryRecruit(melee, null, out _);
            var unit = new object();
            binding.BindNew(unit, soldier);
            Assert.AreEqual(soldier.Id, binding.IdOf(unit));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(new object(), soldier));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(unit, binding.State.TryRecruit(melee, null, out _)));
            RunState stranger = NewRun(4, 4, new RunSquadSetup(melee));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(new object(), stranger.Soldiers[0]));
        }
    }
}
