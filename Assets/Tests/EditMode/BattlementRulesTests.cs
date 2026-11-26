using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-10 성가퀴 규칙: 바깥 판정, 세울 면, 면 위 덩어리.</summary>
    public class BattlementRulesTests
    {
        const int North = 1 << 0, East = 1 << 1, South = 1 << 2, West = 1 << 3;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition stairs;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(7, 7, 3f, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", Vector2Int.one));
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        void Place(BuildPieceDefinition piece, int x, int z, int rotation = 0) =>
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), rotation, out _), $"place {piece.DisplayName} at {x},{z}");

        // 1..5 칸을 두르는 성벽 고리. 안쪽은 2..4.
        void Ring()
        {
            for (int i = 1; i <= 5; i++)
            {
                Place(wall, i, 1); Place(wall, i, 5);
                if (i != 1 && i != 5) { Place(wall, 1, i); Place(wall, 5, i); }
            }
        }

        int Faces(int x, int z)
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            return BattlementRules.Faces(layout, grid, outside, new Vector2Int(x, z));
        }

        [Test]
        public void ClosedRing_GetsBattlementsOnOuterFacesOnly()
        {
            Ring();
            Assert.AreEqual(North, Faces(3, 5));
            Assert.AreEqual(South, Faces(3, 1));
            Assert.AreEqual(East, Faces(5, 3));
            Assert.AreEqual(West, Faces(1, 3));
            Assert.AreEqual(South | West, Faces(1, 1));
            Assert.AreEqual(North | East, Faces(5, 5));
            Assert.IsFalse(outside.Contains(new Vector2Int(3, 3)), "the courtyard is inside");
        }

        [Test]
        public void OpenWall_IsOutsideOnBothSides()
        {
            Place(wall, 2, 3); Place(wall, 3, 3); Place(wall, 4, 3);
            Assert.AreEqual(North | South, Faces(3, 3));
            Assert.AreEqual(North | South | West, Faces(2, 3));
        }

        [Test]
        public void GridEdge_CountsAsOutside()
        {
            Place(wall, 0, 3);
            Assert.AreEqual(North | East | South | West, Faces(0, 3));
        }

        [Test]
        public void OnlyTheTopTierGetsBattlements_AndHigherNeighboursCloseTheFace()
        {
            Ring();
            Place(wall, 3, 5); // 북쪽 가운데만 2층.
            BattlementRules.CollectOutside(layout, grid, outside);
            Assert.AreEqual(2, layout.ColumnHeight(new Vector2Int(3, 5)));
            Assert.AreEqual(North, Faces(3, 5), "the upper piece faces outside; lower neighbours are wall cells, not outside");
            Assert.AreEqual(North, Faces(2, 5), "the face toward the taller neighbour is closed by its wall");
        }

        // BUG-battlement-inner-faces: 기본 성 고리에 틈이 나도 안뜰은 늘 안쪽이다.
        readonly HashSet<Vector2Int> enclosed = new HashSet<Vector2Int>();

        void RemoveAt(int x, int z) => Assert.IsTrue(layout.Remove(layout.Top(new Vector2Int(x, z)).Id, true), $"remove {x},{z}");

        int FacesKeepingInside(int x, int z)
        {
            BattlementRules.CollectOutside(layout, grid, outside, enclosed);
            return BattlementRules.Faces(layout, grid, outside, new Vector2Int(x, z));
        }

        [Test]
        public void Enclosed_IsTheStartFootprint_RingAndCourtyard()
        {
            Ring();
            Place(stairs, 3, 4);
            BattlementRules.CollectEnclosed(layout, grid, enclosed);
            Assert.AreEqual(25, enclosed.Count, "the 5x5 ring with its courtyard");
            Assert.IsTrue(enclosed.Contains(new Vector2Int(3, 3)));
            Assert.IsTrue(enclosed.Contains(new Vector2Int(3, 4)), "a stair cell inside the ring");
            Assert.IsTrue(enclosed.Contains(new Vector2Int(3, 5)), "ring cells too");
            Assert.IsFalse(enclosed.Contains(new Vector2Int(0, 0)));
            Assert.IsFalse(enclosed.Contains(new Vector2Int(3, 6)));
        }

        [Test]
        public void GappedRing_WithoutStartCourtyard_LeaksBattlementsOntoInnerFaces()
        {
            Ring();
            RemoveAt(1, 5); RemoveAt(2, 5);
            Assert.AreEqual(East | West, Faces(1, 3), "plain flood fill: the gap lets the outside into the courtyard");
        }

        [Test]
        public void GappedRing_KeepsTheStartCourtyardInside()
        {
            Ring();
            BattlementRules.CollectEnclosed(layout, grid, enclosed);
            RemoveAt(1, 5); RemoveAt(2, 5);
            Assert.AreEqual(West, FacesKeepingInside(1, 4), "no battlement on the end facing the gap, none on the courtyard side");
            Assert.AreEqual(West, FacesKeepingInside(1, 3));
            Assert.AreEqual(North, FacesKeepingInside(3, 5), "the other end next to the gap stays open too");
            Assert.AreEqual(South, FacesKeepingInside(3, 1));
            Assert.IsFalse(outside.Contains(new Vector2Int(2, 5)), "a fallen ring cell is not outside");
            Assert.IsFalse(outside.Contains(new Vector2Int(2, 4)), "the courtyard behind the gap is not");
            Assert.IsFalse(outside.Contains(new Vector2Int(3, 3)));
        }

        [Test]
        public void StartCourtyard_DoesNotChangeAClosedRing_AndExtensionsFollowTheNormalRule()
        {
            Ring();
            BattlementRules.CollectEnclosed(layout, grid, enclosed);
            Place(wall, 3, 6); // 고리 바깥에 붙인 확장 칸.
            Assert.AreEqual(Faces(3, 1), FacesKeepingInside(3, 1));
            Assert.AreEqual(Faces(1, 3), FacesKeepingInside(1, 3));
            Assert.AreEqual(North | East | West, FacesKeepingInside(3, 6));
        }

        [Test]
        public void StairsGetNone_AndAStairTopAtTheSameHeightLeavesTheFaceOpen()
        {
            Place(wall, 3, 3);
            Place(stairs, 3, 2); // 계단 꼭대기 높이 = 성벽 위 높이.
            Assert.AreEqual(0, Faces(3, 2));
            Assert.AreEqual(North | East | West, Faces(3, 3));
        }

        [Test]
        public void EmptyCell_HasNoFaces()
        {
            Ring();
            Assert.AreEqual(0, Faces(3, 3));
        }

        [Test]
        public void Parapet_IsOneLowSegmentAcrossTheEdge()
        {
            var settings = Track(BattlementSettings.Create());
            var segments = new List<BattlementSegment>();
            BattlementRules.Segments(BattlementStyle.Parapet, settings, 3f, segments);
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual(0f, segments[0].Start, 1e-4f);
            Assert.AreEqual(3f, segments[0].End, 1e-4f);
            Assert.AreEqual(settings.ParapetHeight, segments[0].Height, 1e-4f);
        }

        [Test]
        public void Crenellated_PutsACrenelInTheMiddle_AndHalfCrenelsAtBothEnds()
        {
            var settings = Track(BattlementSettings.Create());
            var segments = new List<BattlementSegment>();
            BattlementRules.Segments(BattlementStyle.Crenellated, settings, 3f, segments);
            Assert.AreEqual(3, segments.Count, "continuous low base + two merlons");
            AssertSegment(segments[0], 0f, 3f, settings.ParapetHeight);
            AssertSegment(segments[1], 0.375f, 1.125f, settings.MerlonHeight);
            AssertSegment(segments[2], 1.875f, 2.625f, settings.MerlonHeight);
            // 가운데 빈틈(사다리 자리)은 사다리 폭 0.7m보다 넓다. 양 끝 반쪽 빈틈은 이웃 칸과 합쳐 같은 폭이 된다.
            Assert.AreEqual(settings.CrenelWidth, segments[2].Start - segments[1].End, 1e-4f);
            Assert.Greater(settings.CrenelWidth, 0.7f);
            Assert.AreEqual(settings.CrenelWidth, segments[1].Start + (3f - segments[2].End), 1e-4f);
        }

        [Test]
        public void Crenellated_PatternStaysCentredOnLongerEdges()
        {
            var settings = Track(BattlementSettings.Create());
            var segments = new List<BattlementSegment>();
            BattlementRules.Segments(BattlementStyle.Crenellated, settings, 6f, segments);
            foreach (BattlementSegment merlon in segments)
                if (merlon.Height > settings.ParapetHeight)
                    Assert.IsTrue(merlon.End <= 2.625f + 1e-4f || merlon.Start >= 3.375f - 1e-4f, "the middle stays a crenel");
        }

        [Test]
        public void NoneStyle_HasNoSegments()
        {
            var settings = Track(BattlementSettings.Create());
            var segments = new List<BattlementSegment> { new BattlementSegment(0f, 1f, 1f) };
            BattlementRules.Segments(BattlementStyle.None, settings, 3f, segments);
            Assert.AreEqual(0, segments.Count);
        }

        float Walkway(int x, int z, Vector2Int axis)
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            return BattlementRules.WalkwayWidth(layout, grid, outside, new Vector2Int(x, z), axis, 0.4f);
        }

        [Test]
        public void WalkwayWidth_SubtractsBattlementsOnlyWhereTheyStand()
        {
            Ring();
            // COMBAT-08: 고리 성벽 한 칸 폭 3m - 바깥 성가퀴 0.4m = 2.6m. 안쪽 면에는 성가퀴가 없다.
            Assert.AreEqual(2.6f, Walkway(3, 5, Vector2Int.up), 1e-4f);
            Assert.AreEqual(2.6f, Walkway(3, 5, Vector2Int.down), 1e-4f, "direction sign does not matter");
            Assert.AreEqual(2.6f, Walkway(5, 3, Vector2Int.right), 1e-4f);
            // 성벽을 따라 재면 이어진 칸이 다 들어가고, 양 끝 바깥 면 두께만 빠진다.
            Assert.AreEqual(15f - 0.8f, Walkway(3, 5, Vector2Int.right), 1e-4f);
        }

        [Test]
        public void WalkwayWidth_OpenWallHasBattlementsOnBothSides_AndEmptyCellsAreNotWalls()
        {
            Place(wall, 2, 3); Place(wall, 3, 3); Place(wall, 4, 3);
            Assert.AreEqual(2.2f, Walkway(3, 3, Vector2Int.up), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(Walkway(3, 4, Vector2Int.up)), "no piece: not a wall top");
            Assert.IsTrue(float.IsPositiveInfinity(BattlementRules.WalkwayWidth(layout, grid, outside, new Vector2Int(3, 3),
                new Vector2Int(1, 1), 0.4f)), "diagonal axis is not measured");
        }

        [Test]
        public void WalkwayWidth_OnGameCells_IsTheCellMinusItsBattlements()
        {
            // COMBAT-15: Game 칸 7.5m(SIEGE-18). 고리 성벽은 바깥 성가퀴 하나만 빠져 7.1m, 트인 성벽은 양쪽이 빠져 6.7m.
            grid = Track(BuildGridDefinition.Create(7, 7, 7.5f, 3));
            layout = new BuildLayout(grid);
            Ring();
            Assert.AreEqual(7.1f, Walkway(3, 5, Vector2Int.up), 1e-4f);
            Assert.AreEqual(7.1f, Walkway(1, 3, Vector2Int.left), 1e-4f);
            Assert.AreEqual(5 * 7.5f - 0.8f, Walkway(3, 5, Vector2Int.right), 1e-4f, "along the wall the whole row counts");

            layout = new BuildLayout(grid);
            Place(wall, 2, 3); Place(wall, 3, 3); Place(wall, 4, 3);
            Assert.AreEqual(6.7f, Walkway(3, 3, Vector2Int.up), 1e-4f);
        }

        [Test]
        public void CrenelCenters_OnGameCells_FiveCrenelsAtEvenSpacing_ContinuingIntoTheNextCell()
        {
            // COMBAT-15: 7.5m 면에는 가운데(3.75m)부터 1.5m마다 빈틈 다섯. 끝 빈틈에서 이웃 칸 첫 빈틈까지도 1.5m다.
            var settings = Track(BattlementSettings.Create());
            var centers = new List<float>();
            BattlementRules.CrenelCenters(BattlementStyle.Crenellated, settings, 7.5f, centers);
            CollectionAssert.AreEqual(new[] { 0.75f, 2.25f, 3.75f, 5.25f, 6.75f }, centers);
            Assert.AreEqual(1.5f, 7.5f - centers[centers.Count - 1] + centers[0], 1e-4f, "the gap across the cell joint");
        }

        static void AssertSegment(BattlementSegment segment, float start, float end, float height)
        {
            Assert.AreEqual(start, segment.Start, 1e-4f);
            Assert.AreEqual(end, segment.End, 1e-4f);
            Assert.AreEqual(height, segment.Height, 1e-4f);
        }
    }
}
