using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class FacilityStateTests
    {
        FacilityDefinition trainingGround;
        Wallet wallet;

        [SetUp]
        public void SetUp()
        {
            trainingGround = FacilityDefinition.Create(FacilityType.TrainingGround,
                new FacilityLevel(),
                new FacilityLevel
                {
                    upgradeCost = new Cost(100),
                    qualityTier = 1,
                    soldierModifiers = new List<StatModifier> { StatModifier.Percent(StatType.Attack, 0.1f) },
                });
            wallet = new Wallet();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(trainingGround);
        }

        [Test]
        public void NewFacility_StartsAtLevelZero()
        {
            var state = new FacilityState(trainingGround);

            Assert.AreEqual(0, state.Level);
            Assert.IsFalse(state.IsMaxLevel);
        }

        [Test]
        public void TryUpgrade_WithEnoughGold_PaysAndAppliesNextLevelEffects()
        {
            var state = new FacilityState(trainingGround);
            wallet.AddGold(150);

            Assert.IsTrue(state.TryUpgrade(wallet));

            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(50, wallet.Gold);
            Assert.AreEqual(1, state.Current.qualityTier);

            var soldier = new StatSheet();
            soldier.SetBase(StatType.Attack, 10f);
            soldier.AddModifiers(state.Current.soldierModifiers, state);
            Assert.AreEqual(11f, soldier.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void TryUpgrade_WithoutEnoughGold_DoesNothing()
        {
            var state = new FacilityState(trainingGround);
            wallet.AddGold(99);

            Assert.IsFalse(state.TryUpgrade(wallet));

            Assert.AreEqual(0, state.Level);
            Assert.AreEqual(99, wallet.Gold);
        }

        [Test]
        public void TryUpgrade_AtMaxLevel_DoesNothing()
        {
            var state = new FacilityState(trainingGround);
            wallet.AddGold(500);
            state.TryUpgrade(wallet);

            Assert.IsTrue(state.IsMaxLevel);
            Assert.IsFalse(state.TryUpgrade(wallet));
            Assert.AreEqual(400, wallet.Gold);
        }
    }
}
