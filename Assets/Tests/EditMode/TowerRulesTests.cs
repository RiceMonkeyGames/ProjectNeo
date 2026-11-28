using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// 망루 규칙: 성벽 줄 안 배치·바꿔 끼울 성벽·처음 층 수(SIEGE-24), 위에는 망루 층만, 층별 방 짜임(SIEGE-14, SIEGE-29),
    /// 낮은 망루 지붕으로 내려가는 성벽 계단 방(SIEGE-24), 지붕 위 판정.
    /// </summary>
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
            issue = BastionRules.Check(layout, grid, outside, piece, cell, 0, level, elevation);
            return issue != PlacementIssue.None ? issue : TowerRules.Check(layout, grid, outside, piece, cell, 0, level, elevation);
        }

        void PlaceTower(Vector2Int cell, int tiers)
        {
            for (int i = 0; i < tiers; i++) Assert.IsTrue(layout.TryPlace(tower, cell, 0, out _));
        }

        // 닫힌 성벽 고리(x 2~7, z 2~7). 북쪽 줄은 z 7, 그 바깥(z 8~)이 성 바깥이다.
        void Ring(int storeys = 1)
        {
            for (int s = 0; s < storeys; s++)
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
            }
        }

        PlacementIssue InLine(Vector2Int cell, List<BuildPlacement> replaced = null)
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            return TowerRules.CheckInLine(layout, grid, outside, tower, cell, 0, replaced);
        }

        // SIEGE-24: the tower replaces two wall-line cells and sticks out two cells outward.
        [Test]
        public void Tower_GoesInTheWallLine_ReplacingTwoWallCells_AndStickingOutward()
        {
            Ring(3);
            var replaced = new List<BuildPlacement>();
            Assert.AreEqual(PlacementIssue.None, InLine(new Vector2Int(4, 7), replaced), "north wall row z 7 + outside row z 8");
            Assert.AreEqual(6, replaced.Count, "both three-storey wall columns go");
            for (int i = 0; i < replaced.Count; i++)
            {
                Assert.AreEqual(BuildPieceKind.Wall, replaced[i].Definition.Kind);
                Assert.IsTrue(replaced[i].Cell == new Vector2Int(4, 7) || replaced[i].Cell == new Vector2Int(5, 7));
                if (i > 0) Assert.GreaterOrEqual(replaced[i - 1].Level, replaced[i].Level, "top storey first");
            }
            Assert.AreEqual(PlacementIssue.None, InLine(new Vector2Int(7, 4)), "east wall, sticking out east");
            Assert.AreEqual(PlacementIssue.None, InLine(new Vector2Int(3, 1)), "south wall, sticking out south");
        }

        [Test]
        public void Tower_DoesNotStickIntoTheCourtyard_OrStandAwayFromTheWall()
        {
            Ring();
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, InLine(new Vector2Int(4, 6)), "wall row z 7 + courtyard row z 6");
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, InLine(new Vector2Int(4, 4)), "inside the courtyard");
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, InLine(new Vector2Int(4, 9)), "outside, not on the wall line");
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, InLine(new Vector2Int(1, 7)), "only one wall cell under it");
        }

        [Test]
        public void Tower_IsNotPlacedWhereAGroundStairLeansOnTheWall()
        {
            Ring();
            var stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", Vector2Int.one, Cell));
            // 계단 (4, 6)이 북쪽(회전 0)으로 올라 (4, 7) 성벽에 닿는다.
            Assert.IsTrue(layout.TryPlace(stairs, new Vector2Int(4, 6), 0, out _));
            Assert.AreEqual(PlacementIssue.Occupied, InLine(new Vector2Int(4, 7)), "the stair would lose its landing");
            Assert.AreEqual(PlacementIssue.None, InLine(new Vector2Int(5, 7)), "but the next pair of cells is fine");
        }

        [Test]
        public void Tower_DoesNotReplaceAFixedGateColumn()
        {
            Ring();
            var gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", Vector2Int.one, Cell));
            var fresh = new BuildLayout(grid);
            for (int x = 2; x <= 7; x++)
            {
                if (x == 4) Assert.IsTrue(fresh.TryPlace(gate, new Vector2Int(x, 7), 0, out _, true));
                else Assert.IsTrue(fresh.TryPlace(wall, new Vector2Int(x, 7), 0, out _));
                Assert.IsTrue(fresh.TryPlace(wall, new Vector2Int(x, 2), 0, out _));
            }
            for (int z = 3; z <= 6; z++)
            {
                Assert.IsTrue(fresh.TryPlace(wall, new Vector2Int(2, z), 0, out _));
                Assert.IsTrue(fresh.TryPlace(wall, new Vector2Int(7, z), 0, out _));
            }
            layout = fresh;
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, InLine(new Vector2Int(4, 7)));
            Assert.AreEqual(PlacementIssue.None, InLine(new Vector2Int(5, 7)));
        }

        [Test]
        public void IsStackOnTower_OnlyOnTheWholeRoofOfOneTower()
        {
            Walls(1, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 2);
            Assert.IsTrue(TowerRules.IsStackOnTower(layout, new Vector2Int(4, 4), new Vector2Int(2, 2)));
            Assert.IsFalse(TowerRules.IsStackOnTower(layout, new Vector2Int(5, 4), new Vector2Int(2, 2)), "half on the roof");
            Assert.IsFalse(TowerRules.IsStackOnTower(layout, new Vector2Int(3, 3), new Vector2Int(2, 2)), "walls, not a tower");
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

        // SIEGE-24: a new tower always gets its initial tiers (2); later tiers go on one at a time.
        [Test]
        public void Tiers_NewTowerGetsItsInitialTiers_StackingAddsOne_CappedByTheGrid()
        {
            tower.SetInitialTiers(2);
            Assert.AreEqual(2, TowerRules.Tiers(grid, tower, 0));
            Assert.AreEqual(1, TowerRules.Tiers(grid, tower, 2), "stacking on a tower adds one tier");
            tower.SetInitialTiers(4);
            Assert.AreEqual(3, TowerRules.Tiers(grid, tower, 0), "capped at the grid's three levels");
            Assert.AreEqual(1, TowerRules.Tiers(grid, wall, 0), "other pieces stack once");
        }

        // SIEGE-24: 3-storey walls beside a 2-tier tower lead down to its roof through a stair room in the touching wall cell.
        [Test]
        public void ThreeStoreyWallBesideALowerTowerRoof_GetsAStairRoomWithADoorAtTheRoof()
        {
            Walls(3, 2, 3, 6, 7);
            PlaceTower(new Vector2Int(4, 3), 2);
            Assert.IsTrue(StairRoomRules.TryGet(layout, null, null, new Vector2Int(3, 3), out StairRoom west), "west wall cell beside the tower");
            Assert.AreEqual(2, west.BaseLevel, "room from the 10m roof up");
            Assert.IsTrue(west.HasDoor(2, 1), "a door east onto the tower roof");
            Assert.IsTrue(StairRoomRules.TryGet(layout, null, null, new Vector2Int(6, 3), out StairRoom east), "east wall cell beside the tower");
            Assert.IsTrue(east.HasDoor(2, 3), "a door west onto the tower roof");
            Assert.IsFalse(StairRoomRules.TryGet(layout, null, null, new Vector2Int(2, 3), out _), "the far cell stays solid");
        }

        [Test]
        public void WallLevelWithTheTowerRoof_NeedsNoStairRoom()
        {
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 4));
            layout = new BuildLayout(grid);
            Walls(3, 2, 3, 6, 7);
            PlaceTower(new Vector2Int(4, 3), 3);
            Assert.IsFalse(StairRoomRules.TryGet(layout, null, null, new Vector2Int(3, 3), out _), "the walkway runs straight onto the roof");
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
            Assert.AreEqual(3, room.RiseFace, "the stair climbs from the outer wall toward the door cell (west)");
        }

        [Test]
        public void TopStorey_WithoutAWalkwayAtItsFloor_HasNoRoom()
        {
            Walls(2, 3, 4, 5, 6);
            PlaceTower(new Vector2Int(4, 4), 2);
            Assert.IsFalse(TowerRules.RoomOf(layout, grid, layout.Top(new Vector2Int(4, 4))).HasRoom, "a two-tier tower is level with the walkway");
        }

        // SIEGE-29: every storey from the lowest touching walkway floor up is a room; storeys below stay solid.
        [Test]
        public void TallTowerBesideOneStoreyWalls_HasRoomsFromTheWalkwayUp_AndADoorOnlyAtTheWalkwayStorey()
        {
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 4));
            layout = new BuildLayout(grid);
            Walls(1, 4, 5);
            for (int i = 0; i < 4; i++) Assert.IsTrue(layout.TryPlace(tower, new Vector2Int(4, 4), 0, out _));
            var cell = new Vector2Int(4, 4);
            Assert.IsFalse(TowerRules.RoomOf(layout, grid, layout.At(cell, 0)).HasRoom, "below the walkway stays solid");
            TowerRules.Interior first = TowerRules.RoomOf(layout, grid, layout.At(cell, 1));
            Assert.IsTrue(first.HasRoom);
            Assert.AreEqual(5f, first.FirstFloor, 1e-3f);
            Assert.IsTrue(first.HasDoor(2), "a door south, onto the walkway");
            for (int level = 2; level <= 3; level++)
            {
                TowerRules.Interior room = TowerRules.RoomOf(layout, grid, layout.At(cell, level));
                Assert.IsTrue(room.HasRoom, $"storey {level} is a room");
                Assert.AreEqual(0, room.Doors, $"no walkway at the floor of storey {level}");
                Assert.AreEqual(first.StairCell, room.StairCell, "the stair stacks in the same cell");
                Assert.AreEqual(first.RiseFace, room.RiseFace);
            }
        }

        [Test]
        public void ThreeStoreyTowerBetweenTwoStoreyWalls_HasRoomsOnlyFromTenMetres_WithADoorOnEachWalkwaySide()
        {
            Walls(2, 4, 5);
            foreach (int x in new[] { 4, 5 })
                for (int i = 0; i < 2; i++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 6), 0, out _));
            PlaceTower(new Vector2Int(4, 4), 3);
            var cell = new Vector2Int(4, 4);
            Assert.IsFalse(TowerRules.RoomOf(layout, grid, layout.At(cell, 0)).HasRoom);
            Assert.IsFalse(TowerRules.RoomOf(layout, grid, layout.At(cell, 1)).HasRoom, "5m is below both walkways");
            TowerRules.Interior top = TowerRules.RoomOf(layout, grid, layout.At(cell, 2));
            Assert.IsTrue(top.HasRoom);
            Assert.IsTrue(top.HasDoor(0) && top.HasDoor(2), "doors north and south");
            Assert.IsTrue(TowerRules.FaceDoorCell(layout, grid, layout.At(cell, 2), top.StairCell, 0, out Vector2Int north));
            Assert.AreNotEqual(top.StairCell, north, "the stair cell never gets a door");
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
