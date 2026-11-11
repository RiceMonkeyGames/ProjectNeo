using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Meta;
using ProjectNeo.Morale;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class PerkTests
    {
        sealed class FakeBattle : IPerkEffectContext
        {
            public readonly List<SquadMorale> allySquads = new List<SquadMorale>();

            public void AddMoraleToAllySquads(float amount)
            {
                foreach (SquadMorale squad in allySquads) squad.Add(amount);
            }
        }

        readonly List<Object> created = new List<Object>();
        MetaProgress progress;
        PerkLoadout loadout;

        [SetUp]
        public void SetUp()
        {
            progress = new MetaProgress { perkPoints = 2 };
            loadout = new PerkLoadout(progress);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        PerkDefinition StatPerk(string id, PerkTarget target, StatModifier modifier, int cost = 1, bool unlockedByDefault = true)
        {
            PerkDefinition perk = PerkDefinition.Create(id, PerkCategory.Combat,
                new[] { new PerkStatEffect(target, modifier) }, null, cost, unlockedByDefault);
            created.Add(perk);
            return perk;
        }

        [Test]
        public void TryAllocate_SpendsPoints()
        {
            PerkDefinition perk = StatPerk("a", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 5f));

            Assert.IsTrue(loadout.TryAllocate(perk));
            Assert.AreEqual(1, loadout.AvailablePoints);
        }

        [Test]
        public void TryAllocate_FailsWithoutEnoughPoints_OrWhenAlreadyAllocated()
        {
            PerkDefinition expensive = StatPerk("big", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 5f), cost: 3);
            PerkDefinition cheap = StatPerk("small", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f));

            Assert.IsFalse(loadout.TryAllocate(expensive));
            Assert.IsTrue(loadout.TryAllocate(cheap));
            Assert.IsFalse(loadout.TryAllocate(cheap));
        }

        [Test]
        public void TryAllocate_FailsForLockedPerk_UntilUnlocked()
        {
            PerkDefinition locked = StatPerk("locked", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 5f), unlockedByDefault: false);

            Assert.IsFalse(loadout.TryAllocate(locked));

            progress.unlockedIds.Add("locked");
            Assert.IsTrue(loadout.TryAllocate(locked));
        }

        [Test]
        public void PointsEarnedMidRun_CanBeSpentImmediately()
        {
            PerkDefinition a = StatPerk("a", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f));
            PerkDefinition b = StatPerk("b", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f));
            PerkDefinition c = StatPerk("c", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f));
            loadout.TryAllocate(a);
            loadout.TryAllocate(b);
            Assert.IsFalse(loadout.TryAllocate(c));

            CommanderProgression.AddPerkPoints(progress, 1);

            Assert.IsTrue(loadout.TryAllocate(c));
        }

        [Test]
        public void Reset_ClearsAllocatedPerks_ButKeepsPointCount()
        {
            loadout.TryAllocate(StatPerk("a", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f)));
            loadout.TryAllocate(StatPerk("b", PerkTarget.Player, StatModifier.Flat(StatType.Attack, 1f)));

            loadout.Reset();

            Assert.AreEqual(0, loadout.Allocated.Count);
            Assert.AreEqual(2, progress.perkPoints);
            Assert.AreEqual(2, loadout.AvailablePoints);
        }

        [Test]
        public void StatEffects_ApplyOnlyToTheirTarget()
        {
            PerkDefinition perk = StatPerk("drill", PerkTarget.Soldiers, StatModifier.Percent(StatType.Attack, 0.2f));
            var player = new StatSheet();
            var soldier = new StatSheet();
            player.SetBase(StatType.Attack, 100f);
            soldier.SetBase(StatType.Attack, 10f);

            PerkLoadout.ApplyStatEffects(perk, player, PerkTarget.Player);
            PerkLoadout.ApplyStatEffects(perk, soldier, PerkTarget.Soldiers);

            Assert.AreEqual(100f, player.GetValue(StatType.Attack), 0.001f);
            Assert.AreEqual(12f, soldier.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void AllocatedPerk_IsReflectedInStatsImmediately_AndRemovedOnReset()
        {
            var player = new StatSheet();
            player.SetBase(StatType.Attack, 100f);
            loadout.PerkAllocated += perk => PerkLoadout.ApplyStatEffects(perk, player, PerkTarget.Player);
            loadout.PerksReset += perks => { foreach (PerkDefinition perk in perks) player.RemoveModifiersFrom(perk); };

            loadout.TryAllocate(StatPerk("strong", PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.1f)));
            Assert.AreEqual(110f, player.GetValue(StatType.Attack), 0.001f);

            loadout.Reset();
            Assert.AreEqual(100f, player.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void ApplyAllStatEffects_GivesNewUnitEveryAllocatedPerk()
        {
            loadout.TryAllocate(StatPerk("a", PerkTarget.Soldiers, StatModifier.Flat(StatType.Attack, 2f)));
            loadout.TryAllocate(StatPerk("b", PerkTarget.Soldiers, StatModifier.Flat(StatType.Attack, 3f)));
            var recruit = new StatSheet();
            recruit.SetBase(StatType.Attack, 10f);

            loadout.ApplyAllStatEffects(recruit, PerkTarget.Soldiers);

            Assert.AreEqual(15f, recruit.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void MoraleBoostPerk_RaisesAllySquadMorale_OnItsTriggerOnly()
        {
            MoraleBoostPerkEffect effect = MoraleBoostPerkEffect.Create(PerkTrigger.BattleStart, 15f);
            PerkDefinition perk = PerkDefinition.Create("rally", PerkCategory.UnitBoost, null, new PerkSpecialEffect[] { effect });
            created.Add(effect);
            created.Add(perk);
            var battle = new FakeBattle();
            battle.allySquads.Add(new SquadMorale(100f, 50f));
            battle.allySquads.Add(new SquadMorale(100f, 95f));
            loadout.TryAllocate(perk);

            loadout.RaiseTrigger(PerkTrigger.PlayerSkillUsed, battle);
            Assert.AreEqual(50f, battle.allySquads[0].Value);

            loadout.RaiseTrigger(PerkTrigger.BattleStart, battle);
            Assert.AreEqual(65f, battle.allySquads[0].Value);
            Assert.AreEqual(100f, battle.allySquads[1].Value);
        }

        [Test]
        public void SpecialEffect_DoesNothing_WhenPerkIsNotAllocated()
        {
            var battle = new FakeBattle();
            battle.allySquads.Add(new SquadMorale(100f, 50f));

            loadout.RaiseTrigger(PerkTrigger.BattleStart, battle);

            Assert.AreEqual(50f, battle.allySquads[0].Value);
        }
    }
}
