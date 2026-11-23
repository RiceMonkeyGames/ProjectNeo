using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>D-8·UI-07 부대 편성: 잡졸 옮기기·보관, 빈 부대 유지, 부대 만들기·없애기, 대장 지정(Q-32 임시).</summary>
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

            Assert.IsTrue(run.TryMoveSoldier(a, second, out int to));

            Assert.AreSame(second, run.SquadOf(a));
            Assert.AreEqual(1, to);
            CollectionAssert.AreEqual(new RunUnit[] { b }, first.Members);
            Assert.AreSame(morale, second.Morale);
            Assert.AreEqual(120f, second.Morale.Value);
            Assert.Greater(changed, 0);
        }

        [Test]
        public void 받을_부대가_꽉_찼거나_같은_부대거나_부대가_없으면_옮기지_않는다()
        {
            run.TryAssign(run.AddSoldier(melee), second);
            Assert.AreEqual(OrganizeCheck.SquadFull, run.CheckMoveSoldier(a, second));
            Assert.IsFalse(run.TryMoveSoldier(a, second, out _));
            Assert.AreSame(first, run.SquadOf(a));
            Assert.AreEqual(OrganizeCheck.SameSquad, run.CheckMoveSoldier(a, first));
            Assert.AreEqual(OrganizeCheck.NotInRun, run.CheckMoveSoldier(a, null));
        }

        [Test]
        public void 마지막_잡졸을_옮겨도_빈_부대와_대장은_남는다()
        {
            RunHero captain = run.AddHero(knight);
            Assert.IsTrue(run.TrySetCaptain(second, captain));

            Assert.IsTrue(run.TryStoreSoldier(c), "두 번째 부대의 마지막 잡졸을 보관으로 돌린다.");
            Assert.IsTrue(run.TryStoreSoldier(a));
            Assert.IsTrue(run.TryStoreSoldier(b));

            Assert.AreEqual(2, run.Army.Squads.Count, "빈 부대는 없어지지 않는다(UI-07).");
            Assert.AreSame(captain, second.Leader);
            CollectionAssert.IsEmpty(first.Members);
        }

        [Test]
        public void 잡졸을_보관으로_돌리고_다시_부대에_넣는다()
        {
            Assert.IsTrue(run.TryStoreSoldier(c));
            Assert.IsNull(run.SquadOf(c));
            CollectionAssert.AreEqual(new[] { c }, run.StoredSoldiers);
            CollectionAssert.IsEmpty(second.Members);
            Assert.AreEqual(OrganizeCheck.SameSquad, run.CheckStoreSoldier(c), "이미 보관 중이다.");

            Assert.IsTrue(run.TryMoveSoldier(c, second, out int to));
            Assert.AreEqual(1, to);
            CollectionAssert.IsEmpty(run.StoredSoldiers);
        }

        [Test]
        public void 새_부대는_맨_뒤에_빈_채로_생기고_상한이면_막는다()
        {
            Squad<RunUnit> added = run.TryAddSquad();
            Assert.IsNotNull(added);
            Assert.AreSame(added, run.Army.Squads[2]);
            CollectionAssert.IsEmpty(added.Members);
            Assert.AreEqual(OrganizeCheck.SquadFull, run.CheckAddSquad(), "부대 수 상한 3");
            Assert.IsNull(run.TryAddSquad());
        }

        [Test]
        public void 부대를_없애면_잡졸은_보관으로_대장은_대기로_가고_뒤_부대가_당겨진다()
        {
            Squad<RunUnit> third = run.TryAddSquad();
            RunHero captain = run.AddHero(knight);
            Assert.IsTrue(run.TrySetCaptain(first, captain));

            Assert.IsTrue(run.TryRemoveSquad(first));

            Assert.AreEqual(2, run.Army.Squads.Count);
            Assert.AreSame(second, run.Army.Squads[0]);
            Assert.AreSame(third, run.Army.Squads[1]);
            CollectionAssert.AreEquivalent(new[] { a, b }, run.StoredSoldiers);
            CollectionAssert.AreEqual(new[] { captain }, run.ReserveHeroes);
            Assert.AreEqual(3, run.Soldiers.Count, "잡졸은 런에 남는다.");
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
            Assert.IsFalse(run.TryMoveSoldier(a, second, out _));
            Assert.AreEqual(OrganizeCheck.BattlePending, run.CheckStoreSoldier(a));
            Assert.AreEqual(OrganizeCheck.BattlePending, run.CheckAddSquad());
            Assert.AreEqual(OrganizeCheck.BattlePending, run.CheckRemoveSquad(first));
        }
    }
}
