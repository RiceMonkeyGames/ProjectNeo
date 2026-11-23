using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    /// <summary>D-0b 잡졸 모집과 새 유닛 짝짓기. 새 잡졸은 보관 병사로 시작한다(UI-07).</summary>
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
        public void 모집한_잡졸은_어느_부대에도_들지_않고_보관된다()
        {
            RunState run = NewRun(4, 2, new RunSquadSetup(melee), new RunSquadSetup());
            RunSoldier soldier = run.TryRecruit(melee);
            Assert.IsNotNull(soldier);
            Assert.IsNull(run.SquadOf(soldier));
            CollectionAssert.AreEqual(new[] { soldier }, run.StoredSoldiers);
            Assert.AreEqual(2, run.Army.Squads.Count, "부대를 새로 만들지 않는다.");
            Assert.AreEqual(1, run.Army.Squads[0].Members.Count);
        }

        [Test]
        public void 부대가_모두_찼어도_모집한다()
        {
            RunState run = NewRun(1, 1, new RunSquadSetup(melee));
            Assert.IsNotNull(run.TryRecruit(melee));
            Assert.AreEqual(2, run.Soldiers.Count);
            Assert.AreEqual(1, run.StoredSoldiers.Count);
        }

        [Test]
        public void 전투_중에는_모집하지_않는다()
        {
            RunState run = NewRun(4, 4, new RunSquadSetup(melee));
            run.CreateBattleInput(1);
            Assert.IsNull(run.TryRecruit(melee));
            Assert.AreEqual(1, run.Soldiers.Count);
        }

        [Test]
        public void 보관_병사는_전투_입력에_들지_않고_부대에_넣으면_들어간다()
        {
            RunState run = NewRun(4, 4, new RunSquadSetup(melee));
            RunSoldier soldier = run.TryRecruit(melee);
            CollectionAssert.DoesNotContain(run.CreateBattleInput(1).Squads[0].Members, soldier);

            RunState next = NewRun(4, 4, new RunSquadSetup(melee));
            RunSoldier joined = next.TryRecruit(melee);
            Assert.IsTrue(next.TryMoveSoldier(joined, next.Army.Squads[0], out int index));
            Assert.AreEqual(0, index);
            CollectionAssert.Contains(next.CreateBattleInput(1).Squads[0].Members, joined);
        }

        [Test]
        public void 새_유닛은_한_번만_씬_유닛과_짝짓는다()
        {
            var player = new object();
            RunUnitBinding<object> binding = RunUnitBinding<object>.Create(SquadConfig.Create(4, 4), playerHero, player,
                new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<
                    System.Collections.Generic.KeyValuePair<object, SoldierDefinition>>>());
            RunSoldier soldier = binding.State.TryRecruit(melee);
            var unit = new object();
            binding.BindNew(unit, soldier);
            Assert.AreEqual(soldier.Id, binding.IdOf(unit));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(new object(), soldier));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(unit, binding.State.TryRecruit(melee)));
            RunState stranger = NewRun(4, 4, new RunSquadSetup(melee));
            Assert.Throws<System.ArgumentException>(() => binding.BindNew(new object(), stranger.Soldiers[0]));
        }
    }
}
