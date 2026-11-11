using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Stats;

namespace ProjectNeo.Tests
{
    public class HealthTests
    {
        StatSheet stats;
        Health health;

        [SetUp]
        public void SetUp()
        {
            stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, 100f);
            health = new Health(stats);
        }

        [TearDown] public void TearDown() => health.Dispose();

        [Test]
        public void Damage_ClampsToRemainingHealth_AndDeathFiresOnce()
        {
            int deaths = 0;
            health.Died += () => deaths++;
            Assert.AreEqual(25f, health.TakeDamage(25f));
            Assert.AreEqual(75f, health.Current);
            Assert.AreEqual(75f, health.TakeDamage(999f));
            Assert.AreEqual(0f, health.TakeDamage(10f));
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void InvalidDamage_DoesNotHealOrCorruptHealth()
        {
            health.TakeDamage(-10f);
            health.TakeDamage(float.NaN);
            health.TakeDamage(0f);
            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void MaxHealthChanges_ClampCurrent_AndDoNotResurrect()
        {
            stats.SetBase(StatType.MaxHealth, 60f);
            Assert.AreEqual(60f, health.Current);
            stats.SetBase(StatType.MaxHealth, 200f);
            Assert.AreEqual(60f, health.Current);
            stats.SetBase(StatType.MaxHealth, 0f);
            Assert.IsTrue(health.IsDead);
            stats.SetBase(StatType.MaxHealth, 100f);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Current);
        }
    }
}
