using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-35 7.5m 칸(층 높이 5m) 킷 꾸밈: 늘림(면 방향만)과 짜맞춤(모듈 + 반 모듈), 모듈 크기 계단.</summary>
    public class CastleKitWideCellTests
    {
        const float Cell = 7.5f;
        const float Storey = 5f;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        readonly List<CastleKitPlacement> parts = new List<CastleKitPlacement>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition gate;
        CastleKitSettings settings;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(10, 10, Cell, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, Storey));
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", Vector2Int.one, Storey));
            settings = Track(CastleKitSettings.Create());
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        void Place(BuildPieceDefinition piece, int x, int z, bool fixedGate = false) =>
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), 0, out _, fixedGate), $"place at {x},{z}");

        void Collect(CastleKitLook look)
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            CastleKitRules.Collect(layout, grid, outside, new HashSet<int>(), new Dictionary<Vector2Int, int>(), settings, parts,
                kitLook: look);
        }

        IEnumerable<CastleKitPlacement> Of(CastleKitPiece piece) => parts.Where(p => p.Piece == piece);

        [Test]
        public void BattlementPieces_FollowTheCrenellatedFaces()
        {
            Place(wall, 2, 2);
            BattlementRules.CollectOutside(layout, grid, outside);
            CastleKitRules.Collect(layout, grid, outside, new HashSet<int>(), new Dictionary<Vector2Int, int>(), settings, parts);
            Assert.AreEqual(4, Of(CastleKitPiece.Battlement).Count(), "without a battlement rule every face is Battlements_01");
            Assert.AreEqual(0, Of(CastleKitPiece.Crenellation).Count());

            // SIEGE-20: crenellated (Lv2) faces take Battlements_03, the rest (Lv1 parapet, gun ports) keep Battlements_01.
            CastleKitRules.Collect(layout, grid, outside, new HashSet<int>(), new Dictionary<Vector2Int, int>(), settings, parts,
                crenellatedFace: (cell, face) => face != 0);
            Assert.AreEqual(3, Of(CastleKitPiece.Crenellation).Count());
            CastleKitPlacement low = Of(CastleKitPiece.Battlement).Single();
            Assert.AreEqual(CastleKitRules.FaceYaw(0), low.Yaw, 1e-3f, "the north face stays Battlements_01");
            Assert.IsTrue(Of(CastleKitPiece.Crenellation).All(p => Mathf.Approximately(p.StretchX, 1.5f)), "stretched like the other faces");
        }

        [Test]
        public void Stretch_WidensFacesAndFloorsOnly_StoreysStayModuleHigh()
        {
            Place(wall, 2, 2);
            Collect(CastleKitLook.Stretch);
            List<CastleKitPlacement> panels = parts.Where(p => p.Piece == CastleKitPiece.Panel || p.Piece == CastleKitPiece.PanelAccent).ToList();
            Assert.AreEqual(4, panels.Count, "one panel per face");
            Assert.IsTrue(panels.All(p => Mathf.Approximately(p.StretchX, 1.5f) && Mathf.Approximately(p.ScaleY, 1f)
                && Mathf.Approximately(p.StretchZ, 1f)), "stretched along the face, height and thickness unchanged");
            Assert.AreEqual(0, Of(CastleKitPiece.PanelHalf).Count());
            CastleKitPlacement floor = Of(CastleKitPiece.Floor).Single();
            Assert.AreEqual(1.5f, floor.StretchX, 1e-4f);
            Assert.AreEqual(1.5f, floor.StretchZ, 1e-4f);
            Assert.AreEqual(Storey, floor.Position.y, 1e-4f, "walkway at the 5m storey top");
            Assert.AreEqual(4, Of(CastleKitPiece.Pillar).Count(), "pillars only at the cell corners");
        }

        [Test]
        public void Compose_FillsEachFaceWithAModuleAndAHalf_AndAddsJointPillars()
        {
            Place(wall, 2, 2);
            Collect(CastleKitLook.Compose);
            Assert.AreEqual(4, parts.Count(p => p.Piece == CastleKitPiece.Panel || p.Piece == CastleKitPiece.PanelAccent));
            Assert.IsTrue(parts.Where(p => p.Piece == CastleKitPiece.Panel || p.Piece == CastleKitPiece.PanelAccent)
                .All(p => Mathf.Approximately(p.StretchX, 1f)), "full modules unscaled");
            List<CastleKitPlacement> halves = Of(CastleKitPiece.PanelHalf).ToList();
            Assert.AreEqual(4, halves.Count, "one half module per face");
            Assert.IsTrue(halves.All(p => Mathf.Approximately(p.StretchX, 1f)), "a 2.5m half fills the rest of 7.5m exactly");
            Assert.AreEqual(8, Of(CastleKitPiece.Pillar).Count(), "four corners and one joint per face");
            Assert.AreEqual(1, Of(CastleKitPiece.Floor).Count());
            Assert.AreEqual(3, Of(CastleKitPiece.FloorHalf).Count(), "two strips and a corner");
            Assert.AreEqual(4, Of(CastleKitPiece.Battlement).Count());
            Assert.AreEqual(4, Of(CastleKitPiece.BattlementHalf).Count());
            Assert.AreEqual(8, Of(CastleKitPiece.BattlementCap).Count());
        }

        [TestCase(CastleKitLook.Compose)]
        [TestCase(CastleKitLook.Stretch)]
        public void BothLooks_CentreTheUnstretchedGateFrame_WithQuarterFillers(CastleKitLook look)
        {
            Place(gate, 2, 2, fixedGate: true);
            Collect(look);
            CastleKitPlacement outer = Of(CastleKitPiece.GateOuter).Single();
            // 북쪽 면(z = 22.5)의 가운데 모듈 [16.25, 21.25]. 기준점은 로컬 +x(동쪽) 끝이다.
            Assert.AreEqual(21.25f, outer.Position.x, 1e-3f);
            Assert.AreEqual(1f, outer.StretchX, 1e-4f);
            List<CastleKitPlacement> fillers = Of(CastleKitPiece.PanelHalf)
                .Where(p => Mathf.Abs(p.Position.z - 22.5f) < 1e-3f && Mathf.Abs(Mathf.DeltaAngle(p.Yaw, 0f)) < 1f).ToList();
            Assert.AreEqual(2, fillers.Count, "a filler on each side of the frame");
            Assert.IsTrue(fillers.All(p => Mathf.Approximately(p.StretchX, 0.5f)), "1.25m fillers");
        }

        [Test]
        public void OutsideStair_KeepsTheModuleRun_LeavingFloorBeforeTheFoot()
        {
            Place(wall, 1, 1);
            Place(wall, 2, 1);
            Place(wall, 2, 1);
            Collect(CastleKitLook.Compose);
            CastleKitPlacement stair = Of(CastleKitPiece.WallStairs).Single();
            Assert.AreEqual(2 * Cell, stair.Position.x, 1e-3f, "top end against the higher column");
            Assert.AreEqual(Storey, stair.Position.y, 1e-3f);
            Assert.AreEqual(1f, stair.StretchX, 1e-4f, "45° over one module; 2.5m of walkway stays in front of the foot");
        }

        [Test]
        public void CornerStair_ReachesTheHigherCell_FromOneModuleBack()
        {
            Place(wall, 0, 1);
            Place(wall, 1, 1);
            Place(wall, 1, 2);
            Place(wall, 1, 2);
            Collect(CastleKitLook.Stretch);
            CastleKitPlacement corner = Of(CastleKitPiece.CornerStairs).Single(p => p.Cell == new Vector2Int(1, 1));
            Assert.AreEqual(2 * Cell - settings.ModuleSize, corner.Position.z, 1e-3f, "upper flight ends at the higher cell's edge");
            Assert.AreEqual(2 * Cell, corner.Position.x, 1e-3f, "pivot on the side away from the incoming walkway");
        }
    }
}
