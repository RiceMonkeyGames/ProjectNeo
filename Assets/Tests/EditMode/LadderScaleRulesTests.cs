using NUnit.Framework;
using ProjectNeo.Siege;

namespace ProjectNeo.Tests
{
    /// <summary>성벽 높이에 따른 사다리 시간·기울기(SIEGE-21).</summary>
    public class LadderScaleRulesTests
    {
        [TestCase(5f, 5f, 1)]
        [TestCase(10f, 5f, 2)]
        [TestCase(15f, 5f, 3)]
        [TestCase(14.999f, 5f, 3)]
        [TestCase(3f, 3f, 1)]
        [TestCase(0f, 5f, 1)]
        [TestCase(5f, 0f, 1)]
        public void Storeys_RoundsWallTopToWholeStoreys_AtLeastOne(float top, float storey, int expected)
        {
            Assert.AreEqual(expected, LadderScaleRules.Storeys(top, storey));
        }

        [TestCase(1, 2.5f)]
        [TestCase(2, 5f)]
        [TestCase(3, 7.5f)]
        [TestCase(0, 2.5f)]
        public void ClimbSeconds_AreTwoAndAHalfSecondsPerStorey(int storeys, float expected)
        {
            Assert.AreEqual(expected, LadderScaleRules.ClimbSeconds(storeys, 2.5f), 0.0001f);
        }

        [TestCase(1, 3f)]
        [TestCase(2, 4.5f)]
        [TestCase(3, 6f)]
        public void RaiseSeconds_AreBasePlusPerExtraStorey(int storeys, float expected)
        {
            Assert.AreEqual(expected, LadderScaleRules.RaiseSeconds(storeys, 3f, 1.5f), 0.0001f);
        }

        [Test]
        public void FootRun_KeepsTheLeanAngle_AndGrowsWithHeight()
        {
            float low = LadderScaleRules.FootRun(6.1f, 75f);
            float high = LadderScaleRules.FootRun(16.1f, 75f);
            Assert.AreEqual(75f, UnityEngine.Mathf.Atan2(6.1f, low) * UnityEngine.Mathf.Rad2Deg, 0.01f);
            Assert.AreEqual(75f, UnityEngine.Mathf.Atan2(16.1f, high) * UnityEngine.Mathf.Rad2Deg, 0.01f);
            Assert.Greater(high, low);
            Assert.AreEqual(0f, LadderScaleRules.FootRun(0f, 75f));
            Assert.Greater(LadderScaleRules.FootRun(5f, 200f), 0f, "각도는 89도로 잘려 0이 되지 않는다.");
        }

        [Test]
        public void LadderState_RaiseSecondsCanChange_AndKeepProgress()
        {
            var state = new SiegeLadderState(3f, 2f);
            state.Tick(0.01f, true, false, false);
            state.Tick(1.5f, true, false, false);
            Assert.AreEqual(0.5f, state.RaiseProgress, 0.01f);
            state.ConfigureRaiseSeconds(6f);
            Assert.AreEqual(6f, state.RaiseSeconds);
            state.Tick(1.5f, true, false, false);
            Assert.AreEqual(0.75f, state.RaiseProgress, 0.01f);
            state.Tick(1.6f, true, false, false);
            Assert.AreEqual(LadderPhase.Up, state.Phase);
        }
    }
}
