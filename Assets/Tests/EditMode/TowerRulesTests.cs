using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-14 망루 규칙: 바깥에 성벽과 맞닿게, 한 층 높게 자동 적층, 위에는 망루 층만, 맨윗층 방 짜임, 지붕 위 판정.</summary>
    public class TowerRulesTests
    {
        const float Cell = 5f;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition tower;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, Cell));
            tower = Track(BuildPieceDefinition.Create(BuildPieceKind.Tower, "tower", new Vector2Int(2, 2), Cell, 400f));
            tower.SetOccupantModifiers(StatModifier.Percent(StatType.AttackRange, 0.2f), StatModifier.Percent(StatType.Attack, 0.2f));
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        void Walls(int storeys, params int[] xs)
        {
            foreach (int x in xs)
                for (int i = 0; i < storeys; i++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 3), 0, out _));
        }

        PlacementIssue Check(BuildPieceDefinition piece, Vector2Int cell)
        {
            PlacementIssue issue = layout.Check(piece, cell, 0, false, out int level, out float elevation);
            if (issue != PlacementIssue.None) return issue;
            BattlementRules.CollectOutside(layout, grid, outside);
            issue = BastionRules.Check(layout, grid, outside, null, piece, cell, 0, level, elevation);
            return issue != PlacementIssue.None ? issue : TowerRules.Check(layout, grid, outside, null, piece, cell, 0, level, elevation);
        }

        void PlaceTower(Vector2Int cell, int tiers)
        {
            for (int i = 0; i < tiers; i++) Assert.IsTrue(layout.TryPlace(tower, cell, 0, out _));
        }

        [Test]
        public void Tower_GoesOutsideAgainstAWall()
        {
            Walls(1, 3, 4, 5, 6);
            Assert.AreEqual(PlacementIssue.None, Check(tower, new Vector2Int(4, 4)));
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, Check(tower, new Vector2Int(8, 8)), "not touching any wall");
        }

        [Test]
        public void Tower_IsNotBuiltInsideTheCourtyard()
        {
            for (int x = 2; x <= 7; x++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 2), 0, out _));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 7), 0, out _));
            }
            for (int z = 3; z <= 6; z++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(2, z), 0, out _));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(7, z), 0, out _));
            }
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, Check(tower, new Vector2Int(3, 3)));
        }

        [Test]
        public void OnlyTowerTiersStackOnATower_AndATowerTierOnlyOnATower()
        {
            Walls(1, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 1);
            Assert.AreEqual(PlacementIssue.TowerStack, Check(wall, new Vector2Int(4, 4)), "no wall on a tower");
            Assert.AreEqual(PlacementIssue.None, Check(tower, new Vector2Int(4, 4)), "another tower tier is fine");
            foreach (var block in new[] { new Vector2Int(8, 8), new Vector2Int(9, 8), new Vector2Int(8, 9), new Vector2Int(9, 9) })
                Assert.IsTrue(layout.TryPlace(wall, block, 0, out _));
            Assert.AreEqual(PlacementIssue.TowerStack, Check(tower, new Vector2Int(8, 8)), "a tower tier does not sit on walls");
        }

        [Test]
        public void Tiers_StackOneStoreyAboveTheTallestTouchingWall_UpToTheGridLimit()
        {
            Walls(1, 3, 4, 5, 6);
            Assert.AreEqual(2, TowerRules.Tiers(layout, grid, tower, new Vector2Int(4, 4), 0, 0));
            Walls(1, 4);
            Assert.AreEqual(3, TowerRules.Tiers(layout, grid, tower, new Vector2Int(4, 4), 0, 0), "the taller wall wins");
            Walls(1, 4);
            Assert.AreEqual(3, TowerRules.Tiers(layout, grid, tower, new Vector2Int(4, 4), 0, 0), "capped at the grid's three levels");
            Assert.AreEqual(1, TowerRules.Tiers(layout, grid, wall, new Vector2Int(4, 4), 0, 0), "other pieces stack once");
        }

        [Test]
        public void TopStorey_IsARoomWithADoorOnTheWalkwaySide_AndAStairBesideIt()
        {
            Walls(2, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 3);
            TowerRules.Interior room = TowerRules.RoomOf(layout, grid, layout.Top(new Vector2Int(4, 4)));
            Assert.IsTrue(room.HasRoom);
            Assert.AreEqual(2, room.InnerFace, "the walkway is south of the tower");
            Assert.AreEqual(new Vector2Int(4, 4), room.DoorCell);
            Assert.AreEqual(new Vector2Int(5, 4), room.StairCell);
            Assert.AreEqual(1, room.RiseFace, "the stair climbs away from the door (east)");
        }

        [Test]
        public void TopStorey_WithoutAWalkwayAtItsFloor_HasNoRoom()
        {
            Walls(2, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 2);
            Assert.IsFalse(TowerRules.RoomOf(layout, grid, layout.Top(new Vector2Int(4, 4))).HasRoom, "a two-tier tower is level with the walkway");
        }

        [Test]
        public void StandingOn_IsTrueOnlyOnTheRoofOfAPieceWithOccupantModifiers()
        {
            Walls(2, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 3);
            Assert.IsTrue(TowerRules.StandingOn(layout, new Vector3(25f, 15f, 25f), 0.6f, out BuildPlacement top));
            Assert.AreEqual(tower, top.Definition);
            Assert.IsFalse(TowerRules.StandingOn(layout, new Vector3(25f, 10f, 25f), 0.6f, out _), "inside the top-storey room");
            Assert.IsFalse(TowerRules.StandingOn(layout, new Vector3(17.5f, 10f, 17.5f), 0.6f, out _), "on a wall walkway");
        }
    }
}
