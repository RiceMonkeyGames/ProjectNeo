using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>D-8 부대 편성: 잡졸 한 명씩 옮기기, 빈 부대 정리, 대장 지정(Q-32 임시·Q-45·Q-47).</summary>
    public class RunOrganizationTests
    {
        SoldierDefinition melee;
        HeroDefinition playerHero, knight, archer;
        RunState run;
        Squad<RunUnit> first, second;
        RunSoldier a, b, c;

        [SetUp]
        public void SetUp()
        {
            melee = SoldierDefinition.Create("melee", new Cost(15));
            playerHero = HeroDefinition.Create("player");
            knight = HeroDefinition.Create("knight");
            archer = HeroDefinition.Create("archer");
            run = new RunState(SquadConfig.Create(3, 2));
            run.AddHero(playerHero, true);
            first = run.Army.TryAddSquad();
            second = run.Army.TryAddSquad();
            a = run.AddSoldier(melee); b = run.AddSoldier(melee); c = run.AddSoldier(melee);
            run.TryAssign(a, first); run.TryAssign(b, first); run.TryAssign(c, second);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in new Object[] { melee, playerHero, knight, archer }) Object.DestroyImmediate(asset);
        }

        [Test]
        public void 잡졸_한_명을_다른_부대로_옮기고_받는_부대_사기는_그대로다()
        {
            var morale = new SquadMorale(200f, 120f);
            second.Morale = morale;
            int changed = 0;
            run.Changed += () => changed++;

            Assert.IsTrue(run.TryMoveSoldier(a, second, out int to, out int removed));

            Assert.AreSame(second, run.SquadOf(a));
            Assert.AreEqual(1, to);
            Assert.AreEqual(-1, removed);
            CollectionAssert.AreEqual(new RunUnit[] { b }, first.Members);
            Assert.AreSame(morale, second.Morale);
            Assert.AreEqual(120f, second.Morale.Value);
            Assert.Greater(changed, 0);
        }

        [Test]
        public void 받을_부대가_꽉_찼거나_같은_부대면_옮기지_않는다()
        {
            run.TryAssign(run.AddSoldier(melee), second);
            Assert.AreEqual(OrganizeCheck.SquadFull, run.CheckMoveSoldier(a, second));
            Assert.IsFalse(run.TryMoveSoldier(a, second, out _, out _));
            Assert.AreSame(first, run.SquadOf(a));
            Assert.AreEqual(OrganizeCheck.SameSquad, run.CheckMoveSoldier(a, first));
        }

        [Test]
        public void 혼자인_잡졸은_새_부대로_옮겨도_같아서_막는다()
        {
            Assert.AreEqual(OrganizeCheck.SameSquad, run.CheckMoveSoldier(c, null));
        }

        [Test]
        public void 부대가_비면_없어지고_대장은_대기로_가며_뒤_부대_번호가_당겨진다()
        {
            Squad<RunUnit> third = run.Army.TryAddSquad();
            RunSoldier d = run.AddSoldier(melee);
            run.TryAssign(d, third);
            RunHero captain = run.AddHero(knight);
            Assert.IsTrue(run.TrySetCaptain(second, captain));

            Assert.IsTrue(run.TryMoveSoldier(c, third, out int to, out int removed));

            Assert.AreEqual(1, removed, "옮기기 전 2번째 부대가 없어진다.");
            Assert.AreEqual(1, to, "세 번째 부대가 두 번째로 당겨진다.");
            Assert.AreEqual(2, run.Army.Squads.Count);
            Assert.AreSame(third, run.Army.Squads[1]);
            Assert.IsNull(run.LeadingSquadOf(captain));
            CollectionAssert.AreEqual(new[] { captain }, run.ReserveHeroes);
        }

        [Test]
        public void 새_부대로_옮기면_맨_뒤에_부대가_생기고_상한이면_막는다()
        {
            Assert.IsTrue(run.TryMoveSoldier(a, null, out int to, out int removed));
            Assert.AreEqual(2, to);
            Assert.AreEqual(-1, removed);
            Assert.AreEqual(3, run.Army.Squads.Count);
            RunSoldier e = run.AddSoldier(melee);
            run.TryAssign(e, second);
            Assert.AreEqual(OrganizeCheck.SquadFull, run.CheckMoveSoldier(e, null), "부대 수 상한 3");
        }

        [Test]
        public void 대기_영웅만_대장이_되고_원래_대장은_대기로_돌아간다()
        {
            RunHero k = run.AddHero(knight);
            RunHero r = run.AddHero(archer);
            CollectionAssert.AreEqual(new[] { k, r }, run.ReserveHeroes);

            Assert.IsTrue(run.TrySetCaptain(first, k));
            Assert.AreEqual(OrganizeCheck.NotReserve, run.CheckSetCaptain(second, k));
            Assert.IsTrue(run.TrySetCaptain(first, r));
            Assert.AreSame(r, first.Leader);
            CollectionAssert.AreEqual(new[] { k }, run.ReserveHeroes);

            Assert.IsTrue(run.TrySetCaptain(first, null));
            Assert.IsNull(first.Leader);
            Assert.AreEqual(2, run.ReserveHeroes.Count);
        }

        [Test]
        public void 플레이어는_지금은_대장이_될_수_없고_설정을_켜면_된다()
        {
            Assert.AreEqual(OrganizeCheck.PlayerNotAllowed, run.CheckSetCaptain(first, run.Player));
            Assert.IsFalse(run.TrySetCaptain(first, run.Player));
            CollectionAssert.DoesNotContain(run.ReserveHeroes, run.Player);

            run.AllowPlayerCaptain = true;
            Assert.IsTrue(run.TrySetCaptain(first, run.Player));
        }

        [Test]
        public void 전투_입력이_나간_뒤에는_편성을_바꿀_수_없다()
        {
            run.CreateBattleInput(1);
            Assert.AreEqual(OrganizeCheck.BattlePending, run.CheckMoveSoldier(a, second));
            Assert.AreEqual(OrganizeCheck.BattlePending, run.CheckSetCaptain(first, null));
            Assert.IsFalse(run.TryMoveSoldier(a, second, out _, out _));
        }
    }
}
