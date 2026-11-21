using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>원거리 사거리 규칙(BUG-wall-archer-los).</summary>
    public class RangeRulesTests
    {
        [TestCase(9f, 1.25f)]      // 3층 성벽 위에서 아래로
        [TestCase(3f, 1.25f)]      // 1층 성벽 위에서 아래로
        [TestCase(1.5f, 1.25f)]    // 기준 높이 그대로
        [TestCase(1.49f, 1f)]      // 기준보다 낮은 단차
        [TestCase(0f, 1f)]         // 같은 높이
        [TestCase(-3f, 1f)]        // 위로 쏠 때
        public void HighGround_AddsBonusOnlyFromMinimumDrop(float drop, float expected)
        {
            Assert.AreEqual(expected, RangeRules.HighGroundMultiplier(drop, 1.5f, 0.25f), 0.0001f);
        }

        [Test]
        public void HighGround_IgnoresInvalidOrNegativeValues()
        {
            Assert.AreEqual(1f, RangeRules.HighGroundMultiplier(9f, 1.5f, -0.5f), "음수 보너스는 사거리를 줄이지 않는다.");
            Assert.AreEqual(1f, RangeRules.HighGroundMultiplier(9f, 1.5f, 0f));
            Assert.AreEqual(1f, RangeRules.HighGroundMultiplier(float.NaN, 1.5f, 0.25f));
            Assert.AreEqual(1f, RangeRules.HighGroundMultiplier(9f, 1.5f, float.PositiveInfinity));
            Assert.AreEqual(1.25f, RangeRules.HighGroundMultiplier(9f, -2f, 0.25f), 0.0001f, "음수 기준 높이는 0으로 본다.");
            Assert.AreEqual(1f, RangeRules.HighGroundMultiplier(-0.1f, -2f, 0.25f));
        }

        [Test]
        public void Profile_DefaultsGiveQuarterBonusFromOneAndHalfMeters()
        {
            CombatProfile profile = CombatProfile.Create();
            try
            {
                Assert.AreEqual(0.25f, profile.HighGroundRangeBonus);
                Assert.AreEqual(1.5f, profile.HighGroundMinimumDrop);
                Assert.AreEqual(profile.UpwardShotMinimumRise, profile.HighGroundMinimumDrop, "위로 쏠 때의 기준 높이와 같게 시작한다.");
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
