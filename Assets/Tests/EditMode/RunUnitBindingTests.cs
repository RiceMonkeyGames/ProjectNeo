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
        public void 도망친_유닛은_Fled로_남고_죽음이_먼저이며_적_퇴각은_처치와_따로_센다()
        {
            // B-8.2 / Q-44: 사기로 도망친 아군은 런에 남고, 도망치다 죽으면 사망이다.
            RunUnitBinding<Unit> binding = Bind();
            RunState state = binding.State;
            first[0].Dead = true;
            var fled = new HashSet<Unit> { first[0], first[1] };
            BattleInput input = state.CreateBattleInput(1);
            BattleResult result = binding.CreateResult(input.BattleNumber, BattleOutcome.Victory,
                BattleEndReason.EnemiesRetreated, unit => unit.Dead, new CastleState(),
                new Dictionary<string, int> { { "enemy_ladder", 2 } }, fled.Contains,
                new Dictionary<string, int> { { "enemy_archer", 3 }, { "", 1 }, { "enemy_ladder", 0 } });

            Assert.AreEqual(UnitFate.Dead, result.FateOf(binding.IdOf(first[0])), "도망치다 죽으면 사망이다.");
            Assert.AreEqual(UnitFate.Fled, result.FateOf(binding.IdOf(first[1])));
            Assert.AreEqual(UnitFate.Alive, result.FateOf(binding.IdOf(second[0])));
            Assert.AreEqual(2, result.EnemiesKilled["enemy_ladder"]);
            Assert.AreEqual(1, result.EnemiesRetreated.Count, "빈 ID와 0 이하는 빼고 담는다.");
            Assert.AreEqual(3, result.EnemiesRetreated["enemy_archer"]);
            Assert.IsFalse(result.EnemiesKilled.ContainsKey("enemy_archer"), "퇴각한 적은 처치 수에 들지 않는다.");

            Assert.AreEqual(BattleResultIssue.None, state.Apply(result));
            CollectionAssert.Contains(binding.ObjectsInRun(), first[1], "도망친 유닛은 런에 남아 다음 전투에 돌아온다.");
            Assert.AreSame(state.Army.Squads[0], state.SquadOf((RunSoldier)state.Find(binding.IdOf(first[1]))));
        }

        [Test]
        public void 런_부대는_사기를_들고_다니며_새_부대는_비어_있다()
        {
            // Q-44: 사기는 런 부대에 둔다. 처음 쓰는 쪽이 시작값을 넣는다.
            RunState state = Bind().State;
            Assert.IsNull(state.Army.Squads[0].Morale);
            var settings = ProjectNeo.Morale.MoraleSettings.Create();
            state.Army.Squads[0].Morale = settings.CreateSquadMorale();
            state.Army.Squads[0].Morale.Add(-70f);
            BattleInput input = state.CreateBattleInput(1);
            Assert.AreEqual(BattleResultIssue.None, state.Apply(new BattleResult(input.BattleNumber,
                BattleOutcome.Victory, BattleEndReason.EnemiesDefeated)));
            Assert.AreEqual(80f, state.Army.Squads[0].Morale.Value, 0.001f, "전투 결과를 반영해도 부대 사기는 그대로 이어진다.");
            UnityEngine.Object.DestroyImmediate(settings);
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
