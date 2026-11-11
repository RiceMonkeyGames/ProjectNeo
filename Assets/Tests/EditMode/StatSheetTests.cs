using NUnit.Framework;
using ProjectNeo.Stats;

namespace ProjectNeo.Tests
{
    public class StatSheetTests
    {
        StatSheet sheet;

        [SetUp]
        public void SetUp()
        {
            sheet = new StatSheet();
            sheet.SetBase(StatType.Attack, 100f);
        }

        [Test]
        public void GetValue_WithoutModifiers_ReturnsBase()
        {
            Assert.AreEqual(100f, sheet.GetValue(StatType.Attack));
            Assert.AreEqual(0f, sheet.GetValue(StatType.Defense));
        }

        [Test]
        public void GetValue_AppliesFlatBeforePercent()
        {
            sheet.AddModifier(StatModifier.Flat(StatType.Attack, 20f), "sword");
            sheet.AddModifier(StatModifier.Percent(StatType.Attack, 0.5f), "perk");

            Assert.AreEqual(180f, sheet.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void GetValue_SumsPercentModifiers()
        {
            sheet.AddModifier(StatModifier.Percent(StatType.Attack, 0.1f), "a");
            sheet.AddModifier(StatModifier.Percent(StatType.Attack, 0.2f), "b");

            Assert.AreEqual(130f, sheet.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void GetValue_IgnoresModifiersOfOtherStats()
        {
            sheet.AddModifier(StatModifier.Flat(StatType.Defense, 50f), "shield");

            Assert.AreEqual(100f, sheet.GetValue(StatType.Attack));
        }

        [Test]
        public void GetValue_NeverGoesBelowZero()
        {
            sheet.AddModifier(StatModifier.Percent(StatType.Attack, -2f), "curse");

            Assert.AreEqual(0f, sheet.GetValue(StatType.Attack));
        }

        [Test]
        public void RemoveModifiersFrom_RemovesOnlyThatSource()
        {
            object perkA = new object();
            object perkB = new object();
            sheet.AddModifier(StatModifier.Flat(StatType.Attack, 10f), perkA);
            sheet.AddModifier(StatModifier.Flat(StatType.Attack, 5f), perkB);

            int removed = sheet.RemoveModifiersFrom(perkA);

            Assert.AreEqual(1, removed);
            Assert.AreEqual(105f, sheet.GetValue(StatType.Attack), 0.001f);
        }

        [Test]
        public void Changed_FiresForTheAffectedStat()
        {
            StatType? changed = null;
            sheet.Changed += stat => changed = stat;

            sheet.AddModifier(StatModifier.Flat(StatType.Defense, 1f), "x");

            Assert.AreEqual(StatType.Defense, changed);
        }

        [Test]
        public void SetBases_SetsEveryListedStat()
        {
            sheet.SetBases(new[] { new StatValue(StatType.Defense, 7f), new StatValue(StatType.MoveSpeed, 3f) });

            Assert.AreEqual(7f, sheet.GetValue(StatType.Defense));
            Assert.AreEqual(3f, sheet.GetValue(StatType.MoveSpeed));
        }
    }
}
