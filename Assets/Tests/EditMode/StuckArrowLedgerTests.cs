using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>박힌 화살 상한과 설정 값(ART-05).</summary>
    public class StuckArrowLedgerTests
    {
        [Test]
        public void Add_OverCap_EvictsOldestFirst()
        {
            var ledger = new StuckArrowLedger<string>();
            var evicted = new List<string>();
            ledger.Add("a", 2, evicted);
            ledger.Add("b", 2, evicted);
            Assert.IsEmpty(evicted);
            ledger.Add("c", 2, evicted);
            CollectionAssert.AreEqual(new[] { "a" }, evicted);
            Assert.AreEqual(2, ledger.Count);
            Assert.IsFalse(ledger.Contains("a"));
            Assert.IsTrue(ledger.Contains("b") && ledger.Contains("c"));
        }

        [Test]
        public void Remove_FreesASlot()
        {
            var ledger = new StuckArrowLedger<string>();
            var evicted = new List<string>();
            ledger.Add("a", 2, evicted);
            ledger.Add("b", 2, evicted);
            Assert.IsTrue(ledger.Remove("a"));
            Assert.IsFalse(ledger.Remove("a"));
            ledger.Add("c", 2, evicted);
            Assert.IsEmpty(evicted, "먼저 사라진 화살 자리는 비어 있어야 한다.");
            Assert.AreEqual(2, ledger.Count);
        }

        [Test]
        public void Add_SameItemTwice_KeepsOneEntry()
        {
            var ledger = new StuckArrowLedger<string>();
            var evicted = new List<string>();
            ledger.Add("a", 3, evicted);
            ledger.Add("a", 3, evicted);
            Assert.AreEqual(1, ledger.Count);
            Assert.IsEmpty(evicted);
        }

        [Test]
        public void Add_CapBelowOne_KeepsNewestOnly()
        {
            var ledger = new StuckArrowLedger<string>();
            var evicted = new List<string>();
            ledger.Add("a", 0, evicted);
            ledger.Add("b", 0, evicted);
            CollectionAssert.AreEqual(new[] { "a" }, evicted);
            Assert.IsTrue(ledger.Contains("b"));
        }

        [Test]
        public void Settings_DefaultsMatchDecision_AndClampInvalidValues()
        {
            var defaults = StuckArrowSettings.Create();
            var clamped = StuckArrowSettings.Create(-1f, 0, -0.5f);
            try
            {
                Assert.AreEqual(30f, defaults.Lifetime);
                Assert.AreEqual(100, defaults.MaxCount);
                Assert.Greater(defaults.Penetration, 0f);
                Assert.AreEqual(0.1f, clamped.Lifetime);
                Assert.AreEqual(1, clamped.MaxCount);
                Assert.AreEqual(0f, clamped.Penetration);
            }
            finally
            {
                Object.DestroyImmediate(defaults);
                Object.DestroyImmediate(clamped);
            }
        }
    }
}
