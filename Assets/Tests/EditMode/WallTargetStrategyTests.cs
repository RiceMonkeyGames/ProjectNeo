using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class WallTargetStrategyTests
    {
        [Test]
        public void Nearest_SelectsDistanceThenStableId_RegardlessOfInputOrder()
        {
            var strategy = NearestWallTargetStrategy.Create();
            try
            {
                var first = new[] { new WallTargetCandidate(9, 4f), new WallTargetCandidate(2, 4f), new WallTargetCandidate(1, 9f) };
                var reversed = new[] { first[2], first[1], first[0] };
                Assert.AreEqual(2, first[strategy.Select(first)].StructureId);
                Assert.AreEqual(2, reversed[strategy.Select(reversed)].StructureId);
            }
            finally { Object.DestroyImmediate(strategy); }
        }

        [Test]
        public void Nearest_RejectsEmptyAndInvalidDistances()
        {
            var strategy = NearestWallTargetStrategy.Create();
            try
            {
                Assert.AreEqual(-1, strategy.Select(new WallTargetCandidate[0]));
                Assert.AreEqual(-1, strategy.Select(new[] { new WallTargetCandidate(1, float.NaN),
                    new WallTargetCandidate(2, float.PositiveInfinity), new WallTargetCandidate(3, -1f) }));
                Assert.AreEqual(1, strategy.Select(new[] { new WallTargetCandidate(1, float.NaN), new WallTargetCandidate(2, 0f) }));
            }
            finally { Object.DestroyImmediate(strategy); }
        }
    }
}
