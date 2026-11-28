using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// SIEGE-40 분지 규칙: 성이 격자 남쪽 가장자리 너머에 있는 북쪽 성벽 한 줄(사용자 결정 2026-11-28).
    /// 6×4 격자, 성벽은 맨 안쪽 줄(z 0)의 x 1~4, 동·서 끝 한 칸씩은 탑 자리라 짓지 못한다.
    /// </summary>
    public class NorthWallBasinRulesTests
    {
        const int North = 1 << 0, East = 1 << 1, South = 1 << 2, West = 1 << 3;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(6, 4, 5f, 4, castleBehindSouthEdge: true, blockedSideCells: 1));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            layout = new BuildLayout(grid);
            for (int x = 1; x <= 4; x++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 0), 0, out _), $"wall at {x},0");
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test]
        public void SouthOfTheGrid_IsInside_AndOutsideSpreadsOnlyFromTheOtherEdges()
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 4; z++)
                {
                    bool isWall = z == 0 && x >= 1 && x <= 4;
                    Assert.AreEqual(!isWall, outside.Contains(new Vector2Int(x, z)), $"cell {x},{z}");
                }
            Assert.IsFalse(BattlementRules.IsOutside(grid, outside, new Vector2Int(2, -1)), "격자 남쪽 밖은 성 안");
            Assert.IsTrue(BattlementRules.IsOutside(grid, outside, new Vector2Int(2, 4)), "격자 북쪽 밖은 바깥");
            Assert.IsTrue(BattlementRules.IsOutside(grid, outside, new Vector2Int(-1, 0)), "격자 서쪽 밖은 바깥");
        }

        [Test]
        public void WallRow_GetsBattlementsOnlyOnTheEnemyFace()
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            Assert.AreEqual(North, BattlementRules.Faces(layout, grid, outside, new Vector2Int(2, 0)));
            // 끝 칸은 탑 자리(빈 바깥 칸)를 향한 면도 바깥이다. 그 면의 킷 꾸밈은 CastleKitDresser.closedSideCells가 뺀다.
            Assert.AreEqual(North | West, BattlementRules.Faces(layout, grid, outside, new Vector2Int(1, 0)));
            Assert.AreEqual(North | East, BattlementRules.Faces(layout, grid, outside, new Vector2Int(4, 0)));
        }

        [Test]
        public void BlockedSideCells_RejectPlacement_ButTheBandBetweenThemIsBuildable()
        {
            Assert.AreEqual(PlacementIssue.OutsideGrid, layout.Check(wall, new Vector2Int(0, 1), 0));
            Assert.AreEqual(PlacementIssue.OutsideGrid, layout.Check(wall, new Vector2Int(5, 2), 0));
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, new Vector2Int(2, 2), 0));
            Assert.IsFalse(grid.IsBuildable(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsBuildable(new Vector2Int(4, 3)));
        }

        [Test]
        public void CastleFootprint_TreatsTheBasinBehindTheWallAsInside()
        {
            var footprint = new CastleFootprint();
            footprint.Refresh(layout, grid, 1, new Vector2Int(0, 1));
            Assert.IsTrue(footprint.HasCastle);
            Assert.IsTrue(footprint.IsInsideCastle(new Vector2Int(2, -3)), "성벽 뒤 분지");
            Assert.IsTrue(footprint.IsWithinCastle(new Vector2Int(5, -1)), "탑 뒤쪽도 분지");
            Assert.IsFalse(footprint.IsInsideCastle(new Vector2Int(2, 2)), "성벽 앞 띠는 바깥");
            Assert.IsFalse(footprint.IsOutside(new Vector2Int(2, -1)));
            Assert.AreEqual(North, footprint.OutwardFaceMask(layout, new Vector2Int(2, 0)));
        }

        [Test]
        public void WithoutTheFlag_OffGridIsStillOutside_LikeTheRingCastle()
        {
            var ring = Track(BuildGridDefinition.Create(6, 4, 5f, 4));
            var ringLayout = new BuildLayout(ring);
            Assert.IsTrue(ringLayout.TryPlace(wall, new Vector2Int(0, 0), 0, out _), "끝 칸도 지을 수 있다");
            BattlementRules.CollectOutside(ringLayout, ring, outside);
            Assert.IsTrue(BattlementRules.IsOutside(ring, outside, new Vector2Int(2, -1)));
        }
    }
}
