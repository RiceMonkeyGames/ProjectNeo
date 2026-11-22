using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-12 마치콜레이션 규칙: 통로 상자, 구멍 고르기, 성문 칸 자리.</summary>
    public class MachicolationRulesTests
    {
        readonly List<Object> assets = new List<Object>();
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        // 사격 자리는 바깥 면 0.85m 안, 면에서 안쪽 1.2m·바깥쪽 2m, 땅까지 3m.
        static MachicolationShaft GateShaft() => MachicolationShaft.FromFace(3f, 0.85f, 1.2f, 2f, 3f, 0.5f, 3f);

        [Test]
        public void Shaft_FromFace_MeasuresFromTheOuterFace()
        {
            MachicolationShaft shaft = GateShaft();
            Assert.AreEqual(1.5f, shaft.HalfWidth, 1e-5f);
            Assert.AreEqual(-0.35f, shaft.Inner, 1e-5f);
            Assert.AreEqual(2.85f, shaft.Outer, 1e-5f);
            Assert.AreEqual(-3.5f, shaft.Bottom, 1e-5f);
            Assert.AreEqual(3f, shaft.Top, 1e-5f);
        }

        [Test]
        public void Shaft_Contains_OnlyInsideTheBox()
        {
            MachicolationShaft shaft = GateShaft();
            Assert.IsTrue(shaft.Contains(new Vector3(0f, -1f, 1f)), "wall face region under the opening");
            Assert.IsTrue(shaft.Contains(new Vector3(1.4f, -3f, 2.5f)), "ground just outside the gate");
            Assert.IsFalse(shaft.Contains(new Vector3(1.6f, -1f, 1f)), "beside the shaft");
            Assert.IsFalse(shaft.Contains(new Vector3(0f, -1f, -0.5f)), "deeper inside (door side)");
            Assert.IsFalse(shaft.Contains(new Vector3(0f, -1f, 3f)), "too far out");
            Assert.IsFalse(shaft.Contains(new Vector3(0f, -3.6f, 1f)), "under the ground");
        }

        [Test]
        public void Shaft_IsBelow_NeedsFeetNearTheGround()
        {
            MachicolationShaft shaft = GateShaft();
            Assert.IsTrue(shaft.IsBelow(new Vector3(0.5f, -3f, 1.5f), 1.5f), "enemy at the gate base");
            Assert.IsFalse(shaft.IsBelow(new Vector3(0f, 0f, 0.2f), 1.5f), "defender on the wall top");
            Assert.IsFalse(shaft.IsBelow(new Vector3(0f, -3f, 4f), 1.5f), "enemy farther out");
        }

        [Test]
        public void Rank_KeepsFreeNearOpeningsWithEnemiesBelow_NearestFirst()
        {
            var candidates = new List<MachicolationCandidate>
            {
                new MachicolationCandidate(64f, true, true, true),   // 0: 8m
                new MachicolationCandidate(9f, true, true, true),    // 1: 3m
                new MachicolationCandidate(4f, true, false, true),   // 2: taken
                new MachicolationCandidate(1f, true, true, false),   // 3: no enemy below
                new MachicolationCandidate(1f, false, true, true),   // 4: other level
                new MachicolationCandidate(400f, true, true, true),  // 5: 20m, out of radius
                new MachicolationCandidate(9f, true, true, true),    // 6: 3m, tie with 1
            };
            var order = new List<int>();
            MachicolationRules.Rank(candidates, 12f, order);
            CollectionAssert.AreEqual(new[] { 1, 6, 0 }, order);
        }

        [Test]
        public void Rank_ZeroRadius_ReturnsNothing()
        {
            var order = new List<int> { 3 };
            MachicolationRules.Rank(new List<MachicolationCandidate> { new MachicolationCandidate(0f, true, true, true) }, 0f, order);
            Assert.IsEmpty(order);
        }

        [Test]
        public void ReleaseAndClaimRules()
        {
            Assert.IsTrue(MachicolationRules.ShouldRelease(false, true), "opening gone");
            Assert.IsTrue(MachicolationRules.ShouldRelease(true, false), "no enemy below");
            Assert.IsFalse(MachicolationRules.ShouldRelease(true, true));

            Assert.IsTrue(MachicolationRules.IsClaimFree(false, false, false), "unclaimed");
            Assert.IsTrue(MachicolationRules.IsClaimFree(true, true, true), "own claim");
            Assert.IsTrue(MachicolationRules.IsClaimFree(true, false, false), "claimant dead or disabled");
            Assert.IsFalse(MachicolationRules.IsClaimFree(true, false, true), "claimed by another live unit");
        }

        [Test]
        public void PassesThrough_NeedsGatePieceAndBothPointsInsideTheShaft()
        {
            Assert.IsTrue(MachicolationRules.PassesThrough(true, true, true));
            Assert.IsFalse(MachicolationRules.PassesThrough(false, true, true), "other wall piece or ground");
            Assert.IsFalse(MachicolationRules.PassesThrough(true, true, false), "line leaves through the wall body");
            Assert.IsFalse(MachicolationRules.PassesThrough(true, false, true), "line enters outside the shaft");
        }

        [Test]
        public void ArrowModule_IsForRangedAttackersOnly()
        {
            ArrowMachicolationAttack arrow = Track(ArrowMachicolationAttack.Create());
            Assert.IsFalse(arrow.CanUse(null));
        }

        // 1..6 × 1..5 성벽 고리. 남쪽 면 (3,1)·(4,1)이 2칸 성문.
        BuildLayout GateRing(out BuildGridDefinition grid, out BuildPieceDefinition wall)
        {
            grid = Track(BuildGridDefinition.Create(8, 7, 3f, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            BuildPieceDefinition gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", new Vector2Int(2, 1)));
            var layout = new BuildLayout(grid);
            Assert.IsTrue(layout.TryPlace(gate, new Vector2Int(3, 1), 0, out _, true), "gate");
            for (int x = 1; x <= 6; x++)
            {
                if (x != 3 && x != 4) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 1), 0, out _));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 5), 0, out _));
            }
            for (int z = 2; z <= 4; z++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(1, z), 0, out _));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(6, z), 0, out _));
            }
            return layout;
        }

        static List<MachicolationSpot> Spots(BuildLayout layout, BuildGridDefinition grid)
        {
            var outside = new HashSet<Vector2Int>();
            BattlementRules.CollectOutside(layout, grid, outside);
            var spots = new List<MachicolationSpot>();
            MachicolationLayout.Collect(layout, grid, outside, 0.85f, spots);
            return spots;
        }

        [Test]
        public void Layout_GateGetsOneOpeningPerCellOnItsOuterFace()
        {
            BuildLayout layout = GateRing(out BuildGridDefinition grid, out _);
            List<MachicolationSpot> spots = Spots(layout, grid);
            Assert.AreEqual(2, spots.Count);
            foreach (MachicolationSpot spot in spots)
            {
                Assert.AreEqual(2, spot.Face, "south face (outside)");
                Assert.AreEqual(Vector3.back, spot.Outward);
                Assert.AreEqual(3f, spot.Drop, 1e-4f);
            }
            Assert.AreEqual(new Vector3(10.5f, 3f, 3.85f), spots[0].StandPoint);
            Assert.AreEqual(new Vector3(13.5f, 3f, 3.85f), spots[1].StandPoint);
        }

        [Test]
        public void Layout_WallStackedOnGate_UsesTheTopSurface()
        {
            BuildLayout layout = GateRing(out BuildGridDefinition grid, out BuildPieceDefinition wall);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3, 1), 0, out BuildPlacement upper));
            Assert.AreEqual(1, upper.Level);
            List<MachicolationSpot> spots = Spots(layout, grid);
            MachicolationSpot stacked = spots.Find(spot => spot.Cell == new Vector2Int(3, 1));
            Assert.AreEqual(6f, stacked.StandPoint.y, 1e-4f);
            Assert.AreEqual(6f, stacked.Drop, 1e-4f);
        }

        [Test]
        public void Layout_NoGate_NoOpenings()
        {
            BuildGridDefinition grid = Track(BuildGridDefinition.Create(5, 5, 3f, 1));
            BuildPieceDefinition wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            var layout = new BuildLayout(grid);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(2, 2), 0, out _));
            Assert.IsEmpty(Spots(layout, grid));
        }
    }
}
