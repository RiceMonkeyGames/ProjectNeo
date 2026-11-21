using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>원거리 탄퍼짐(COMBAT-09).</summary>
    public class AimSpreadRulesTests
    {
        sealed class FixedRandom : IRandomSource
        {
            readonly float[] values;
            int next;
            public FixedRandom(params float[] values) { this.values = values; }
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => values[next++ % values.Length];
        }

        [Test]
        public void Radius_IsDistanceTimesTangent()
        {
            Assert.AreEqual(16f * Mathf.Tan(3f * Mathf.Deg2Rad), AimSpreadRules.Radius(16f, 3f), 0.0001f);
            Assert.AreEqual(0.838f, AimSpreadRules.Radius(16f, 3f), 0.001f, "기본 3°는 16m에서 약 0.8m.");
            Assert.AreEqual(0f, AimSpreadRules.Radius(16f, 0f));
            Assert.AreEqual(0f, AimSpreadRules.Radius(16f, -2f));
            Assert.AreEqual(0f, AimSpreadRules.Radius(0f, 3f));
            Assert.AreEqual(16f, AimSpreadRules.Radius(16f, 80f), 0.001f, "45°보다 큰 값은 45°로 자른다.");
        }

        [Test]
        public void Apply_StaysOnDiskPerpendicularToLine()
        {
            Vector3 origin = new Vector3(0f, 1f, 0f);
            Vector3 aim = new Vector3(6f, 3f, 14f);
            float radius = AimSpreadRules.Radius(Vector3.Distance(origin, aim), 3f);
            var random = new SeededRandom(7);
            for (int i = 0; i < 200; i++)
            {
                Vector3 point = AimSpreadRules.Apply(origin, aim, 3f, random);
                Vector3 offset = point - aim;
                Assert.LessOrEqual(offset.magnitude, radius + 0.0001f);
                Assert.AreEqual(0f, Vector3.Dot(offset, (aim - origin).normalized), 0.0001f, "조준선에 수직인 원 위다.");
            }
        }

        [Test]
        public void Apply_EdgeOfCircleWithMaxRoll()
        {
            Vector3 point = AimSpreadRules.Apply(Vector3.zero, Vector3.forward * 16f, 3f, new FixedRandom(0.999999f, 0.25f));
            Assert.AreEqual(AimSpreadRules.Radius(16f, 3f), Vector3.Distance(point, Vector3.forward * 16f), 0.001f);
            Assert.Greater(point.y, 0.8f, "각도 1/4 바퀴는 위쪽이다.");
        }

        [Test]
        public void Apply_NoSpreadOrNoRandom_KeepsAim()
        {
            Vector3 aim = new Vector3(2f, 1f, 9f);
            Assert.AreEqual(aim, AimSpreadRules.Apply(Vector3.zero, aim, 0f, new FixedRandom(0.5f)));
            Assert.AreEqual(aim, AimSpreadRules.Apply(Vector3.zero, aim, 3f, null));
        }

        [Test]
        public void Apply_VerticalLine_StillSpreads()
        {
            Vector3 point = AimSpreadRules.Apply(Vector3.zero, Vector3.down * 10f, 5f, new FixedRandom(0.999999f, 0f));
            Assert.Greater(Vector3.Distance(point, Vector3.down * 10f), 0.5f);
        }

        [Test]
        public void SameSeed_SameResult()
        {
            Vector3 a = AimSpreadRules.Apply(Vector3.zero, Vector3.forward * 16f, 3f, new SeededRandom(3));
            Vector3 b = AimSpreadRules.Apply(Vector3.zero, Vector3.forward * 16f, 3f, new SeededRandom(3));
            Assert.AreEqual(a, b);
        }
    }
}
