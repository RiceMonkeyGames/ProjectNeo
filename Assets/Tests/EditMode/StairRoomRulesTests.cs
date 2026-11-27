using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-27 3층 성벽 계단 방: 방 칸·방 층·문·계단 짜임, 꾸밈, 땅 위에만 놓는 계단.</summary>
    public class StairRoomRulesTests
    {
        const float Cell = 5f;
        const int North = 0, East = 1, South = 2, West = 3;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        readonly HashSet<int> unfinished = new HashSet<int>();
        readonly Dictionary<Vector2Int, float> fallen = new Dictionary<Vector2Int, float>();
        readonly Dictionary<Vector2Int, StairRoom> rooms = new Dictionary<Vector2Int, StairRoom>();
        readonly List<CastleKitPlacement> parts = new List<CastleKitPlacement>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition tower;
        BuildPieceDefinition stairs;
        CastleKitSettings settings;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, Cell));
            tower = Track(BuildPieceDefinition.Create(BuildPieceKind.Tower, "tower", Vector2Int.one, Cell));
            stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", Vector2Int.one, Cell));
            settings = Track(CastleKitSettings.Create());
            layout = new BuildLayout(grid);
            unfinished.Clear();
            fallen.Clear();
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        // 남북 z = 3 한 줄에 x = 3부터 층 수대로 쌓는다.
        void Row(params int[] storeys)
        {
            for (int i = 0; i < storeys.Length; i++)
                for (int s = 0; s < storeys[i]; s++)
                    Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3 + i, 3), 0, out _), $"place {3 + i}, storey {s + 1}");
        }

        StairRoom Room(int x)
        {
            Assert.IsTrue(StairRoomRules.TryGet(layout, unfinished, fallen, new Vector2Int(x, 3), out StairRoom room), $"room at {x}");
            return room;
        }

        bool HasRoom(int x) => StairRoomRules.TryGet(layout, unfinished, fallen, new Vector2Int(x, 3), out _);

        void Dress()
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            CastleKitRules.Collect(layout, grid, outside, unfinished, null, settings, parts, null, fallen, rooms);
        }

        IEnumerable<CastleKitPlacement> Of(CastleKitPiece piece) => parts.Where(p => p.Piece == piece);

        [Test]
        public void OneThreeOne_RoomsFromTheLowerWalkwayUp_WithDoorsOnBothSides()
        {
            Row(1, 3, 1);
            StairRoom room = Room(4);
            Assert.AreEqual(1, room.BaseLevel, "floor at 5m beside one-storey walkways");
            Assert.AreEqual(2, room.TopLevel);
            Assert.IsFalse(room.Contains(0), "the storey below the walkway stays solid");
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(1));
            Assert.AreEqual(0, room.Doors(2), "no walkway at 10m");
            Assert.AreEqual(1, Mathf.Abs(room.FirstFlightFace - room.FootFace) % 2, "the first flight runs along a side face");
            Assert.IsFalse(HasRoom(3) || HasRoom(5), "one-storey walls get no room");
        }

        [Test]
        public void OneThreeTwo_LowerDoorTowardTheOneStoreySide_UpperDoorTowardTheTwoStoreySide()
        {
            Row(1, 3, 2);
            StairRoom room = Room(4);
            Assert.AreEqual(1, room.BaseLevel);
            Assert.AreEqual(1 << West, room.Doors(1));
            Assert.AreEqual(1 << East, room.Doors(2));
            Assert.AreEqual(1, room.FootFace % 2, "the stair runs along the row so both doors sit at its ends");
            Assert.AreNotEqual(West, room.FirstFlightFace);
        }

        [Test]
        public void TwoThreeTwo_OnlyTheTopStoreyIsARoom()
        {
            Row(2, 3, 2);
            StairRoom room = Room(4);
            Assert.AreEqual(2, room.BaseLevel, "floor at 10m beside two-storey walkways only");
            Assert.AreEqual(2, room.TopLevel);
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(2));
        }

        [Test]
        public void ParallelRows_StairRoomsOnlyAtTheRowEnds_TheMiddleStaysSolid()
        {
            for (int x = 3; x <= 5; x++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 3), 0, out _));
                for (int s = 0; s < 3; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 4), 0, out _));
            }
            StairRoomRules.Collect(layout, unfinished, fallen, rooms);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 4), new Vector2Int(5, 4) }, rooms.Keys);
            Assert.IsTrue(rooms.Values.All(r => r.Doors(1) == 1 << South), "each end room opens onto the lower walkway");
        }

        [Test]
        public void JoinedThreeStoreyRow_TouchingAWalkwayOnlyAtItsEnds_GetsStairRoomsAtTheEnds()
        {
            Row(1, 3, 3, 3, 3, 1);
            StairRoomRules.Collect(layout, unfinished, fallen, rooms);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(4, 3), new Vector2Int(7, 3) }, rooms.Keys);
            Assert.AreEqual(1 << West, rooms[new Vector2Int(4, 3)].Doors(1));
            Assert.AreEqual(1 << East, rooms[new Vector2Int(7, 3)].Doors(1));
        }

        [Test]
        public void ThreeStoreyRow_WithoutAnyLowerWalkway_GetsNoRooms()
        {
            Row(3, 3, 3);
            StairRoomRules.Collect(layout, unfinished, fallen, rooms);
            CollectionAssert.IsEmpty(rooms);
        }

        [Test]
        public void ThreeStoreyGroup_WithoutTouchingEnds_GetsOneStairRoomAtItsFirstTouchingCell()
        {
            // 3층 3칸 줄의 가운데 칸만 낮은 통로(남쪽 1층)와 닿는다.
            Row(3, 3, 3);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 2), 0, out _));
            StairRoomRules.Collect(layout, unfinished, fallen, rooms);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(4, 3) }, rooms.Keys);
        }

        [Test]
        public void FourThreeStoreyWalls_StaySolidBetweenTheEndStairRooms()
        {
            for (int x = 3; x <= 6; x++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 3), 0, out _));
                for (int s = 0; s < 3; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, 4), 0, out _));
            }
            Dress();
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 4), new Vector2Int(6, 4) }, rooms.Keys);
            Assert.AreEqual(2 * 2 * 2, Of(CastleKitPiece.RoomStairs).Count(), "two flights per storey in the two end rooms only");
            Assert.AreEqual(2, Of(CastleKitPiece.TowerDoor).Count(), "a walkway door into each end room");
            Assert.IsFalse(parts.Any(p => (p.Cell.x == 4 || p.Cell.x == 5) && p.Cell.y == 4
                && (p.Piece == CastleKitPiece.TowerPanel || p.Piece == CastleKitPiece.RoomCollider)), "the middle cells stay solid walls");
            Assert.IsTrue(parts.Any(p => p.Cell == new Vector2Int(3, 4) && p.Piece == CastleKitPiece.TowerPanel
                && Mathf.Approximately(p.Position.x, 4 * Cell) && Mathf.Approximately(p.Position.y, Cell)), "the end room is walled off from the solid neighbour");
        }

        [Test]
        public void NoRoom_ForOneTwoSteps_UnfinishedStoreys_OrATowerAsTallAsTheWall()
        {
            Row(1, 2);
            Assert.IsFalse(HasRoom(4), "a two-storey wall keeps the outside stairs");
            layout = new BuildLayout(grid);
            Row(1);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out _));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out _));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out BuildPlacement top));
            unfinished.Add(top.Id);
            Assert.IsFalse(HasRoom(4), "an unfinished storey");
            unfinished.Clear();
            layout = new BuildLayout(grid);
            for (int s = 0; s < 3; s++) Assert.IsTrue(layout.TryPlace(tower, new Vector2Int(3, 3), 0, out _));
            for (int s = 0; s < 3; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out _));
            Assert.IsFalse(HasRoom(4), "a tower roof level with the walkway joins it directly");
        }

        // SIEGE-24 (user, 2026-11-27): a tower roof lower than a three-storey wall is reached through the wall's stair room.
        [Test]
        public void LowerTowerRoofBesideAThreeStoreyWall_GetsADoorAtTheRoofHeight()
        {
            Assert.IsTrue(layout.TryPlace(tower, new Vector2Int(3, 3), 0, out _));
            for (int s = 0; s < 3; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out _));
            StairRoom room = Room(4);
            Assert.AreEqual(1, room.BaseLevel, "rooms from the 5m tower roof up");
            Assert.IsTrue(room.HasDoor(1, West), "a door west onto the tower roof");
        }

        [Test]
        public void FallenNeighbour_KeepsTheDoorAsAnOpening()
        {
            Row(0, 3, 1);
            fallen[new Vector2Int(3, 3)] = Cell;
            StairRoom room = Room(4);
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(1));
            fallen.Clear();
            Assert.AreEqual(1 << East, Room(4).Doors(1));
        }

        [Test]
        public void UpperDoorsAtBothEnds_AreReachedThroughTheMiddleCorridor()
        {
            Row(2, 3, 2);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 4), 0, out _));
            StairRoom room = Room(4);
            Assert.AreEqual(1, room.BaseLevel, "the one-storey courtyard-side wall lowers the room floor to 5m");
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(2), "foot and far faces open on every room storey");
            Assert.AreEqual(1 << North, room.Doors(1));
        }

        [Test]
        public void UpperDoorsOnPerpendicularFaces_KeepOnlyOne()
        {
            // 위 방 층(10m) 문 자리가 동·북 두 면인데, 위층은 발치·맞은편 면으로만 드나든다.
            Row(1, 3, 2);
            for (int s = 0; s < 2; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 4), 0, out _));
            StairRoom room = Room(4);
            Assert.AreEqual(1 << West, room.Doors(1));
            int upper = room.Doors(2);
            Assert.IsTrue(upper == 1 << East || upper == 1 << North, $"one upper door, got {upper}");
        }

        [Test]
        public void StairWell_TwoFortyFiveDegreeFlights_HoleAboveTheSecondFlight()
        {
            StairWell well = StairWell.Create(new Vector2Int(0, 0), Cell, 10f, Cell, East, South, 0.31f, 1f);
            Assert.AreEqual(10f, well.FirstBottom.y, 1e-4f);
            Assert.AreEqual(12.5f, well.FirstTop.y, 1e-4f);
            Assert.AreEqual(12.5f, well.LandingHeight, 1e-4f);
            Assert.AreEqual(15f, well.SecondTop.y, 1e-4f);
            Assert.AreEqual(2.5f, Vector2.Distance(Flat(well.FirstBottom), Flat(well.FirstTop)), 1e-4f, "45 degrees");
            Assert.Greater(well.FirstBottom.x, well.FirstTop.x, "climbs away from the east foot");
            Assert.Less(well.FirstBottom.z, Cell * 0.5f, "first flight on the south half");
            Assert.AreEqual(well.FirstBottom.x, well.SecondTop.x, 1e-4f, "arrives above the start of the flight below");
            Assert.IsTrue(well.Hole.Contains(Flat(well.SecondTop) + new Vector2(-0.01f, 0f)) && well.Hole.Contains(Flat(well.SecondBottom) + new Vector2(0.01f, 0f)));
            Assert.IsFalse(well.Hole.Contains(Flat(well.FirstBottom)) || well.Hole.Contains(Flat(well.FirstTop)));
            var ceiling = new List<Rect>();
            well.CeilingAreas(ceiling);
            float area = ceiling.Sum(r => r.width * r.height);
            Assert.AreEqual(Cell * Cell - well.Hole.width * well.Hole.height, area, 1e-3f);
            Assert.IsTrue(ceiling.All(r => !r.Overlaps(well.Hole)));
            Assert.Greater(well.LandingHeight - 10f - settings.RoomSlabThickness, 2f, "a unit walks under the landing");
            StairWell roomy = StairWell.Create(new Vector2Int(0, 0), Cell, 10f, Cell, East, South, 0.31f, 1f, 0.6f);
            Assert.AreEqual(well.Hole.xMin - 0.6f, roomy.Hole.xMin, 1e-4f, "the opening reaches past the foot of the second flight");
            Assert.AreEqual(well.Hole.xMax, roomy.Hole.xMax, 1e-4f);
            StairWell narrow = StairWell.Create(new Vector2Int(0, 0), Cell, 10f, Cell, East, South, 0.31f, 1f, 0f, 1.46f);
            Assert.AreEqual(1.46f, narrow.FlightWidth, 1e-4f);
            Assert.AreEqual(0.31f + 0.73f, narrow.FirstBottom.z, 1e-4f, "the first flight hugs the south wall");
            Assert.AreEqual(Cell - 0.31f - 0.73f, narrow.SecondTop.z, 1e-4f, "the second flight hugs the north wall");
            Assert.AreEqual(Cell - 0.31f - 1.46f, narrow.Hole.yMin, 1e-4f, "the opening covers only the second flight");
            var floors = new List<Rect>();
            narrow.CeilingAreas(floors);
            Assert.IsTrue(floors.Any(r => r.Contains(new Vector2(2.5f, 2.5f))), "the middle corridor is floored on the storey above");
        }

        static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);

        [Test]
        public void Dressing_RoomGetsDoorsStairsAndColliders_AndNoOutsideStairsIntoIt()
        {
            Row(1, 3, 2, 1);
            Dress();
            var cell = new Vector2Int(4, 3);
            Assert.IsTrue(rooms.ContainsKey(cell));
            List<CastleKitPlacement> doors = Of(CastleKitPiece.TowerDoor).ToList();
            Assert.AreEqual(2, doors.Count);
            Assert.IsTrue(doors.Any(d => Mathf.Approximately(d.Position.y, Cell) && Mathf.Approximately(d.Position.x, 4 * Cell)), "lower door on the west face");
            Assert.IsTrue(doors.Any(d => Mathf.Approximately(d.Position.y, 2 * Cell) && Mathf.Approximately(d.Position.x, 5 * Cell)), "upper door on the east face");
            Assert.AreEqual(4, Of(CastleKitPiece.RoomStairs).Count(), "two half flights per room storey");
            Assert.AreEqual(4, Of(CastleKitPiece.RoomCollider).Count(p => Mathf.Abs(p.Tilt) > 1f), "a ramp under every flight");
            Assert.AreEqual(3, Of(CastleKitPiece.RoomLink).Count(), "a link across each landing and across the upper foot strip");
            List<CastleKitPlacement> posts = Of(CastleKitPiece.RoomPost).ToList();
            Assert.AreEqual(4, posts.Count, "two posts under every second flight");
            Assert.IsTrue(posts.All(p => Mathf.Approximately(p.Position.y, Cell) || Mathf.Approximately(p.Position.y, 2 * Cell)), "posts stand on the storey floor");
            Assert.IsFalse(parts.Any(p => p.Cell == new Vector2Int(5, 3) && p.Piece == CastleKitPiece.WallStairs), "no outside 2-3 stairs");
            Assert.IsTrue(parts.Any(p => p.Cell == new Vector2Int(6, 3) && p.Piece == CastleKitPiece.WallStairs), "the 1-2 step keeps them");
            Assert.IsFalse(Of(CastleKitPiece.Floor).Any(p => p.Cell == cell && Mathf.Approximately(p.Position.y, 3 * Cell)
                && Mathf.Approximately(p.StretchX, 1f) && Mathf.Approximately(p.StretchZ, 1f)), "the roof walkway has an opening");
            Assert.IsFalse(parts.Any(p => p.Cell == cell && p.Piece == CastleKitPiece.Panel && p.Position.y > Cell - 0.01f),
                "room storeys use collider panels");
        }

        [Test]
        public void CornerBetweenTwoStoreysAndARoomDoor_GetsASideEntryQuarterTurnStair()
        {
            // 2층(북) - 1층 모서리 - 3층 계단 방(동). 방 문이 모서리로 들어오므로 꺾임 계단이 그 쪽에서 시작하고, 옆으로 올라탄다.
            Row(1, 3);
            for (int s = 0; s < 2; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3, 4), 0, out _));
            Dress();
            var corner = new Vector2Int(3, 3);
            Assert.IsTrue(rooms[new Vector2Int(4, 3)].HasDoor(1, West));
            Assert.IsFalse(parts.Any(p => p.Cell == corner && (p.Piece == CastleKitPiece.WallStairs || p.Piece == CastleKitPiece.CornerStairs)),
                "neither a straight stair nor the side-boarded corner stair");
            CastleKitPlacement visual = parts.Single(p => p.Cell == corner && p.Piece == CastleKitPiece.CornerStairsOpen);
            Assert.AreEqual(new Vector3(3 * Cell, Cell, 3 * Cell), visual.Position, "starts on the door side and climbs to the north");
            Assert.AreEqual(2, parts.Count(p => p.Cell == corner && p.Piece == CastleKitPiece.RoomCollider && Mathf.Abs(p.Tilt) > 1f), "two ramps");
            Assert.AreEqual(1, parts.Count(p => p.Cell == corner && p.Piece == CastleKitPiece.RoomLink), "a link from the door floor onto the stair");
        }

        [Test]
        public void CornerWithAWalkwayOnTheSide_KeepsTheUsualCornerStair()
        {
            Row(1, 1);
            for (int s = 0; s < 2; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3, 4), 0, out _));
            Dress();
            Assert.AreEqual(1, parts.Count(p => p.Cell == new Vector2Int(3, 3) && p.Piece == CastleKitPiece.CornerStairs));
            CollectionAssert.IsEmpty(Of(CastleKitPiece.CornerStairsOpen));
        }

        [Test]
        public void GroundStairsOnly_RejectsStairsOnAWall()
        {
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 3, groundStairsOnly: true));
            layout = new BuildLayout(grid);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 4), 0, out _));
            Assert.AreEqual(PlacementIssue.StairsGroundOnly, layout.Check(stairs, new Vector2Int(4, 4), 0));
            Assert.AreEqual(PlacementIssue.None, layout.Check(stairs, new Vector2Int(4, 3), 0));
            var free = new BuildLayout(Track(BuildGridDefinition.Create(12, 12, Cell, 3)));
            Assert.IsTrue(free.TryPlace(wall, new Vector2Int(4, 4), 0, out _));
            Assert.AreEqual(PlacementIssue.None, free.Check(stairs, new Vector2Int(4, 4), 0), "other grids keep stairs on walls");
        }

        // SIEGE-19: x = 4가 성문 + 성벽 두 층(3층 성문 칸), 양옆 x = 3, 5는 side층 성벽.
        void GateColumn(int side)
        {
            BuildPieceDefinition gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", Vector2Int.one, Cell));
            Assert.IsTrue(layout.TryPlace(gate, new Vector2Int(4, 3), 0, out _, fixedGate: true));
            for (int s = 0; s < 2; s++) Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 3), 0, out _), $"gate storey {s + 2}");
            for (int s = 0; s < side; s++)
            {
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3, 3), 0, out _));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(5, 3), 0, out _));
            }
        }

        [Test]
        public void GateColumn_BesideTwoStoreyWalkways_RoomsFromTheWalkwayUp()
        {
            GateColumn(2);
            StairRoom room = Room(4);
            Assert.AreEqual(2, room.BaseLevel, "floor at 10m beside two-storey walkways");
            Assert.AreEqual(2, room.TopLevel);
            Assert.IsFalse(room.Contains(0) || room.Contains(1), "the gate and the storey above it stay solid");
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(2));
            Assert.IsTrue(StairRoomRules.IsTallWall(layout, unfinished, new Vector2Int(4, 3)), "no outside stairs up to the gate column");
            Dress();
            Assert.IsFalse(Of(CastleKitPiece.WallStairs).Any(), "the room replaces the outside stairs");
        }

        [Test]
        public void GateColumn_BesideOneStoreyWalkways_RoomsFromTheStoreyAboveTheGate()
        {
            GateColumn(1);
            StairRoom room = Room(4);
            Assert.AreEqual(1, room.BaseLevel, "like a wall column: the raised portcullis hides in the outer wall panel");
            Assert.AreEqual(2, room.TopLevel);
            Assert.IsFalse(room.Contains(0), "the gate storey is never a room");
            Assert.AreEqual((1 << East) | (1 << West), room.Doors(1));
            Assert.IsTrue(StairRoomRules.IsTallWall(layout, unfinished, new Vector2Int(4, 3)));
        }
    }
}
