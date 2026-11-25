using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Core;
using ProjectNeo.Town;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-36: 성 안 주민의 산책 지점·쉬는 시간·피신처 규칙.</summary>
    public sealed class TownRulesTests
    {
        sealed class FixedRandom : IRandomSource
        {
            readonly Queue<int> ints;
            readonly float value;
            public FixedRandom(float floatValue, params int[] values) { ints = new Queue<int>(values); value = floatValue; }
            public int NextInt(int maxExclusive) => ints.Count > 0 ? ints.Dequeue() % maxExclusive : 0;
            public float NextFloat() => value;
        }

        [Test]
        public void NextWaypoint_NeverRepeatsTheCurrentOne_AndCoversTheOthers()
        {
            var seen = new HashSet<int>();
            for (int roll = 0; roll < 4; roll++)
            {
                int next = TownRules.NextWaypoint(2, 5, new FixedRandom(0f, roll));
                Assert.AreNotEqual(2, next);
                seen.Add(next);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3, 4 }, seen);
        }

        [Test]
        public void NextWaypoint_HandlesNoCurrentSingleAndEmpty()
        {
            Assert.AreEqual(3, TownRules.NextWaypoint(-1, 5, new FixedRandom(0f, 3)), "처음에는 아무 지점이나");
            Assert.AreEqual(0, TownRules.NextWaypoint(0, 1, new FixedRandom(0f, 0)), "지점이 하나면 그 지점");
            Assert.AreEqual(-1, TownRules.NextWaypoint(-1, 0, new FixedRandom(0f)), "지점이 없으면 -1");
        }

        [Test]
        public void IdleSeconds_StaysBetweenMinAndMax_EvenWhenSwapped()
        {
            Assert.AreEqual(2f, TownRules.IdleSeconds(2f, 6f, new FixedRandom(0f)), 1e-4f);
            Assert.AreEqual(4f, TownRules.IdleSeconds(2f, 6f, new FixedRandom(0.5f)), 1e-4f);
            Assert.AreEqual(4f, TownRules.IdleSeconds(6f, 2f, new FixedRandom(0.5f)), 1e-4f);
        }

        [Test]
        public void NearestShelter_UsesFlatDistance_AndReturnsMinusOneWhenEmpty()
        {
            var shelters = new[] { new Vector3(10f, 0f, 0f), new Vector3(0f, 50f, 3f), new Vector3(-6f, 0f, 0f) };
            Assert.AreEqual(1, TownRules.NearestShelter(Vector3.zero, shelters), "높이는 거리에 넣지 않는다");
            Assert.AreEqual(0, TownRules.NearestShelter(new Vector3(8f, 0f, 0f), shelters));
            Assert.AreEqual(-1, TownRules.NearestShelter(Vector3.zero, new Vector3[0]));
        }
    }
}
