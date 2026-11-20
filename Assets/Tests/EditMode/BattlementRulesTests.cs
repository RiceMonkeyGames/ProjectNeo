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

        static void AssertSegment(BattlementSegment segment, float start, float end, float height)
        {
            Assert.AreEqual(start, segment.Start, 1e-4f);
            Assert.AreEqual(end, segment.End, 1e-4f);
            Assert.AreEqual(height, segment.Height, 1e-4f);
        }
    }
}
