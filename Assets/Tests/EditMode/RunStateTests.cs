using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class RunStateTests
    {
        SoldierDefinition melee;
        SoldierDefinition archer;
        HeroDefinition playerHero;
        MaterialDefinition bone;

        [SetUp]
        public void SetUp()
        {
            melee = SoldierDefinition.Create("melee", new Cost(0));
            archer = SoldierDefinition.Create("archer", new Cost(0));
            playerHero = HeroDefinition.Create("player");
            bone = MaterialDefinition.Create("bone");
        }

        RunState NewRun(int maxSquads = 4, int maxMembers = 10)
        {
            var config = RunConfig.Create(SquadConfig.Create(maxSquads, maxMembers), playerHero, 120,
                new[] { new MaterialAmount(bone, 3) },
                new RunSquadSetup(melee, melee), new RunSquadSetup(archer));
            return RunState.CreateNew(config);
        }

        [Test]
        public void 새_런은_설정의_재화_플레이어_부대로_시작한다()
        {
            RunState run = NewRun();
            Assert.AreEqual(120, run.Wallet.Gold);
            Assert.AreEqual(3, run.Wallet.GetMaterial(bone));
            Assert.IsNotNull(run.Player);
            Assert.IsTrue(run.Player.IsPlayer);
            Assert.AreSame(playerHero, run.Player.Definition);
            Assert.AreEqual(2, run.Army.Squads.Count);
            Assert.AreEqual(2, run.Army.Squads[0].Members.Count);
            Assert.AreEqual(1, run.Army.Squads[1].Members.Count);
            Assert.AreEqual(3, run.Soldiers.Count);
            Assert.AreEqual(0, run.BattlesFought);
            Assert.IsFalse(run.HasPendingBattle);
        }

        [Test]
        public void 런_유닛_번호는_모두_다르고_찾을_수_있다()
        {
            RunState run = NewRun();
            var seen = new HashSet<int>();
            Assert.IsTrue(seen.Add(run.Player.Id));
            foreach (RunSoldier soldier in run.Soldiers)
            {
                Assert.IsTrue(seen.Add(soldier.Id), "번호 중복");
                Assert.AreSame(soldier, run.Find(soldier.Id));
            }
            Assert.IsNull(run.Find(9999));
        }

        [Test]
        public void 시작_부대가_상한을_넘으면_설정_오류다()
        {
            var tooManySquads = RunConfig.Create(SquadConfig.Create(1, 10), playerHero, 0, null,
                new RunSquadSetup(melee), new RunSquadSetup(melee));
            Assert.Throws<System.ArgumentException>(() => RunState.CreateNew(tooManySquads));

            var tooManyMembers = RunConfig.Create(SquadConfig.Create(2, 1), playerHero, 0, null,
                new RunSquadSetup(melee, melee));
            Assert.Throws<System.ArgumentException>(() => RunState.CreateNew(tooManyMembers));

            var noPlayer = RunConfig.Create(SquadConfig.Create(2, 2), null, 0);
            Assert.Throws<System.ArgumentException>(() => RunState.CreateNew(noPlayer));
        }

        [Test]
        public void 잡졸은_한_부대에만_있고_옮기면_이전_부대에서_빠진다()
        {
            RunState run = NewRun();
            Squad<RunUnit> first = run.Army.Squads[0];
            Squad<RunUnit> second = run.Army.Squads[1];
            var soldier = (RunSoldier)first.Members[0];

            Assert.IsTrue(run.TryAssign(soldier, second));
            Assert.AreSame(second, run.SquadOf(soldier));
            Assert.AreEqual(1, first.Members.Count);
            Assert.AreEqual(2, second.Members.Count);

            Assert.IsTrue(run.Unassign(soldier));
            Assert.IsNull(run.SquadOf(soldier));
            Assert.AreSame(soldier, run.Find(soldier.Id), "부대에서 빠져도 런에는 남는다.");
        }

        [Test]
        public void 꽉_찬_부대나_다른_런의_잡졸은_넣지_않고_상태가_그대로다()
        {
            RunState run = NewRun(maxSquads: 2, maxMembers: 2);
            Squad<RunUnit> full = run.Army.Squads[0];
            Squad<RunUnit> other = run.Army.Squads[1];
            var mover = (RunSoldier)other.Members[0];

            Assert.IsFalse(run.TryAssign(mover, full));
            Assert.AreSame(other, run.SquadOf(mover));
            Assert.AreEqual(2, full.Members.Count);

            RunState stranger = NewRun();
            Assert.IsFalse(run.TryAssign(stranger.Soldiers[0], other));
            Assert.IsFalse(run.TryAssign(mover, stranger.Army.Squads[0]));
        }

        [Test]
        public void 영웅은_한_부대만_맡는다()
        {
            RunState run = NewRun();
            RunHero hero = run.AddHero(HeroDefinition.Create("knight"));
            Assert.IsTrue(run.TrySetLeader(run.Army.Squads[0], hero));
            Assert.IsFalse(run.TrySetLeader(run.Army.Squads[1], hero));
            Assert.IsNull(run.Army.Squads[1].Leader);
            Assert.IsTrue(run.TrySetLeader(run.Army.Squads[0], null));
            Assert.IsTrue(run.TrySetLeader(run.Army.Squads[1], hero));
        }

        [Test]
        public void 플레이어_영웅은_한_명뿐이다()
        {
            RunState run = NewRun();
            Assert.Throws<System.InvalidOperationException>(() => run.AddHero(HeroDefinition.Create("other"), true));
        }

        [Test]
        public void 전투_입력은_부대_구성과_성_복사본을_담는다()
        {
            RunState run = NewRun();
            RunHero hero = run.AddHero(HeroDefinition.Create("knight"));
            run.TrySetLeader(run.Army.Squads[1], hero);

            BattleInput input = run.CreateBattleInput(3);
            Assert.AreEqual(1, input.BattleNumber);
            Assert.AreEqual(3, input.StageNumber);
            Assert.AreSame(run.Player, input.Player);
            Assert.AreEqual(2, input.Squads.Count);
            Assert.AreEqual(2, input.Squads[0].Members.Count);
            Assert.IsNull(input.Squads[0].Leader);
            Assert.AreSame(hero, input.Squads[1].Leader);
            Assert.AreNotSame(run.Castle, input.Castle);
            Assert.IsTrue(run.HasPendingBattle);
            Assert.AreEqual(1, run.CreateBattleInput(3).BattleNumber, "결과 전에는 같은 전투 번호다.");
        }

        [Test]
        public void 죽은_잡졸은_사라지고_도망친_잡졸은_남는다()
        {
            RunState run = NewRun();
            BattleInput input = run.CreateBattleInput(1);
            RunSoldier dead = input.Squads[0].Members[0];
            RunSoldier fled = input.Squads[0].Members[1];
            RunSoldier untouched = input.Squads[1].Members[0];

            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.SetFate(dead.Id, UnitFate.Dead);
            result.SetFate(fled.Id, UnitFate.Fled);

            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
            Assert.IsNull(run.Find(dead.Id));
            Assert.IsNull(run.SquadOf(dead));
            CollectionAssert.DoesNotContain(run.Soldiers, dead);
            Assert.AreSame(fled, run.Find(fled.Id));
            Assert.AreSame(run.Army.Squads[0], run.SquadOf(fled), "도망친 잡졸은 부대에 남아 다음 전투에 돌아온다.");
            Assert.AreSame(run.Army.Squads[1], run.SquadOf(untouched));
            Assert.AreEqual(2, run.Soldiers.Count);
            Assert.AreEqual(1, run.BattlesFought);
            Assert.AreSame(result, run.LastBattleResult);
            Assert.IsFalse(run.HasPendingBattle);
        }

        [Test]
        public void 죽은_영웅은_영구_사망_목록으로_가고_부대장에서_빠지며_장비는_보관함으로_돌아온다()
        {
            RunState run = NewRun();
            RunHero hero = run.AddHero(HeroDefinition.Create("knight"));
            ItemInstance sword = run.Inventory.Add(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(10)));
            Assert.IsTrue(hero.Character.TryEquip(sword, run.Inventory));
            run.TrySetLeader(run.Army.Squads[0], hero);

            BattleInput input = run.CreateBattleInput(1);
            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.SetFate(hero.Id, UnitFate.Dead);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));

            Assert.IsNull(run.Find(hero.Id));
            CollectionAssert.DoesNotContain(run.Heroes, hero);
            CollectionAssert.Contains(run.FallenHeroes, hero);
            Assert.IsNull(run.Army.Squads[0].Leader);
            CollectionAssert.IsEmpty(hero.Character.Equipment, "Q-31: 죽은 영웅은 장비를 들고 가지 않는다.");
            CollectionAssert.Contains(run.Inventory.Items, sword, "Q-31: 장비는 보관함으로 돌아와 다시 낄 수 있다.");

            RunHero other = run.AddHero(HeroDefinition.Create("archer"));
            Assert.IsTrue(run.TryEquip(other, sword), "돌아온 장비는 다른 영웅이 낄 수 있다.");
        }

        [Test]
        public void 같은_결과는_한_번만_반영된다()
        {
            RunState run = NewRun();
            BattleInput input = run.CreateBattleInput(1);
            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.SetFate(input.Squads[0].Members[0].Id, UnitFate.Dead);

            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
            Assert.AreEqual(BattleResultIssue.NoBattlePending, run.Apply(result));
            Assert.AreEqual(2, run.Soldiers.Count);
            Assert.AreEqual(1, run.BattlesFought);

            run.CreateBattleInput(2);
            Assert.AreEqual(BattleResultIssue.WrongBattle, run.Apply(result), "지난 전투의 결과는 다음 전투에 반영하지 않는다.");
            Assert.AreEqual(1, run.BattlesFought);
        }

        [Test]
        public void 입력_없이_온_결과나_모르는_유닛이_있는_결과는_아무것도_바꾸지_않는다()
        {
            RunState run = NewRun();
            var early = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            Assert.AreEqual(BattleResultIssue.NoBattlePending, run.Apply(early));

            BattleInput input = run.CreateBattleInput(1);
            var result = new BattleResult(input.BattleNumber, BattleOutcome.Defeat, BattleEndReason.PlayerDied);
            result.SetFate(input.Squads[0].Members[0].Id, UnitFate.Dead);
            result.SetFate(9999, UnitFate.Dead);
            result.CastleAfter = new CastleState();
            CastleState before = run.Castle;

            Assert.AreEqual(BattleResultIssue.UnknownUnit, run.Apply(result));
            Assert.AreEqual(3, run.Soldiers.Count);
            Assert.AreSame(before, run.Castle);
            Assert.AreEqual(0, run.BattlesFought);
            Assert.IsNull(run.LastBattleResult);
            Assert.IsTrue(run.HasPendingBattle, "고친 결과를 다시 보낼 수 있다.");
        }

        [Test]
        public void 전투_후_성_기록으로_바뀌고_처치_수를_남긴다()
        {
            RunState run = NewRun();
            BattleInput input = run.CreateBattleInput(1);
            var wall = BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, 3f, 100f);
            var layout = new BuildLayout(BuildGridDefinition.Create(4, 4, 3f, 2));
            layout.TryPlace(wall, new Vector2Int(1, 1), 0, out _);
            CastleState after = CastleState.Capture(layout, null, _ => 40f);

            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesRetreated);
            result.CastleAfter = after;
            result.AddKill("enemy_ladder", 2);
            result.AddKill("enemy_ladder");
            result.AddKill("enemy_archer");
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));

            Assert.AreEqual(1, run.Castle.Pieces.Count);
            Assert.AreEqual(40f, run.Castle.Pieces[0].Health);
            Assert.AreNotSame(after, run.Castle, "전투가 넘긴 기록을 그대로 들지 않는다.");
            Assert.AreEqual(3, run.LastBattleResult.EnemiesKilled["enemy_ladder"]);
            Assert.AreEqual(1, run.LastBattleResult.EnemiesKilled["enemy_archer"]);
        }

        [Test]
        public void 흐름은_런_데이터를_스테이지_내내_들고_타이틀에서_버린다()
        {
            var flow = new GameFlow();
            RunState run = NewRun();
            Assert.IsTrue(flow.StartRun(run));
            Assert.AreSame(run, flow.Run.State);

            flow.StartBattle();
            BattleInput input = run.CreateBattleInput(flow.Run.StageNumber);
            var result = new BattleResult(input.BattleNumber, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.SetFate(input.Squads[0].Members[0].Id, UnitFate.Dead);
            Assert.IsTrue(flow.FinishBattle(result));
            Assert.AreEqual(GamePhase.Reward, flow.Phase);
            Assert.AreEqual(2, run.Soldiers.Count);

            flow.ContinueToNextStage();
            Assert.AreSame(run, flow.Run.State, "다음 스테이지에도 같은 런 데이터다.");
            flow.StartBattle();
            flow.FinishBattle(BattleOutcome.Defeat);
            flow.ReturnToTitle();
            Assert.IsNull(flow.Run);
        }

        [Test]
        public void 흐름은_반영할_수_없는_결과로_단계를_넘기지_않는다()
        {
            var flow = new GameFlow();
            RunState run = NewRun();
            flow.StartRun(run);
            flow.StartBattle();
            run.CreateBattleInput(1);
            var wrong = new BattleResult(5, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);

            Assert.IsFalse(flow.FinishBattle(wrong));
            Assert.AreEqual(GamePhase.Battle, flow.Phase);
            Assert.IsNull(flow.LastOutcome);
        }

        [Test]
        public void 런_데이터가_없는_흐름도_기존처럼_결과로_넘어간다()
        {
            var flow = new GameFlow();
            flow.StartRun();
            Assert.IsNull(flow.Run.State);
            flow.StartBattle();
            Assert.IsTrue(flow.FinishBattle(new BattleResult(1, BattleOutcome.Defeat, BattleEndReason.PlayerDied)));
            Assert.AreEqual(GamePhase.RunResult, flow.Phase);
        }
    }
}
