using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Core;
using ProjectNeo.Morale;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>B-8.1 사기 규칙(Q-04·Q-38·Q-39). 기본 수치는 MoraleSettings 기본값.</summary>
    public class MoraleRulesTests
    {
        /// <summary>정해 둔 값을 차례로 돌려주는 랜덤. 비면 0을 준다.</summary>
        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<float> floats = new Queue<float>();
            readonly Queue<int> ints = new Queue<int>();
            public int FloatCalls { get; private set; }

            public ScriptedRandom Floats(params float[] values)
            {
                foreach (float value in values) floats.Enqueue(value);
                return this;
            }

            public ScriptedRandom Ints(params int[] values)
            {
                foreach (int value in values) ints.Enqueue(value);
                return this;
            }

            public int NextInt(int maxExclusive) => ints.Count > 0 ? ints.Dequeue() % maxExclusive : 0;

            public float NextFloat()
            {
                FloatCalls++;
                return floats.Count > 0 ? floats.Dequeue() : 0f;
            }
        }

        MoraleSettings settings;
        readonly List<BattleSquadMorale> battles = new List<BattleSquadMorale>();

        [SetUp]
        public void SetUp() => settings = MoraleSettings.Create();

        [TearDown]
        public void TearDown()
        {
            foreach (BattleSquadMorale battle in battles) battle.Dispose();
            battles.Clear();
            Object.DestroyImmediate(settings);
        }

        BattleSquadMorale Battle(float start, MoraleSide side = MoraleSide.Ally, int members = 10,
            IRandomSource random = null)
        {
            var battle = new BattleSquadMorale(new SquadMorale(settings.MaxMorale, start), side, members,
                settings, random ?? new ScriptedRandom());
            battles.Add(battle);
            return battle;
        }

        // --- 시작과 감소 ---

        [Test]
        public void NewSquad_StartsAt150_OutOf200()
        {
            SquadMorale morale = settings.CreateSquadMorale();
            Assert.AreEqual(200f, morale.Max);
            Assert.AreEqual(150f, morale.Value);
        }

        [Test]
        public void MemberDeath_LowersMorale_ByShareOfStartingMembers()
        {
            BattleSquadMorale ten = Battle(150f, members: 10);
            BattleSquadMorale five = Battle(150f, members: 5);

            ten.OnMemberDied();
            five.OnMemberDied();

            Assert.AreEqual(140f, ten.Morale.Value, 0.001f);
            Assert.AreEqual(130f, five.Morale.Value, 0.001f);
            Assert.AreEqual(1, ten.LostMembers);
        }

        [Test]
        public void CaptainDeath_IsTheLargestFixedLoss()
        {
            Assert.Greater(settings.CaptainDeathLoss, settings.GateDestroyedLoss);
            Assert.Greater(settings.CaptainDeathLoss, settings.NearbyWallCollapseLoss);
            Assert.Greater(settings.CaptainDeathLoss, settings.NearbySquadRoutLoss);

            BattleSquadMorale battle = Battle(150f);
            battle.OnCaptainDied();
            Assert.AreEqual(150f - settings.CaptainDeathLoss, battle.Morale.Value, 0.001f);
        }

        [Test]
        public void GateAndWallLosses_ApplyToAlliesOnly()
        {
            BattleSquadMorale ally = Battle(150f);
            BattleSquadMorale enemy = Battle(150f, MoraleSide.Enemy);

            ally.OnGateDestroyed();
            ally.OnNearbyWallCollapsed();
            enemy.OnGateDestroyed();
            enemy.OnNearbyWallCollapsed();

            Assert.AreEqual(150f - settings.GateDestroyedLoss - settings.NearbyWallCollapseLoss, ally.Morale.Value, 0.001f);
            Assert.AreEqual(150f, enemy.Morale.Value);
        }

        [Test]
        public void NearbyRout_LowersBothSides()
        {
            BattleSquadMorale ally = Battle(150f);
            BattleSquadMorale enemy = Battle(150f, MoraleSide.Enemy);

            ally.OnNearbySquadRouted();
            enemy.OnNearbySquadRouted();

            Assert.AreEqual(150f - settings.NearbySquadRoutLoss, ally.Morale.Value, 0.001f);
            Assert.AreEqual(150f - settings.NearbySquadRoutLoss, enemy.Morale.Value, 0.001f);
        }

        // --- 한계 판정 ---

        [Test]
        public void BreakingPoint_Collapses_WhenRollMissesAwakenChance()
        {
            var random = new ScriptedRandom().Floats(0.25f).Ints(2);
            BattleSquadMorale battle = Battle(110f, random: random);
            int checks = 0;
            battle.BreakingPointReached += _ => checks++;

            battle.OnCaptainDied();

            Assert.AreEqual(1, checks);
            Assert.AreEqual(MoraleState.Collapsed, battle.State);
            Assert.AreEqual(CollapseEffect.Contagion, battle.Collapse);
            Assert.AreEqual(AwakenEffect.None, battle.Awaken);
        }

        [Test]
        public void BreakingPoint_Awakens_WhenRollIsUnderAwakenChance()
        {
            var random = new ScriptedRandom().Floats(0.24f).Ints(1);
            BattleSquadMorale battle = Battle(110f, random: random);

            battle.OnCaptainDied();

            Assert.AreEqual(MoraleState.Awakened, battle.State);
            Assert.AreEqual(AwakenEffect.LastStand, battle.Awaken);
        }

        [Test]
        public void BreakingPoint_HappensOncePerBattle_EvenAfterRecoveringAbove100()
        {
            var random = new ScriptedRandom().Floats(0.9f).Ints(0);
            BattleSquadMorale battle = Battle(110f, random: random);
            int checks = 0;
            battle.BreakingPointReached += _ => checks++;

            battle.OnCaptainDied();
            battle.Morale.Add(60f);
            battle.OnCaptainDied();

            Assert.AreEqual(1, checks);
            Assert.AreEqual(1, random.FloatCalls);
        }

        [Test]
        public void AwakenChanceBonus_RaisesTheChance()
        {
            var random = new ScriptedRandom().Floats(0.4f).Ints(0);
            BattleSquadMorale battle = Battle(110f, random: random);
            battle.AwakenChanceBonus = 0.2f;

            battle.OnCaptainDied();

            Assert.AreEqual(MoraleState.Awakened, battle.State);
        }

        [Test]
        public void PerkGain_DoesNotTriggerCheck_ButLaterLossDoes()
        {
            BattleSquadMorale battle = Battle(150f, random: new ScriptedRandom().Floats(0.9f));
            battle.Morale.Add(30f);
            Assert.IsFalse(battle.CheckUsed);

            battle.Morale.Add(-90f);

            Assert.IsTrue(battle.CheckUsed);
            Assert.AreEqual(MoraleState.Collapsed, battle.State);
        }

        [Test]
        public void Enemy_NeverRunsBreakingPointCheck()
        {
            var random = new ScriptedRandom();
            BattleSquadMorale enemy = Battle(110f, MoraleSide.Enemy, random: random);

            enemy.OnCaptainDied();

            Assert.AreEqual(MoraleState.Normal, enemy.State);
            Assert.IsFalse(enemy.CheckUsed);
            Assert.AreEqual(0, random.FloatCalls);
        }

        // --- 100 미만으로 시작 ---

        [Test]
        public void StartingBelow100_StartsWithWeakCollapse_AndNoCheck()
        {
            var random = new ScriptedRandom().Ints(0);
            BattleSquadMorale battle = Battle(80f, random: random);
            int checks = 0;
            battle.BreakingPointReached += _ => checks++;

            Assert.IsTrue(battle.StartedShaken);
            Assert.AreEqual(MoraleState.Collapsed, battle.State);
            Assert.AreEqual(CollapseEffect.Fear, battle.Collapse);
            Assert.AreEqual(settings.StartingCollapseStrength, battle.EffectStrength);

            battle.OnCaptainDied();
            Assert.AreEqual(0, checks);
            Assert.AreEqual(0, random.FloatCalls);
        }

        [Test]
        public void EnemyStartingBelow100_HasNoCollapse()
        {
            BattleSquadMorale enemy = Battle(50f, MoraleSide.Enemy);
            Assert.IsFalse(enemy.StartedShaken);
            Assert.AreEqual(MoraleState.Normal, enemy.State);
        }

        // --- 도망 ---

        [Test]
        public void MoraleZero_RoutsWholeSquadOnce_AndIgnoresFurtherLoss()
        {
            BattleSquadMorale battle = Battle(30f);
            int routs = 0;
            battle.Routed += _ => routs++;

            battle.OnCaptainDied();
            battle.OnCaptainDied();
            battle.Morale.Add(50f);
            battle.OnMemberDied();

            Assert.AreEqual(1, routs);
            Assert.IsTrue(battle.IsRouted);
            Assert.AreEqual(MoraleState.Routed, battle.State);
        }

        [Test]
        public void BigLoss_ChecksBeforeRout_SoCollapsedSquadStillRouts()
        {
            var random = new ScriptedRandom().Floats(0.9f).Ints(0);
            BattleSquadMorale battle = Battle(110f, random: random);
            var order = new List<string>();
            battle.BreakingPointReached += _ => order.Add("check");
            battle.Routed += _ => order.Add("rout");

            battle.Morale.Add(-200f);

            CollectionAssert.AreEqual(new[] { "check", "rout" }, order);
            Assert.AreEqual(CollapseEffect.Fear, battle.Collapse);
        }

        [Test]
        public void EnemyRoutsAtZero()
        {
            BattleSquadMorale enemy = Battle(20f, MoraleSide.Enemy);
            bool routed = false;
            enemy.Routed += _ => routed = true;

            enemy.OnNearbySquadRouted();

            Assert.IsTrue(routed);
        }

        // --- 각성 효과 ---

        [Test]
        public void Rally_RaisesOwnMorale_AndAsksNearbySquadsToRise()
        {
            var random = new ScriptedRandom().Floats(0f).Ints(0);
            BattleSquadMorale battle = Battle(110f, random: random);
            float nearby = 0f;
            battle.NearbyMoraleChange += (_, amount) => nearby += amount;

            battle.OnNearbySquadRouted();

            Assert.AreEqual(AwakenEffect.Rally, battle.Awaken);
            Assert.AreEqual(110f - settings.NearbySquadRoutLoss + settings.RallyAmount, battle.Morale.Value, 0.001f);
            Assert.AreEqual(settings.RallyAmount, nearby, 0.001f);
        }

        [Test]
        public void Rally_OnHugeLoss_KeepsSquadFromRouting()
        {
            BattleSquadMorale battle = Battle(110f, random: new ScriptedRandom().Floats(0f).Ints(0));

            battle.Morale.Add(-200f);

            Assert.IsFalse(battle.IsRouted);
            Assert.AreEqual(settings.RallyAmount, battle.Morale.Value, 0.001f);
        }

        [Test]
        public void LastStand_AttackRisesWithShareOfMembersLost()
        {
            BattleSquadMorale battle = Battle(110f, members: 10, random: new ScriptedRandom().Floats(0f).Ints(1));
            for (int i = 0; i < 5; i++) battle.OnMemberDied();

            Assert.AreEqual(AwakenEffect.LastStand, battle.Awaken);
            // 60 < 100 → −10%, 결사항전 0.5 × 50% → +25%
            Assert.AreEqual(1f - 0.1f + 0.25f, battle.AttackMultiplier, 0.001f);
        }

        [Test]
        public void Steadfast_DoesNotRoutAtZero()
        {
            BattleSquadMorale battle = Battle(110f, random: new ScriptedRandom().Floats(0f).Ints(2));
            bool routed = false;
            battle.Routed += _ => routed = true;

            battle.Morale.Add(-200f);
            battle.OnCaptainDied();

            Assert.AreEqual(AwakenEffect.Steadfast, battle.Awaken);
            Assert.AreEqual(0f, battle.Morale.Value);
            Assert.IsFalse(routed);
            Assert.IsFalse(battle.IsRouted);
        }

        // --- 능력 보정 ---

        [Test]
        public void AttackMultiplier_FollowsMoraleBands_ForAllies()
        {
            Assert.AreEqual(1.1f, Battle(150f).AttackMultiplier, 0.001f);
            Assert.AreEqual(1f, Battle(120f).AttackMultiplier, 0.001f);
            Assert.AreEqual(0.9f, Battle(90f).AttackMultiplier, 0.001f);
        }

        [Test]
        public void AttackMultiplier_EnemiesGetOnlyLowBand()
        {
            Assert.AreEqual(1f, Battle(180f, MoraleSide.Enemy).AttackMultiplier, 0.001f);
            Assert.AreEqual(0.9f, Battle(90f, MoraleSide.Enemy).AttackMultiplier, 0.001f);
        }

        // --- 붕괴 효과 ---

        [Test]
        public void Fear_IgnoresOrdersAtChance_ScaledByStrength()
        {
            // 판정으로 걸린 공포: 50%
            var full = new ScriptedRandom().Floats(0.9f, 0.49f, 0.51f).Ints(0);
            BattleSquadMorale judged = Battle(110f, random: full);
            judged.OnCaptainDied();
            Assert.AreEqual(CollapseEffect.Fear, judged.Collapse);
            Assert.IsTrue(judged.ShouldIgnoreOrder());
            Assert.IsFalse(judged.ShouldIgnoreOrder());

            // 100 미만 시작의 약한 공포: 50% × 0.5 = 25%
            var weak = new ScriptedRandom().Ints(0).Floats(0.24f, 0.26f);
            BattleSquadMorale shaken = Battle(80f, random: weak);
            Assert.IsTrue(shaken.ShouldIgnoreOrder());
            Assert.IsFalse(shaken.ShouldIgnoreOrder());
        }

        [Test]
        public void NotAfraid_NeverIgnoresOrders()
        {
            var random = new ScriptedRandom();
            Assert.IsFalse(Battle(150f, random: random).ShouldIgnoreOrder());
            Assert.AreEqual(0, random.FloatCalls);
        }

        [Test]
        public void Panic_SignalsRetreatEachInterval()
        {
            BattleSquadMorale battle = Battle(110f, random: new ScriptedRandom().Floats(0.9f).Ints(1));
            battle.OnCaptainDied();
            int retreats = 0;
            battle.PanicRetreat += _ => retreats++;

            battle.Tick(settings.PanicInterval - 0.1f);
            Assert.AreEqual(0, retreats);
            battle.Tick(0.2f);
            Assert.AreEqual(1, retreats);
            battle.Tick(settings.PanicInterval * 2f);
            Assert.AreEqual(3, retreats);
        }

        [Test]
        public void Contagion_LowersNearbyMorale_WeakerWhenStartedShaken()
        {
            BattleSquadMorale judged = Battle(110f, random: new ScriptedRandom().Floats(0.9f).Ints(2));
            judged.OnCaptainDied();
            BattleSquadMorale shaken = Battle(80f, random: new ScriptedRandom().Ints(2));
            float judgedTotal = 0f, shakenTotal = 0f;
            judged.NearbyMoraleChange += (_, amount) => judgedTotal += amount;
            shaken.NearbyMoraleChange += (_, amount) => shakenTotal += amount;

            float window = settings.ContagionInterval * 2f;
            judged.Tick(window);
            shaken.Tick(window);

            Assert.AreEqual(-settings.ContagionAmount * 2f, judgedTotal, 0.001f);
            // 약한 전염: 주기 2배 → 1번, 양 절반
            Assert.AreEqual(-settings.ContagionAmount * 0.5f, shakenTotal, 0.001f);
        }

        [Test]
        public void Dispose_StopsListeningToMoraleChanges()
        {
            BattleSquadMorale battle = Battle(30f);
            bool routed = false;
            battle.Routed += _ => routed = true;

            battle.Dispose();
            battle.Morale.Add(-100f);

            Assert.IsFalse(routed);
        }

        // --- 전투 후 회복 (Q-39) ---

        [TestCase(0f, 80f)]
        [TestCase(30f, 80f)]
        [TestCase(70f, 90f)]
        [TestCase(150f, 170f)]
        [TestCase(190f, 200f)]
        public void RecoverAfterBattle_IsMaxOfPlus20And80_CappedAt200(float before, float after)
        {
            var morale = new SquadMorale(settings.MaxMorale, before);
            MoraleRules.RecoverAfterBattle(morale, settings);
            Assert.AreEqual(after, morale.Value, 0.001f);
        }
    }
}
