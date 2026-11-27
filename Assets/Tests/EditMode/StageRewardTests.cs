using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stages;

namespace ProjectNeo.Tests
{
    public class StageRewardTests
    {
        // 정해진 값을 차례로 돌려주는 랜덤.
        sealed class FixedRandom : IRandomSource
        {
            readonly Queue<float> values;
            public FixedRandom(params float[] values) => this.values = new Queue<float>(values);
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => values.Count > 0 ? values.Dequeue() : 0f;
        }

        MaterialDefinition bone;
        MaterialDefinition hide;
        ItemDefinition sword;
        ItemDefinition potion;
        StageRewardTable table;

        [SetUp]
        public void SetUp()
        {
            bone = MaterialDefinition.Create("bone");
            hide = MaterialDefinition.Create("hide");
            sword = ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40));
            potion = ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10));
            // 처치 보상은 적 종류(CONTENT-04)에 있다.
            table = StageRewardTable.Create(50, 10,
                EnemyCatalog.Create(
                    EnemyDefinition.Create("enemy_ladder", "Ladder", EnemyRole.GroundLadder, null, 3, new EnemyMaterialDrop(bone, 1)),
                    EnemyDefinition.Create("enemy_gate_breaker", "Gate Breaker", EnemyRole.GateBreaker, null, 5, new EnemyMaterialDrop(hide, 2))),
                0.6f, 1,
                new[] { new WeightedEntry<ItemDefinition>(sword, 1f), new WeightedEntry<ItemDefinition>(potion, 3f) },
                40, 10, 1, 2);
        }

        static BattleResult Victory(int battle = 1)
        {
            var result = new BattleResult(battle, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.AddKill("enemy_ladder", 4);
            result.AddKill("enemy_gate_breaker", 2);
            result.AddKill("enemy_unknown", 9);
            return result;
        }

        [Test]
        public void RunEndUnlockPoints_CountStagesClearedAndBossesKilled()
        {
            // 표: 스테이지당 1, 보스당 2.
            Assert.AreEqual(0, table.RunEndUnlockPoints(0, 0), "1스테이지에서 지면 0");
            Assert.AreEqual(3, table.RunEndUnlockPoints(3, 0));
            Assert.AreEqual(3 + 2 * 2, table.RunEndUnlockPoints(3, 2));
            Assert.AreEqual(0, table.RunEndUnlockPoints(-1, -1), "음수는 0으로 본다");
        }

        [Test]
        public void 골드는_기본과_스테이지_가산과_처치_골드의_합이고_재료는_종류별로_쌓인다()
        {
            StageReward reward = StageReward.Roll(table, 3, Victory(), new FixedRandom(0.99f));
            Assert.AreEqual(50 + 10 * 2 + 3 * 4 + 5 * 2, reward.Gold, "표에 없는 적은 보상이 없다.");
            Assert.AreEqual(4, reward.Materials[bone]);
            Assert.AreEqual(4, reward.Materials[hide]);
            Assert.AreEqual(2, reward.Materials.Count);
            Assert.AreEqual(40 + 10 * 2, reward.CommanderXp);
            Assert.AreEqual(0, reward.Items.Count, "0.99는 확률 0.6을 넘어 아이템이 없다.");
            Assert.AreEqual(3, reward.StageNumber);
            Assert.AreEqual(1, reward.BattleNumber);
        }

        [Test]
        public void 적_종류의_재료_여러_줄은_처치_수만큼_모두_쌓이고_목록이_없으면_처치_보상이_없다()
        {
            var feather = MaterialDefinition.Create("feather");
            var mixed = StageRewardTable.Create(0, 0, EnemyCatalog.Create(EnemyDefinition.Create("enemy_flyer", "Flyer",
                EnemyRole.Flyer, null, 2, new EnemyMaterialDrop(feather, 3), new EnemyMaterialDrop(bone, 1))));
            var result = new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated);
            result.AddKill("enemy_flyer", 2);
            StageReward reward = StageReward.Roll(mixed, 1, result, new FixedRandom(0.99f));
            Assert.AreEqual(2 * 2, reward.Gold);
            Assert.AreEqual(6, reward.Materials[feather]);
            Assert.AreEqual(2, reward.Materials[bone]);

            StageReward none = StageReward.Roll(StageRewardTable.Create(0, 0), 1, result, new FixedRandom(0.99f));
            Assert.AreEqual(0, none.Gold);
            Assert.AreEqual(0, none.Materials.Count);
        }

        [Test]
        public void 아이템은_확률을_넘기면_가중치_풀에서_뽑는다()
        {
            // 첫 값은 확률 검사(0.1 < 0.6), 둘째 값은 풀 추첨(0.1 × 전체 가중치 4 = 0.4 → 첫 항목).
            StageReward first = StageReward.Roll(table, 1, Victory(), new FixedRandom(0.1f, 0.1f));
            CollectionAssert.AreEqual(new[] { sword }, first.Items);
            StageReward second = StageReward.Roll(table, 1, Victory(), new FixedRandom(0.1f, 0.9f));
            CollectionAssert.AreEqual(new[] { potion }, second.Items);
        }

        [Test]
        public void 같은_시드면_같은_보상이고_패배에는_보상이_없다()
        {
            StageReward a = StageReward.Roll(table, 2, Victory(), new SeededRandom(1234));
            StageReward b = StageReward.Roll(table, 2, Victory(), new SeededRandom(1234));
            Assert.AreEqual(a.Gold, b.Gold);
            CollectionAssert.AreEqual(a.Items, b.Items);
            var defeat = new BattleResult(1, BattleOutcome.Defeat, BattleEndReason.PlayerDied);
            Assert.Throws<System.ArgumentException>(() => StageReward.Roll(table, 1, defeat, new SeededRandom(1)));
        }

        [Test]
        public void 런은_마지막_승리의_보상을_한_번만_지갑과_보관함에_넣는다()
        {
            var state = new RunState(SquadConfig.Create(2, 4), 77);
            state.AddHero(HeroDefinition.Create("player"), true);
            BattleInput input = state.CreateBattleInput(1);
            BattleResult result = Victory(input.BattleNumber);
            StageReward reward = StageReward.Roll(table, 1, result, new FixedRandom(0.1f, 0.9f));
            Assert.IsFalse(state.ApplyReward(reward), "결과를 반영하기 전에는 지급하지 않는다.");

            Assert.AreEqual(BattleResultIssue.None, state.Apply(result));
            Assert.IsTrue(state.ApplyReward(reward));
            Assert.AreEqual(reward.Gold, state.Wallet.Gold);
            Assert.AreEqual(4, state.Wallet.GetMaterial(bone));
            Assert.AreEqual(1, state.Inventory.CountOf(potion));
            Assert.AreEqual(1, state.LastRewardedBattle);
            Assert.AreSame(reward, state.LastReward);

            Assert.IsFalse(state.ApplyReward(reward), "같은 전투의 보상은 두 번 받지 않는다.");
            Assert.AreEqual(reward.Gold, state.Wallet.Gold);
        }

        [Test]
        public void 패배를_반영한_뒤나_다른_전투_번호의_보상은_지급하지_않는다()
        {
            var state = new RunState(SquadConfig.Create(2, 4));
            state.AddHero(HeroDefinition.Create("player"), true);
            BattleInput input = state.CreateBattleInput(1);
            StageReward stale = StageReward.Roll(table, 1, Victory(5), new FixedRandom(0.99f));
            state.Apply(new BattleResult(input.BattleNumber, BattleOutcome.Defeat, BattleEndReason.PlayerDied));
            Assert.IsFalse(state.ApplyReward(StageReward.Roll(table, 1, Victory(1), new FixedRandom(0.99f))));
            Assert.IsFalse(state.ApplyReward(stale));
            Assert.AreEqual(0, state.Wallet.Gold);
        }

        [Test]
        public void 전투별_시드는_런과_전투_번호로_정해진다()
        {
            var a = new RunState(SquadConfig.Create(1, 1), 42);
            var b = new RunState(SquadConfig.Create(1, 1), 42);
            Assert.AreEqual(a.SeedFor(3), b.SeedFor(3));
            Assert.AreNotEqual(a.SeedFor(3), a.SeedFor(4));
        }
    }
}
