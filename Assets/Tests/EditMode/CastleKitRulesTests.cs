using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-14 성 킷 조립 규칙: 가려진 면, 공유 기둥, 무너진 칸, 성문 틀, 층 차이 계단.</summary>
    public class CastleKitRulesTests
    {
        const float Cell = 5f;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        readonly HashSet<int> unfinished = new HashSet<int>();
        readonly Dictionary<Vector2Int, int> breaches = new Dictionary<Vector2Int, int>();
        readonly HashSet<Vector2Int> rubble = new HashSet<Vector2Int>();
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
            grid = Track(BuildGridDefinition.Create(12, 12, Cell, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, Cell));
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", Vector2Int.one, Cell));
            settings = Track(CastleKitSettings.Create());
            layout = new BuildLayout(grid);
            unfinished.Clear();
            breaches.Clear();
            rubble.Clear();
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        BuildPlacement Place(BuildPieceDefinition piece, int x, int z, int rotation = 0, bool fixedGate = false)
        {
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), rotation, out BuildPlacement placement, fixedGate), $"place at {x},{z}");
            return placement;
        }

        void Collect()
        {
            BattlementRules.CollectOutside(layout, grid, outside);
            CastleKitRules.Collect(layout, grid, outside, unfinished, breaches, settings, parts, rubble);
        }

        IEnumerable<CastleKitPlacement> Of(CastleKitPiece piece) => parts.Where(p => p.Piece == piece);
        int Panels(Vector2Int cell) => parts.Count(p => p.Cell == cell && (p.Piece == CastleKitPiece.Panel || p.Piece == CastleKitPiece.PanelAccent));

        [Test]
        public void TwoWalls_HideTheSharedFace_AndShareJointPillars()
        {
            Place(wall, 3, 3);
            Place(wall, 4, 3);
            Collect();
            Assert.AreEqual(3, Panels(new Vector2Int(3, 3)), "north, south, west; east joins the neighbour");
            Assert.AreEqual(3, Panels(new Vector2Int(4, 3)));
            Assert.AreEqual(6, Of(CastleKitPiece.Pillar).Count(), "one pillar per used grid corner");
            Assert.AreEqual(2, Of(CastleKitPiece.Floor).Count());
        }

        [Test]
        public void CollapsedCell_GetsAVRuin_WhileNeighboursStayIntact()
        {
            Place(wall, 3, 3);
            Place(wall, 4, 3);
            Place(wall, 6, 3);
            var gap = new Vector2Int(5, 3);
            breaches[gap] = 0;
            Collect();
            Assert.AreEqual(2, Panels(new Vector2Int(4, 3)), "full north/south panels, nothing toward the gap");
            Assert.AreEqual(3, Panels(new Vector2Int(6, 3)), "full north/south/east panels, nothing toward the gap");
            Assert.AreEqual(3, Of(CastleKitPiece.Floor).Count(), "intact walkways stay whole");
            Assert.AreEqual(10, Of(CastleKitPiece.Pillar).Count(), "joint pillars stay on the intact walls");
            // V자: 양쪽 이음새에서 가운데로 내려오는 반쪽 둘(각각 면 두 개의 부서진 벽판 + 단면), 가운데 돌무더기.
            Assert.IsTrue(parts.Where(p => p.Piece == CastleKitPiece.RubbleTop || p.Piece == CastleKitPiece.BrokenTop
                || p.Piece == CastleKitPiece.RubblePile).All(p => p.Cell == gap), "the ruin sits in the collapsed cell");
            Assert.AreEqual(2, Of(CastleKitPiece.RubbleTop).Count());
            Assert.AreEqual(4, Of(CastleKitPiece.BrokenTop).Count());
            Assert.AreEqual(1, Of(CastleKitPiece.RubblePile).Count());
            CollectionAssert.AreEquivalent(new[] { 5 * Cell, 6 * Cell },
                Of(CastleKitPiece.RubbleTop).Select(p => Mathf.Round(p.Position.x * 100f) / 100f).ToArray(), "one half from each joint");
        }

        [Test]
        public void TwoStoreyCollapse_StretchesTheVToTheFullHeight()
        {
            Place(wall, 4, 3); Place(wall, 4, 3);
            Place(wall, 6, 3); Place(wall, 6, 3);
            breaches[new Vector2Int(5, 3)] = 0;
            Collect();
            Assert.IsTrue(Of(CastleKitPiece.BrokenTop).All(p => Mathf.Approximately(p.ScaleY, 2f)));
        }

        [Test]
        public void StairsBehindACollapsedWall_StayAndGetTheRubbleSectionOnTheirEnd()
        {
            var stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", Vector2Int.one, Cell));
            Place(wall, 4, 3);
            BuildPlacement middle = Place(wall, 5, 3);
            Place(wall, 6, 3);
            Place(stairs, 5, 2);
            Assert.IsTrue(layout.Remove(middle.Id, destroyed: true));
            var gap = new Vector2Int(5, 3);
            breaches[gap] = 0;
            Collect();
            // 이웃 성벽 쪽 V자 반쪽 둘에, 계단 끝에 붙는 단면 하나가 더해진다. 계단 쪽에는 부서진 벽판을 두지 않는다.
            Assert.AreEqual(3, Of(CastleKitPiece.RubbleTop).Count());
            Assert.AreEqual(4, Of(CastleKitPiece.BrokenTop).Count());
            // 계단 쪽 단면은 모델 +x가 남쪽 계단에서 칸 가운데(북쪽, +z)를 본다: yaw −90.
            CastleKitPlacement stairCap = Of(CastleKitPiece.RubbleTop).Single(p => Mathf.Abs(Mathf.DeltaAngle(p.Yaw, -90f)) < 1f);
            Assert.AreEqual(gap, stairCap.Cell);
            Assert.AreEqual(3 * Cell, stairCap.Position.z, 0.5f, "on the stairs' end face");
            Assert.AreEqual(1f, stairCap.ScaleY, 1e-4f, "the section matches the stair top height");
        }

        [Test]
        public void CollapsedBastion_LeavesOnlyRubble_AndTheWallBehindStaysWhole()
        {
            Place(wall, 5, 3);
            var bastionCell = new Vector2Int(5, 4);
            rubble.Add(bastionCell);
            Collect();
            Assert.AreEqual(4, Panels(new Vector2Int(5, 3)), "the wall face toward the fallen bastion keeps its panel");
            CollectionAssert.IsEmpty(Of(CastleKitPiece.RubbleTop), "no broken-wall section against the wall");
            CollectionAssert.IsEmpty(Of(CastleKitPiece.BrokenTop));
            Assert.AreEqual(bastionCell, Of(CastleKitPiece.RubblePile).Single().Cell);
        }

        [Test]
        public void RubbleCell_IsNotDressedOnceSomethingIsBuiltThere()
        {
            Place(wall, 5, 3);
            Place(wall, 5, 4);
            rubble.Add(new Vector2Int(5, 4));
            Collect();
            CollectionAssert.IsEmpty(Of(CastleKitPiece.RubblePile));
        }

        [Test]
        public void Tower_GetsADoorArrowSlitsAndAnInteriorStair_AndNoWalkwayStairs()
        {
            var tower = Track(BuildPieceDefinition.Create(BuildPieceKind.Tower, "tower", new Vector2Int(2, 2), Cell, 400f));
            foreach (int x in new[] { 3, 4, 5, 6 }) { Place(wall, x, 3); Place(wall, x, 3); }
            for (int tier = 0; tier < 3; tier++) Place(tower, 4, 4);
            Collect();
            Assert.AreEqual(new Vector2Int(4, 4), Of(CastleKitPiece.TowerDoor).Single().Cell, "a door from the walkway");
            Assert.AreEqual(new Vector2Int(5, 4), Of(CastleKitPiece.TowerPanel).Single().Cell, "the stair cell's walkway face is plain");
            Assert.AreEqual(6, Of(CastleKitPiece.TowerArrowslit).Count(), "the other top-storey faces");
            CastleKitPlacement stair = Of(CastleKitPiece.WallStairs).Single();
            Assert.AreEqual(new Vector2Int(5, 4), stair.Cell, "only the interior stair; walkways get none toward the tower");
            Assert.AreEqual(10f, stair.Position.y, 1e-3f, "from the room floor");
            Assert.AreEqual(new Vector2Int(5, 4), Of(CastleKitPiece.FloorHalf).Single().Cell, "the roof opens above the stair");
            Assert.AreEqual(4, Of(CastleKitPiece.TowerCap).Count());
            Assert.AreEqual(4, Of(CastleKitPiece.TowerCorbelCorner).Count());
            Assert.AreEqual(8, Of(CastleKitPiece.TowerCorbel).Count(), "every roof edge is above its neighbour");
        }

        [Test]
        public void TowerPillars_StandOnlyAtItsFourCorners()
        {
            var tower = Track(BuildPieceDefinition.Create(BuildPieceKind.Tower, "tower", new Vector2Int(2, 2), Cell, 400f));
            Place(tower, 4, 4);
            Collect();
            CollectionAssert.AreEquivalent(new[] { new Vector2(20f, 20f), new Vector2(30f, 20f), new Vector2(20f, 30f), new Vector2(30f, 30f) },
                Of(CastleKitPiece.Pillar).Select(p => new Vector2(p.Position.x, p.Position.z)).ToArray(), "no pillars at mid-face joints");
        }

        [Test]
        public void Gate_GetsOuterFrameOnItsRotationSide_AndInnerFrameOpposite()
        {
            Place(gate, 5, 5, rotation: 0, fixedGate: true);
            Collect();
            Assert.AreEqual(CastleKitRules.FaceYaw(0), Of(CastleKitPiece.GateOuter).Single().Yaw, 1e-3f);
            Assert.AreEqual(CastleKitRules.FaceYaw(2), Of(CastleKitPiece.GateInner).Single().Yaw, 1e-3f);
            Assert.AreEqual(1, Of(CastleKitPiece.Floor).Count(), "the gate top is a walkway");
            Assert.AreEqual(2, Panels(new Vector2Int(5, 5)), "passage side walls between the two frames");
        }

        [Test]
        public void TwoStoreyGate_KeepsAPassageCeiling_AndDressesTheUpperStorey()
        {
            Place(gate, 5, 5, rotation: 0, fixedGate: true);
            BuildPlacement upper = Place(wall, 5, 5, fixedGate: true);
            Assert.IsTrue(upper.IsFixed, "the storey over the gate cannot be dismantled");
            Collect();
            Assert.AreEqual(2, Of(CastleKitPiece.Floor).Count(), "passage ceiling plus the walkway on top");
            Assert.AreEqual(2 + 4, Panels(new Vector2Int(5, 5)), "passage sides plus four upper-storey faces");
        }

        [Test]
        public void TwoStoreyGate_GetsStairsFromBothNeighbouringWalkways()
        {
            Place(wall, 3, 5); Place(wall, 4, 5);
            Place(gate, 5, 5, rotation: 0, fixedGate: true);
            Place(wall, 5, 5, fixedGate: true);
            Place(wall, 6, 5); Place(wall, 7, 5);
            Collect();
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(4, 5), new Vector2Int(6, 5) },
                Of(CastleKitPiece.WallStairs).Select(p => p.Cell).ToArray());
        }

        [Test]
        public void EnclosedRing_InnerFacesGetReliefOnTheTopStorey_OuterFacesDoNot()
        {
            for (int i = 2; i <= 6; i++) { Place(wall, i, 2); Place(wall, i, 6); }
            for (int i = 3; i <= 5; i++) { Place(wall, 2, i); Place(wall, 6, i); }
            Collect();
            List<CastleKitPlacement> relief = Of(CastleKitPiece.InnerRelief).ToList();
            Assert.AreEqual(12, relief.Count, "one per courtyard-facing face");
            Assert.IsTrue(relief.All(p => Mathf.Abs(p.Position.y - (Cell - settings.InnerReliefDrop)) < 1e-3f));
            Assert.IsTrue(relief.All(p => p.Position.x > 2 * Cell - 1e-3f && p.Position.x < 7 * Cell + 1e-3f
                && p.Position.z > 2 * Cell - 1e-3f && p.Position.z < 7 * Cell + 1e-3f));
        }

        [Test]
        public void WallStair_IsCentredOnTheWalkway()
        {
            Place(wall, 4, 3);
            Place(wall, 5, 3);
            Place(wall, 5, 3);
            Collect();
            CastleKitPlacement stair = Of(CastleKitPiece.WallStairs).Single();
            // 동쪽(+x)으로 오르는 계단: 폭은 로컬 −z(= 월드 +z)로 펼쳐지므로 기준점이 칸 가운데에서 폭 절반만큼 −z에 있다.
            Assert.AreEqual(3.5f * Cell - settings.StairWidth * 0.5f, stair.Position.z, 1e-3f);
        }

        [Test]
        public void OneStoreyStep_GetsAStairOnTheLowerWalkway()
        {
            Place(wall, 3, 3);
            Place(wall, 4, 3);
            Place(wall, 5, 3);
            Place(wall, 5, 3);
            Collect();
            CastleKitPlacement stair = Of(CastleKitPiece.WallStairs).Single();
            Assert.AreEqual(new Vector2Int(4, 3), stair.Cell);
            Assert.AreEqual(Cell, stair.Position.y, 1e-3f, "starts on the lower walkway");
            Assert.AreEqual(5 * Cell, stair.Position.x, 1e-3f, "top end against the higher column");
        }

        [Test]
        public void ParallelRows_GetStairsOnlyAtTheRowEnds()
        {
            for (int x = 2; x <= 6; x++) { Place(wall, x, 3); Place(wall, x, 4); Place(wall, x, 4); }
            Collect();
            List<Vector2Int> cells = parts.Where(p => p.Piece == CastleKitPiece.WallStairs || p.Piece == CastleKitPiece.CornerStairs)
                .Select(p => p.Cell).ToList();
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 3), new Vector2Int(6, 3) }, cells);
        }

        [Test]
        public void Valley_GetsTwoParallelStairsOnOppositeHalves()
        {
            Place(wall, 3, 3); Place(wall, 3, 3);
            Place(wall, 4, 3);
            Place(wall, 5, 3); Place(wall, 5, 3);
            Collect();
            List<CastleKitPlacement> stairs = Of(CastleKitPiece.WallStairs).ToList();
            Assert.AreEqual(2, stairs.Count);
            Assert.IsTrue(stairs.All(s => s.Cell == new Vector2Int(4, 3)));
            // 폭 2.16m 띠의 가운데가 칸 가운데(z 17.5)에서 ±1.25m: 기준점은 띠 가장자리라 둘의 z가 다르고 겹치지 않는다.
            float[] z = stairs.Select(s => s.Position.z).OrderBy(v => v).ToArray();
            Assert.Greater(z[1] - z[0], settings.StairWidth - 1e-3f, "the two flights do not overlap");
        }

        [Test]
        public void CornerCell_GetsAQuarterTurnStairStartingFromTheIncomingWalkway()
        {
            Place(wall, 3, 3);
            Place(wall, 4, 3);
            Place(wall, 4, 4); Place(wall, 4, 4);
            Collect();
            CollectionAssert.IsEmpty(Of(CastleKitPiece.WallStairs));
            CastleKitPlacement stair = Of(CastleKitPiece.CornerStairs).Single();
            Assert.AreEqual(new Vector2Int(4, 3), stair.Cell);
            // 들어오는 통로는 서쪽, 높은 칸은 북쪽: 기준점은 동쪽·남쪽 바깥 모서리(25, 5, 15).
            Assert.AreEqual(new Vector3(5 * Cell, Cell, 3 * Cell), stair.Position);
        }

        [Test]
        public void UnfinishedPieces_AreNotDressed()
        {
            unfinished.Add(Place(wall, 3, 3).Id);
            Collect();
            CollectionAssert.IsEmpty(parts);
        }

        [Test]
        public void RingCorners_GetOneTurretEach_AtTheTopOfTheCorner()
        {
            // x 3~5, z 3~5 성벽 고리(가운데 (4, 4)가 안뜰). 바깥 모서리 넷에만 망루가 선다(SIEGE-19).
            for (int x = 3; x <= 5; x++)
                for (int z = 3; z <= 5; z++)
                    if (x != 4 || z != 4) Place(wall, x, z);
            Collect();
            List<CastleKitPlacement> turrets = Of(CastleKitPiece.CornerTurret).ToList();
            Assert.AreEqual(4, turrets.Count, "one per outer corner; inner and straight edges get none");
            float drop = Cell - CastleKitRules.CornerTurretDrop;
            foreach (Vector3 corner in new[] { new Vector3(15f, drop, 15f), new Vector3(30f, drop, 15f), new Vector3(15f, drop, 30f), new Vector3(30f, drop, 30f) })
                Assert.IsTrue(turrets.Any(t => Vector3.Distance(t.Position, corner) < 0.01f), $"turret at {corner}");

            Place(wall, 5, 5);
            Collect();
            turrets = Of(CastleKitPiece.CornerTurret).ToList();
            Assert.AreEqual(4, turrets.Count, "a raised corner keeps one turret");
            Assert.IsTrue(turrets.Any(t => Vector3.Distance(t.Position, new Vector3(30f, 2f * Cell - CastleKitRules.CornerTurretDrop, 30f)) < 0.01f),
                "it moves up to the new top");
        }
    }
}
