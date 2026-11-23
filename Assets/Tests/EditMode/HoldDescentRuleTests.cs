using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class HoldDescentRuleTests
    {
        [Test]
        public void PathAlongWallTop_StaysAbove()
        {
            var corners = new[] { new Vector3(0f, 9f, 0f), new Vector3(4f, 9.2f, 0f), new Vector3(8f, 8f, 0f) };
            Assert.IsTrue(HoldDescentRule.StaysAbove(corners, 7.5f));
        }

        [Test]
        public void PathDownTheStairs_DoesNotStayAbove()
        {
            var corners = new[] { new Vector3(0f, 9f, 0f), new Vector3(4f, 9f, 0f), new Vector3(13f, 0f, 0f), new Vector3(0f, 0f, 12f) };
            Assert.IsFalse(HoldDescentRule.StaysAbove(corners, 7.5f));
        }

        [Test]
        public void PathThatDipsAndClimbsBack_DoesNotStayAbove()
        {
            var corners = new[] { new Vector3(0f, 9f, 0f), new Vector3(5f, 3f, 0f), new Vector3(10f, 9f, 0f) };
            Assert.IsFalse(HoldDescentRule.StaysAbove(corners, 7.5f));
        }

        [Test]
        public void EmptyOrMissingPath_StaysAbove()
        {
            Assert.IsTrue(HoldDescentRule.StaysAbove(new Vector3[0], 7.5f));
            Assert.IsTrue(HoldDescentRule.StaysAbove(null, 7.5f));
        }

        [Test]
        public void EndpointMayStayAbove_RejectsLowerLevelTargets_AndAllowsSameLevelWithinTolerance()
        {
            // Wall top at 3m, HoldMaxDescent 1.5m -> floorY = 1.5m.
            Assert.IsFalse(HoldDescentRule.EndpointMayStayAbove(0f, 1.5f), "ground enemy below 3m wall");
            Assert.IsFalse(HoldDescentRule.EndpointMayStayAbove(1.0f, 1.5f), "clearly below floorY");
            Assert.IsTrue(HoldDescentRule.EndpointMayStayAbove(1.3f, 1.5f), "within default 0.25m tolerance");
            Assert.IsTrue(HoldDescentRule.EndpointMayStayAbove(3.0f, 1.5f), "same wall-top level");
        }
    }
}
