using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    public class RunUnitBindingTests
    {
        // 씬 유닛 대신 쓰는 이름표.
        sealed class Unit
        {
            public readonly string Name;
            public bool Dead;
            public Unit(string name) => Name = name;
        }

        HeroDefinition hero;
        SoldierDefinition melee;
        SoldierDefinition archer;
        Unit player;
        Unit[] first;
        Unit[] second;

        [SetUp]
        public void SetUp()
        {
            hero = HeroDefinition.Create("player");
            melee = SoldierDefinition.Create("melee", new Cost(0));
            archer = SoldierDefinition.Create("archer", new Cost(0));
            player = new Unit("player");
            first = new[] { new Unit("a"), new Unit("b") };
            second = new[] { new Unit("c") };
        }

        static List<KeyValuePair<Unit, SoldierDefinition>> Line(SoldierDefinition definition, params Unit[] units)
        {
            var line = new List<KeyValuePair<Unit, SoldierDefinition>>();
            foreach (Unit unit in units) line.Add(new KeyValuePair<Unit, SoldierDefinition>(unit, definition));
            return line;
        }

        RunUnitBinding<Unit> Bind(int maxSquads = 4, int maxMembers = 10) =>
            RunUnitBinding<Unit>.Create(SquadConfig.Create(maxSquads, maxMembers), hero, player,
                new List<IReadOnlyList<KeyValuePair<Unit, SoldierDefinition>>> { Line(melee, first), Line(archer, second) });

        [Test]
        public void 씬_편성대로_런_데이터를_만들고_유닛마다_번호를_붙인다()
        {
            RunUnitBinding<Unit> binding = Bind();
            RunState state = binding.State;
            Assert.AreSame(hero, state.Player.Definition);
            Assert.AreEqual(state.Player.Id, binding.IdOf(player));
            Assert.AreEqual(2, state.Army.Squads.Count);
            Assert.AreEqual(2, state.Army.Squads[0].Members.Count);
            Assert.AreEqual(1, state.Army.Squads[1].Members.Count);

            var soldier = (RunSoldier)state.Find(binding.IdOf(second[0]));
            Assert.AreSame(archer, soldier.Definition);
            Assert.AreSame(state.Army.Squads[1], state.SquadOf(soldier));
            Assert.AreSame(first[1], binding.ObjectOf(binding.IdOf(first[1])));
            Assert.AreEqual(0, binding.IdOf(new Unit("stranger")));
            Assert.AreEqual(4, binding.ObjectsInRun().Count);
        }

        [Test]
        public void 정의가_없거나_상한을_넘거나_같은_유닛이_두_번이면_만들지_않는다()
        {
            var missing = new List<IReadOnlyList<KeyValuePair<Unit, SoldierDefinition>>> { Line(null, first) };
            Assert.Throws<System.ArgumentException>(() =>
                RunUnitBinding<Unit>.Create(SquadConfig.Create(2, 10), hero, player, missing));
            Assert.Throws<System.ArgumentException>(() => Bind(maxSquads: 1));
            Assert.Throws<System.ArgumentException>(() => Bind(maxMembers: 1));
            var twice = new List<IReadOnlyList<KeyValuePair<Unit, SoldierDefinition>>> { Line(melee, first[0], first[0]) };
            Assert.Throws<System.ArgumentException>(() =>
                RunUnitBinding<Unit>.Create(SquadConfig.Create(2, 10), hero, player, twice));
        }

        [Test]
        public void 결과는_씬_생사를_담고_런에서_빠진_유닛은_다시_담지_않는다()
        {
            RunUnitBinding<Unit> binding = Bind();
            RunState state = binding.State;
            first[0].Dead = true;
            BattleInput input = state.CreateBattleInput(1);
            BattleResult result = binding.CreateResult(input.BattleNumber, BattleOutcome.Victory,
                BattleEndReason.EnemiesDefeated, unit => unit.Dead, new CastleState(),
                new Dictionary<string, int> { { "enemy_ladder", 3 }, { "", 1 }, { "enemy_archer", 0 } });

            Assert.AreEqual(UnitFate.Dead, result.FateOf(binding.IdOf(first[0])));
            Assert.AreEqual(UnitFate.Alive, result.FateOf(binding.IdOf(first[1])));
            Assert.AreEqual(4, result.Fates.Count);
            Assert.AreEqual(1, result.EnemiesKilled.Count, "빈 ID와 0 이하는 빼고 담는다.");
            Assert.AreEqual(3, result.EnemiesKilled["enemy_ladder"]);
            Assert.AreEqual(BattleResultIssue.None, state.Apply(result));
            Assert.AreEqual(3, binding.ObjectsInRun().Count);
            CollectionAssert.DoesNotContain(binding.ObjectsInRun(), first[0]);

            BattleInput next = state.CreateBattleInput(2);
            BattleResult again = binding.CreateResult(next.BattleNumber, BattleOutcome.Victory,
                BattleEndReason.EnemiesDefeated, unit => unit.Dead, null);
            Assert.AreEqual(3, again.Fates.Count, "앞 전투에서 죽은 유닛은 다시 넣지 않아 모르는 유닛 오류가 나지 않는다.");
            Assert.AreEqual(BattleResultIssue.None, state.Apply(again));
        }

        [Test]
        public void 흐름은_데이터_없이_시작한_런에만_한_번_데이터를_붙인다()
        {
            var flow = new GameFlow();
            RunState state = Bind().State;
            Assert.IsFalse(flow.AttachRunState(state), "런이 없으면 붙이지 않는다.");
            flow.StartRun();
            Assert.IsTrue(flow.AttachRunState(state));
            Assert.AreSame(state, flow.Run.State);
            Assert.IsFalse(flow.AttachRunState(Bind().State));
            Assert.AreSame(state, flow.Run.State);
        }
    }
}
