using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Meta;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class MetaProgressionTests
    {
        readonly List<Object> created = new List<Object>();
        MetaConfig config;
        MetaProgress progress;

        [SetUp]
        public void SetUp()
        {
            // 레벨 1→2에 100, 2→3에 150. 레벨당 퍽 포인트 1. 런 점수의 10%가 경험치. 해금 1회에 100.
            config = MetaConfig.Create(100, 50, 1, 0.1f, 100);
            created.Add(config);
            progress = new MetaProgress();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        HeroDefinition LockedHero(string id)
        {
            HeroDefinition hero = HeroDefinition.Create(id, unlockedByDefault: false);
            created.Add(hero);
            return hero;
        }

        [Test]
        public void AddXp_BelowThreshold_KeepsLevel()
        {
            int gained = CommanderProgression.AddXp(progress, config, 99);

            Assert.AreEqual(0, gained);
            Assert.AreEqual(1, progress.commanderLevel);
            Assert.AreEqual(0, progress.perkPoints);
        }

        [Test]
        public void AddXp_LevelUp_GrantsPerkPointImmediately()
        {
            CommanderProgression.AddXp(progress, config, 100);

            Assert.AreEqual(2, progress.commanderLevel);
            Assert.AreEqual(1, progress.perkPoints);
            Assert.AreEqual(0, progress.commanderXp);
        }

        [Test]
        public void AddXp_CanGainSeveralLevelsAtOnce_AndCarriesRemainder()
        {
            int gained = CommanderProgression.AddXp(progress, config, 260);

            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, progress.commanderLevel);
            Assert.AreEqual(2, progress.perkPoints);
            Assert.AreEqual(10, progress.commanderXp);
        }

        [Test]
        public void AddRunScoreBonus_ConvertsPartOfScoreToXp()
        {
            int xp = CommanderProgression.AddRunScoreBonus(progress, config, 1234);

            Assert.AreEqual(123, xp);
            Assert.AreEqual(2, progress.commanderLevel);
            Assert.AreEqual(23, progress.commanderXp);
        }

        [Test]
        public void UnlockRoll_SpendsPoints_UnlocksOne_AndMarksItGuaranteed()
        {
            progress.unlockPoints = 150;
            var heroes = new List<IUnlockable> { LockedHero("archer"), LockedHero("knight") };

            IUnlockable unlocked = UnlockGacha.TryRoll(progress, config, UnlockCategory.Hero, heroes, new SeededRandom(1));

            Assert.IsNotNull(unlocked);
            Assert.AreEqual(50, progress.unlockPoints);
            Assert.IsTrue(progress.IsUnlocked(unlocked));
            CollectionAssert.Contains(progress.guaranteedNextRunIds, unlocked.Id);
            Assert.AreEqual(1, UnlockGacha.CountRemaining(progress, UnlockCategory.Hero, heroes));
        }

        [Test]
        public void UnlockRoll_WithoutEnoughPoints_ChangesNothing()
        {
            progress.unlockPoints = 99;
            var heroes = new List<IUnlockable> { LockedHero("archer") };

            Assert.IsNull(UnlockGacha.TryRoll(progress, config, UnlockCategory.Hero, heroes, new SeededRandom(1)));
            Assert.AreEqual(99, progress.unlockPoints);
            Assert.AreEqual(0, progress.unlockedIds.Count);
        }

        [Test]
        public void UnlockRoll_NeverRepeats_AndStopsWhenEverythingIsUnlocked()
        {
            progress.unlockPoints = 1000;
            var heroes = new List<IUnlockable> { LockedHero("a"), LockedHero("b"), LockedHero("c") };
            var random = new SeededRandom(7);
            var seen = new HashSet<string>();

            for (int i = 0; i < 3; i++)
            {
                IUnlockable unlocked = UnlockGacha.TryRoll(progress, config, UnlockCategory.Hero, heroes, random);
                Assert.IsTrue(seen.Add(unlocked.Id));
            }

            Assert.IsNull(UnlockGacha.TryRoll(progress, config, UnlockCategory.Hero, heroes, random));
            Assert.AreEqual(700, progress.unlockPoints);
        }

        [Test]
        public void UnlockRoll_OnlyPicksFromRequestedCategory()
        {
            progress.unlockPoints = 100;
            ItemDefinition item = ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(10), unlockedByDefault: false);
            created.Add(item);
            var candidates = new List<IUnlockable> { item, LockedHero("archer") };

            IUnlockable unlocked = UnlockGacha.TryRoll(progress, config, UnlockCategory.Item, candidates, new SeededRandom(3));

            Assert.AreEqual("sword", unlocked.Id);
        }

        [Test]
        public void MetaProgress_SurvivesJsonRoundTrip()
        {
            progress.commanderLevel = 4;
            progress.perkPoints = 3;
            progress.unlockPoints = 20;
            progress.unlockedIds.Add("archer");

            MetaProgress loaded = JsonUtility.FromJson<MetaProgress>(JsonUtility.ToJson(progress));

            Assert.AreEqual(4, loaded.commanderLevel);
            Assert.AreEqual(3, loaded.perkPoints);
            Assert.AreEqual(20, loaded.unlockPoints);
            CollectionAssert.Contains(loaded.unlockedIds, "archer");
        }
    }
}
