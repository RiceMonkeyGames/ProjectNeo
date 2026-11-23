using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Skills;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// COMBAT-06 1단계 순수 규칙(Q-62~Q-65, Q-72, Q-73): 칸 수, 쿨다운, 효과 대상·양, 플레이어 장착.
    /// </summary>
    public class SkillRulesTests
    {
        readonly List<Object> created = new List<Object>();
        SkillDefinition whirl, warCry, heal, slam;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            whirl = Track(SkillDefinition.Create("whirl", 8f, SkillTargetMode.Self, 0f, 3f, SkillEffect.Damage(10f, 1f)));
            warCry = Track(SkillDefinition.Create("war_cry", 20f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.2f), 10f)));
            heal = Track(SkillDefinition.Create("heal", 15f, SkillTargetMode.AimPoint, 12f, 4f, SkillEffect.Heal(30f)));
            slam = Track(SkillDefinition.Create("slam", 6f, SkillTargetMode.TargetUnit, 3f, 0f,
                SkillEffect.Damage(5f), SkillEffect.Stagger(50f)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void Create_StoresDataAndClampsNegatives()
        {
            SkillDefinition skill = Track(SkillDefinition.Create("odd", -1f, SkillTargetMode.AimPoint, -2f, -3f));
            Assert.AreEqual("odd", skill.Id);
            Assert.AreEqual(0f, skill.Cooldown);
            Assert.AreEqual(0f, skill.Range);
            Assert.AreEqual(0f, skill.Radius);
            Assert.AreEqual(0, skill.Effects.Count);
            Assert.AreEqual(2, slam.Effects.Count);
            Assert.AreEqual(SkillTargetMode.TargetUnit, slam.TargetMode);
        }

        [Test]
        public void TargetModeAndEffectKind_KeepPersistentNumbers()
        {
            Assert.AreEqual(0, (int)SkillTargetMode.Self);
            Assert.AreEqual(1, (int)SkillTargetMode.AimPoint);
            Assert.AreEqual(2, (int)SkillTargetMode.TargetUnit);
            Assert.AreEqual(3, (int)SkillTargetMode.OwnSquad);
            Assert.AreEqual(0, (int)SkillEffectKind.Damage);
            Assert.AreEqual(1, (int)SkillEffectKind.Buff);
            Assert.AreEqual(2, (int)SkillEffectKind.Debuff);
            Assert.AreEqual(3, (int)SkillEffectKind.Heal);
            Assert.AreEqual(4, (int)SkillEffectKind.MoraleGain);
            Assert.AreEqual(5, (int)SkillEffectKind.Stagger);
        }

        [Test]
        public void SlotRules_PlayerHasThree_HeroOpensByGrade()
        {
            Assert.AreEqual(3, SkillSlotRules.PlayerSlots);
            Assert.AreEqual(1, SkillSlotRules.HeroSlots(0));
            Assert.AreEqual(1, SkillSlotRules.HeroSlots(1));
            Assert.AreEqual(2, SkillSlotRules.HeroSlots(2));
            Assert.AreEqual(3, SkillSlotRules.HeroSlots(3));
            Assert.AreEqual(3, SkillSlotRules.HeroSlots(5));

            var fixedSkills = new List<SkillDefinition> { whirl, warCry, heal };
            CollectionAssert.AreEqual(new[] { whirl }, SkillSlotRules.OpenHeroSkills(fixedSkills, 1));
            CollectionAssert.AreEqual(new[] { whirl, warCry }, SkillSlotRules.OpenHeroSkills(fixedSkills, 2));
            CollectionAssert.AreEqual(new[] { whirl, warCry, heal }, SkillSlotRules.OpenHeroSkills(fixedSkills, 3));
            CollectionAssert.AreEqual(new[] { warCry }, SkillSlotRules.OpenHeroSkills(new List<SkillDefinition> { null, warCry }, 2));
            Assert.AreEqual(0, SkillSlotRules.OpenHeroSkills(null, 3).Count);
        }

        [Test]
        public void Slots_AllReadyAtStart_CooldownOnUse_ReadyAfterTick()
        {
            var slots = new SkillSlots(new[] { whirl, null, slam, heal, warCry });
            Assert.AreEqual(3, slots.Count, "null skipped, capped at 3 slots");
            Assert.AreSame(slam, slots.Get(1));
            Assert.IsTrue(slots.IsReady(0) && slots.IsReady(1) && slots.IsReady(2));

            var changes = new List<int>();
            slots.Changed += changes.Add;
            Assert.IsTrue(slots.TryUse(0));
            Assert.IsFalse(slots.TryUse(0), "on cooldown");
            Assert.AreEqual(8f, slots.Remaining(0));
            Assert.AreEqual(1f, slots.RemainingFraction(0));

            slots.Tick(6f);
            Assert.AreEqual(2f, slots.Remaining(0), 1e-4f);
            Assert.AreEqual(0.25f, slots.RemainingFraction(0), 1e-4f);
            Assert.IsFalse(slots.IsReady(0));

            slots.Tick(5f);
            Assert.IsTrue(slots.IsReady(0));
            Assert.AreEqual(0f, slots.Remaining(0));
            CollectionAssert.AreEqual(new[] { 0, 0 }, changes, "used, then ready");

            Assert.IsFalse(slots.TryUse(-1));
            Assert.IsFalse(slots.TryUse(3));
            Assert.IsNull(slots.Get(3));
        }

        [Test]
        public void Slots_ResetAll_MakesEverySlotReady()
        {
            var slots = new SkillSlots(new[] { whirl, slam });
            slots.TryUse(0);
            slots.TryUse(1);
            slots.ResetAll();
            Assert.IsTrue(slots.IsReady(0));
            Assert.IsTrue(slots.IsReady(1));
        }

        [Test]
        public void Slots_ZeroCooldown_IsAlwaysReady()
        {
            SkillDefinition free = Track(SkillDefinition.Create("free", 0f, SkillTargetMode.Self, 0f, 0f));
            var slots = new SkillSlots(new[] { free });
            Assert.IsTrue(slots.TryUse(0));
            Assert.IsTrue(slots.TryUse(0));
            Assert.AreEqual(0f, slots.RemainingFraction(0));
        }

        [Test]
        public void EffectRules_SideByKind()
        {
            Assert.IsTrue(SkillEffectRules.AffectsEnemies(SkillEffectKind.Damage));
            Assert.IsTrue(SkillEffectRules.AffectsEnemies(SkillEffectKind.Debuff));
            Assert.IsTrue(SkillEffectRules.AffectsEnemies(SkillEffectKind.Stagger));
            Assert.IsFalse(SkillEffectRules.AffectsEnemies(SkillEffectKind.Buff));
            Assert.IsFalse(SkillEffectRules.AffectsEnemies(SkillEffectKind.Heal));
            Assert.IsFalse(SkillEffectRules.AffectsEnemies(SkillEffectKind.MoraleGain));
            Assert.IsTrue(SkillEffectRules.IsTimed(SkillEffectKind.Buff));
            Assert.IsTrue(SkillEffectRules.IsTimed(SkillEffectKind.Debuff));
            Assert.IsFalse(SkillEffectRules.IsTimed(SkillEffectKind.Damage));
        }

        [Test]
        public void EffectRules_AmountAddsAttackScaleForDamageAndHealOnly()
        {
            Assert.AreEqual(30f, SkillEffectRules.Amount(SkillEffect.Damage(10f, 1f), 20f), 1e-4f);
            Assert.AreEqual(25f, SkillEffectRules.Amount(SkillEffect.Heal(15f, 0.5f), 20f), 1e-4f);
            Assert.AreEqual(10f, SkillEffectRules.Amount(SkillEffect.Damage(10f, 1f), -5f), 1e-4f, "negative attack ignored");
            Assert.AreEqual(50f, SkillEffectRules.Amount(SkillEffect.Stagger(50f), 100f), 1e-4f);
            Assert.AreEqual(12f, SkillEffectRules.Amount(SkillEffect.MoraleGain(12f), 100f), 1e-4f);
            Assert.AreEqual(0f, SkillEffectRules.Amount(SkillEffect.Damage(-10f), 0f), "never negative");
        }

        [Test]
        public void StatusTimers_RefreshWithoutStacking_AndExpire()
        {
            var timers = new SkillStatusTimers();
            object a = new object(), b = new object();
            Assert.IsTrue(timers.Apply(a, 2f));
            Assert.IsFalse(timers.Apply(a, 1f), "already active");
            Assert.AreEqual(2f, timers.Remaining(a), "shorter reapply keeps the longer time");
            Assert.IsTrue(timers.Apply(b, 5f));

            var expired = new List<object>();
            timers.Tick(1.5f, expired);
            Assert.AreEqual(0, expired.Count);
            Assert.IsFalse(timers.Apply(a, 3f));
            Assert.AreEqual(3f, timers.Remaining(a), "longer reapply refreshes");

            timers.Tick(3f, expired);
            CollectionAssert.AreEqual(new[] { a }, expired);
            Assert.IsFalse(timers.Has(a));
            Assert.IsTrue(timers.Has(b));

            expired.Clear();
            timers.Clear(expired);
            CollectionAssert.AreEqual(new[] { b }, expired);
            Assert.AreEqual(0, timers.Count);
            Assert.IsFalse(timers.Apply(null, 1f));
        }

        [Test]
        public void ClampAim_PullsFlatDistanceIntoRange_KeepsHeight()
        {
            Vector3 clamped = SkillTargeting.ClampAim(Vector3.zero, new Vector3(10f, 3f, 0f), 4f);
            Assert.AreEqual(4f, clamped.x, 1e-4f);
            Assert.AreEqual(3f, clamped.y, 1e-4f);
            Assert.AreEqual(new Vector3(2f, 0f, 1f), SkillTargeting.ClampAim(Vector3.zero, new Vector3(2f, 0f, 1f), 4f));
        }

        [Test]
        public void AutoRules_IntentAndThresholds()
        {
            SkillDefinition mixed = Track(SkillDefinition.Create("mixed", 1f, SkillTargetMode.Self, 0f, 2f,
                SkillEffect.Heal(5f), SkillEffect.Stagger(10f)));
            SkillDefinition rally = Track(SkillDefinition.Create("rally", 1f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.MoraleGain(10f)));
            Assert.AreEqual(SkillAutoIntent.Offense, SkillAutoRules.Intent(whirl));
            Assert.AreEqual(SkillAutoIntent.Offense, SkillAutoRules.Intent(slam));
            Assert.AreEqual(SkillAutoIntent.Offense, SkillAutoRules.Intent(mixed), "any enemy effect -> offense");
            Assert.AreEqual(SkillAutoIntent.Heal, SkillAutoRules.Intent(heal));
            Assert.AreEqual(SkillAutoIntent.Support, SkillAutoRules.Intent(warCry));
            Assert.AreEqual(SkillAutoIntent.Support, SkillAutoRules.Intent(rally));

            SkillAutoSettings auto = Track(SkillAutoSettings.Create(0.5f, 2, 0.6f, 12f));
            Assert.IsFalse(SkillAutoRules.ShouldUseArea(1, auto));
            Assert.IsTrue(SkillAutoRules.ShouldUseArea(2, auto));
            Assert.IsTrue(SkillAutoRules.ShouldHeal(0.6f, auto));
            Assert.IsFalse(SkillAutoRules.ShouldHeal(0.61f, auto));
            Assert.IsTrue(SkillAutoRules.ShouldSupport(true, 12f, auto));
            Assert.IsFalse(SkillAutoRules.ShouldSupport(true, 12.5f, auto));
            Assert.IsFalse(SkillAutoRules.ShouldSupport(false, 0f, auto), "no enemy target -> not engaged");
            Assert.IsFalse(SkillAutoRules.ShouldUseArea(5, null));

            Assert.AreEqual(0.5f, SkillAutoRules.HealthRatio(50f, 100f), 1e-4f);
            Assert.AreEqual(0f, SkillAutoRules.HealthRatio(-5f, 100f));
            Assert.AreEqual(1f, SkillAutoRules.HealthRatio(150f, 100f));
            Assert.AreEqual(1f, SkillAutoRules.HealthRatio(10f, 0f));

            SkillAutoSettings clamped = Track(SkillAutoSettings.Create(0f, 0, 2f, -1f));
            Assert.AreEqual(0.05f, clamped.ThinkInterval, 1e-4f);
            Assert.AreEqual(1, clamped.MinimumEnemiesInArea);
            Assert.AreEqual(1f, clamped.HealBelowHealthRatio);
            Assert.AreEqual(0f, clamped.EngageDistance);
        }

        [Test]
        public void AutoRules_MoraleThreshold_COMBAT13()
        {
            SkillDefinition rally = Track(SkillDefinition.Create("rally", 1f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.MoraleGain(10f)));
            SkillDefinition rousing = Track(SkillDefinition.Create("rousing", 1f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.MoraleGain(10f), SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.1f), 5f)));
            SkillDefinition empty = Track(SkillDefinition.Create("empty", 1f, SkillTargetMode.Self, 0f, 0f));
            Assert.IsTrue(SkillAutoRules.IsMoraleOnly(rally));
            Assert.IsFalse(SkillAutoRules.IsMoraleOnly(rousing), "a buff alongside morale is worth casting anyway");
            Assert.IsFalse(SkillAutoRules.IsMoraleOnly(warCry));
            Assert.IsFalse(SkillAutoRules.IsMoraleOnly(empty));
            Assert.IsFalse(SkillAutoRules.IsMoraleOnly(null));

            SkillAutoSettings defaults = Track(SkillAutoSettings.Create());
            Assert.AreEqual(0.75f, defaults.MoraleGainBelowRatio, 1e-4f);
            SkillAutoSettings auto = Track(SkillAutoSettings.Create(0.5f, 2, 0.6f, 12f, 0.8f));
            Assert.IsTrue(SkillAutoRules.ShouldRaiseMorale(0.8f, auto));
            Assert.IsFalse(SkillAutoRules.ShouldRaiseMorale(0.81f, auto));
            Assert.IsFalse(SkillAutoRules.ShouldRaiseMorale(0f, null));
            Assert.AreEqual(0.95f, SkillAutoRules.MoraleRatio(190f, 200f), 1e-4f);
            Assert.AreEqual(1f, SkillAutoRules.MoraleRatio(250f, 200f));
            Assert.AreEqual(0f, SkillAutoRules.MoraleRatio(-1f, 200f));
            Assert.AreEqual(1f, SkillAutoRules.MoraleRatio(10f, 0f));

            Assert.AreEqual(0f, Track(SkillAutoSettings.Create(moraleGainBelowRatio: -1f)).MoraleGainBelowRatio);
            Assert.AreEqual(1f, Track(SkillAutoSettings.Create(moraleGainBelowRatio: 3f)).MoraleGainBelowRatio);
        }

        [Test]
        public void AutoRules_BestClusterCenter_COMBAT13()
        {
            // 외톨이 하나(0,0,0)와 세 명 무리(10,0,0 근처). 반경 2.
            var enemies = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(11.5f, 0f, 0f), new Vector3(10f, 0f, 1.5f),
            };
            var bodies = new List<float> { 0f, 0f, 0f, 0f };
            int best = SkillAutoRules.BestClusterCenter(enemies, enemies, bodies, 2f, Vector3.zero, out int hits);
            Assert.AreEqual(1, best, "the clump center catches all three");
            Assert.AreEqual(3, hits);

            // 사거리 안 후보가 외톨이뿐이면 무리 쪽 적도 반경에 들 때만 센다.
            var nearOnly = new List<Vector3> { enemies[0] };
            Assert.AreEqual(0, SkillAutoRules.BestClusterCenter(nearOnly, enemies, bodies, 2f, Vector3.zero, out hits));
            Assert.AreEqual(1, hits);

            // 몸 반경만큼 더 멀어도 든다.
            var wide = new List<float> { 0f, 0f, 0f, 0.6f };
            var pair = new List<Vector3> { enemies[2] };
            SkillAutoRules.BestClusterCenter(pair, enemies, bodies, 2f, Vector3.zero, out hits);
            Assert.AreEqual(2, hits, "(10,0,1.5) is 2.12 m from (11.5,0,0): outside a 2 m radius");
            SkillAutoRules.BestClusterCenter(pair, enemies, wide, 2f, Vector3.zero, out hits);
            Assert.AreEqual(3, hits, "a 0.6 m body lets it reach 2.6 m");

            // 같은 수면 선호 지점에 가까운 후보.
            var twins = new List<Vector3> { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            Assert.AreEqual(1, SkillAutoRules.BestClusterCenter(twins, twins, null, 1f, new Vector3(4f, 0f, 0f), out hits));
            Assert.AreEqual(1, hits);
            Assert.AreEqual(0, SkillAutoRules.BestClusterCenter(twins, twins, null, 1f, new Vector3(-4f, 0f, 0f), out _));

            Assert.AreEqual(-1, SkillAutoRules.BestClusterCenter(new List<Vector3>(), enemies, bodies, 2f, Vector3.zero, out hits));
            Assert.AreEqual(0, hits);
            Assert.AreEqual(0, SkillAutoRules.BestClusterCenter(enemies, enemies, bodies, 0f, Vector3.zero, out hits));
            Assert.AreEqual(0, hits, "zero radius catches no one");
        }

        [Test]
        public void AutoRules_UrgencyOrderAndTiers_COMBAT18()
        {
            Assert.Greater(SkillAutoUrgency.CriticalHeal, SkillAutoUrgency.MoraleStabilize);
            Assert.Greater(SkillAutoUrgency.MoraleStabilize, SkillAutoUrgency.HighValueOffense);
            Assert.Greater(SkillAutoUrgency.HighValueOffense, SkillAutoUrgency.Heal, "user decision 2026-11-23: plain heal below high-value offense");
            Assert.Greater(SkillAutoUrgency.Heal, SkillAutoUrgency.General);
            Assert.AreEqual(0, (int)SkillAutoUrgency.None);
            Assert.AreEqual(5, (int)SkillAutoUrgency.CriticalHeal);

            SkillAutoSettings defaults = Track(SkillAutoSettings.Create());
            Assert.AreEqual(0.3f, defaults.CriticalHealBelowRatio, 1e-4f);
            Assert.AreEqual(4, defaults.HighValueEnemiesInArea);
            Assert.AreEqual(2.5f, defaults.MeleeEngageMaxHeight, 1e-4f);

            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.HealUrgency(0.61f, defaults));
            Assert.AreEqual(SkillAutoUrgency.Heal, SkillAutoRules.HealUrgency(0.6f, defaults));
            Assert.AreEqual(SkillAutoUrgency.Heal, SkillAutoRules.HealUrgency(0.31f, defaults));
            Assert.AreEqual(SkillAutoUrgency.CriticalHeal, SkillAutoRules.HealUrgency(0.3f, defaults));
            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.HealUrgency(0f, null));

            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.AreaOffenseUrgency(1, false, defaults));
            Assert.AreEqual(SkillAutoUrgency.General, SkillAutoRules.AreaOffenseUrgency(3, false, defaults));
            Assert.AreEqual(SkillAutoUrgency.HighValueOffense, SkillAutoRules.AreaOffenseUrgency(4, false, defaults));
            Assert.AreEqual(SkillAutoUrgency.HighValueOffense, SkillAutoRules.AreaOffenseUrgency(1, true, defaults),
                "staggering a boss is worth it below the area minimum");
            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.AreaOffenseUrgency(9, true, null));
            Assert.AreEqual(SkillAutoUrgency.General, SkillAutoRules.UnitOffenseUrgency(false));
            Assert.AreEqual(SkillAutoUrgency.HighValueOffense, SkillAutoRules.UnitOffenseUrgency(true));

            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.SupportUrgency(false, false, true), "not engaged");
            Assert.AreEqual(SkillAutoUrgency.General, SkillAutoRules.SupportUrgency(true, false, false));
            Assert.AreEqual(SkillAutoUrgency.None, SkillAutoRules.SupportUrgency(true, true, false), "morale-only skill waits for low morale");
            Assert.AreEqual(SkillAutoUrgency.MoraleStabilize, SkillAutoRules.SupportUrgency(true, true, true));
            Assert.AreEqual(SkillAutoUrgency.MoraleStabilize, SkillAutoRules.SupportUrgency(true, false, true));

            SkillAutoSettings clamped = Track(SkillAutoSettings.Create(criticalHealBelowRatio: 2f, highValueEnemiesInArea: 0,
                meleeEngageMaxHeight: -1f));
            Assert.AreEqual(1f, clamped.CriticalHealBelowRatio);
            Assert.AreEqual(1, clamped.HighValueEnemiesInArea);
            Assert.AreEqual(0f, clamped.MeleeEngageMaxHeight);
        }

        [Test]
        public void AutoRules_MostUrgent_HighestThenEarliestSlot_COMBAT18()
        {
            var order = new List<SkillAutoUrgency> { SkillAutoUrgency.General, SkillAutoUrgency.CriticalHeal, SkillAutoUrgency.Heal };
            Assert.AreEqual(1, SkillAutoRules.MostUrgent(order), "slot order no longer wins over urgency");
            var tie = new List<SkillAutoUrgency> { SkillAutoUrgency.None, SkillAutoUrgency.General, SkillAutoUrgency.General };
            Assert.AreEqual(1, SkillAutoRules.MostUrgent(tie), "same tier: earlier slot");
            Assert.AreEqual(-1, SkillAutoRules.MostUrgent(new List<SkillAutoUrgency> { SkillAutoUrgency.None }));
            Assert.AreEqual(-1, SkillAutoRules.MostUrgent(null));
        }

        [Test]
        public void AutoRules_EffectFlagsAndEngageHeight_COMBAT18()
        {
            SkillDefinition bash = Track(SkillDefinition.Create("bash", 1f, SkillTargetMode.TargetUnit, 2f, 0f,
                SkillEffect.Damage(5f), SkillEffect.Stagger(50f)));
            SkillDefinition rousing = Track(SkillDefinition.Create("rousing", 1f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.MoraleGain(10f), SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.1f), 5f)));
            Assert.IsTrue(SkillAutoRules.HasStagger(bash));
            Assert.IsFalse(SkillAutoRules.HasStagger(rousing));
            Assert.IsTrue(SkillAutoRules.HasMoraleGain(rousing));
            Assert.IsFalse(SkillAutoRules.HasMoraleGain(bash));
            Assert.IsFalse(SkillAutoRules.HasStagger(null));

            SkillAutoSettings defaults = Track(SkillAutoSettings.Create());
            Assert.IsTrue(SkillAutoRules.WithinEngageHeight(true, 2.5f, defaults));
            Assert.IsFalse(SkillAutoRules.WithinEngageHeight(true, -6f, defaults), "melee hero on a 6 m wall vs enemy below");
            Assert.IsTrue(SkillAutoRules.WithinEngageHeight(false, -6f, defaults), "ranged heroes shoot down from the wall");
            Assert.IsTrue(SkillAutoRules.WithinEngageHeight(true, -6f, Track(SkillAutoSettings.Create(meleeEngageMaxHeight: 0f))),
                "0 turns the height check off");
            Assert.IsFalse(SkillAutoRules.WithinEngageHeight(false, 0f, null));
        }

        [Test]
        public void AutoRules_BestHealCenter_WeightsMissingHealth_COMBAT18()
        {
            // 가장 많이 다친 외톨이(0,0,0 · 20%)와, 덜 다친 세 명 무리(10,0,0 근처 · 50%씩). 반경 2.
            var allies = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(11f, 0f, 0f), new Vector3(10f, 0f, 1f),
            };
            var ratios = new List<float> { 0.2f, 0.5f, 0.5f, 0.5f };
            int best = SkillAutoRules.BestHealCenter(allies, allies, null, ratios, 2f, allies[0], out float missing);
            Assert.AreEqual(1, best, "three 50% allies (1.5 missing) beat one 20% ally (0.8 missing)");
            Assert.AreEqual(1.5f, missing, 1e-4f);

            // 무리가 덜 다치면 가장 많이 다친 외톨이를 덮는다.
            var light = new List<float> { 0.2f, 0.9f, 0.9f, 0.9f };
            Assert.AreEqual(0, SkillAutoRules.BestHealCenter(allies, allies, null, light, 2f, allies[0], out missing));
            Assert.AreEqual(0.8f, missing, 1e-4f);

            // 다치지 않은 아군은 무게가 0이다. 같으면 선호 지점(가장 많이 다친 아군)에 가까운 후보.
            var twins = new List<Vector3> { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            var even = new List<float> { 0.4f, 0.4f };
            Assert.AreEqual(1, SkillAutoRules.BestHealCenter(twins, twins, null, even, 1f, twins[1], out _));
            Assert.AreEqual(0, SkillAutoRules.BestHealCenter(twins, twins, null, even, 1f, twins[0], out _));

            Assert.AreEqual(-1, SkillAutoRules.BestHealCenter(new List<Vector3>(), allies, null, ratios, 2f, Vector3.zero, out missing));
            Assert.AreEqual(0f, missing);
        }
    }
}
