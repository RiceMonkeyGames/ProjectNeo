using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
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
            // 레벨 1→2에 100, 2→3에 150. 레벨당 퍽 포인트 1.
            config = MetaConfig.Create(100, 50, 1);
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
        public void LegacyUnlockedHero_StaysUnlocked()
        {
            HeroDefinition archer = LockedHero("archer");
            Assert.IsFalse(progress.IsUnlocked(archer));

            progress.unlockedIds.Add("archer");

            Assert.IsTrue(progress.IsUnlocked(archer));
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
