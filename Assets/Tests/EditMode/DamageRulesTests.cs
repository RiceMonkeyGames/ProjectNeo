using NUnit.Framework;
using ProjectNeo.Combat;

namespace ProjectNeo.Tests
{
    public class DamageRulesTests
    {
        [TestCase(30f, 20f, 10f)]
        [TestCase(10f, 20f, 1f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(20f, 0f, 20f)]
        [TestCase(12.5f, 5.25f, 7.25f)]
        public void Damage_SubtractsDefense_WithMinimumOne(float attack, float defense, float expected)
        {
            Assert.AreEqual(expected, DamageRules.Calculate(attack, defense, 1f));
        }

        [Test]
        public void Damage_NonFiniteValues_DoNotCorruptHealth()
        {
            Assert.AreEqual(0f, DamageRules.Calculate(float.NaN, 10f, 1f));
            Assert.AreEqual(0f, DamageRules.Calculate(10f, float.PositiveInfinity, 1f));
        }
    }
}
