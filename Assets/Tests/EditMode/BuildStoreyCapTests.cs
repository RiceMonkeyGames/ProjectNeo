using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// 조각별 층 제한(Q-59, SIEGE-18): Game 격자는 4층이지만 성벽·보루·계단·성문 칸은 3층까지만, 망루 층만 4층에 오른다.
    /// </summary>
    public class BuildStoreyCapTests
    {
        const float Cell = 7.5f, Storey = 5f;
        readonly List<Object> assets = new List<Object>();
        BuildPieceDefinition wall, gate, free;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, Storey));
            wall.SetMaxStorey(3);
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", Vector2Int.one, Storey));
            gate.SetMaxStorey(1);
            free = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "free wall", Vector2Int.one, Storey));
            layout = new BuildLayout(Track(BuildGridDefinition.Create(12, 12, Cell, 4)));
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test]
        public void CappedWall_StopsAtItsStorey_ThoughTheGridAllowsMore()
        {
            var cell = new Vector2Int(4, 4);
            for (int storey = 0; storey < 3; storey++) Assert.IsTrue(layout.TryPlace(wall, cell, 0, out _), "storey " + (storey + 1));
            Assert.AreEqual(PlacementIssue.StoreyCap, layout.Check(wall, cell, 0, false, out int level, out float elevation));
            Assert.AreEqual(3, level, "the preview still sits on top of the third storey");
            Assert.AreEqual(3 * Storey, elevation, 1e-4f);
            Assert.AreEqual(3, layout.ColumnHeight(cell));
        }

        [Test]
        public void UncappedPiece_FollowsTheGridLimit()
        {
            Assert.AreEqual(0, free.MaxStorey);
            var cell = new Vector2Int(4, 4);
            for (int storey = 0; storey < 4; storey++) Assert.IsTrue(layout.TryPlace(free, cell, 0, out _));
            Assert.AreEqual(PlacementIssue.Occupied, layout.Check(free, cell, 0), "the grid's four levels still apply");
        }

        [Test]
        public void CapAboveTheGridLimit_UsesTheGridLimit()
        {
            wall.SetMaxStorey(9);
            var flat = new BuildLayout(Track(BuildGridDefinition.Create(12, 12, Cell, 2)));
            var cell = new Vector2Int(2, 2);
            for (int storey = 0; storey < 2; storey++) Assert.IsTrue(flat.TryPlace(wall, cell, 0, out _));
            Assert.AreEqual(PlacementIssue.Occupied, flat.Check(wall, cell, 0));
        }

        [Test]
        public void GateColumn_IsCappedByThePiecesStackedOnIt()
        {
            var cell = new Vector2Int(5, 8);
            Assert.IsTrue(layout.TryPlace(gate, cell, 0, out _, true));
            Assert.IsTrue(layout.TryPlace(wall, cell, 0, out _), "gatehouse storey 2");
            Assert.IsTrue(layout.TryPlace(wall, cell, 0, out _), "gatehouse storey 3");
            Assert.AreEqual(PlacementIssue.StoreyCap, layout.Check(wall, cell, 0));
        }

        [Test]
        public void ValleyFill_NeverStacksPastTheCap()
        {
            var valleys = new BuildLayout(Track(BuildGridDefinition.Create(12, 12, Cell, 4, forbidValleys: true)));
            // 4층 이웃(제한 없는 조각) 사이의 빈 칸: 3층 제한 성벽으로는 골짜기를 메울 수 없다.
            for (int storey = 0; storey < 4; storey++)
            {
                Assert.IsTrue(valleys.TryPlace(free, new Vector2Int(3, 5), 0, out _));
                Assert.IsTrue(valleys.TryPlace(free, new Vector2Int(5, 5), 0, out _));
            }
            Assert.AreEqual(PlacementIssue.Valley, valleys.Check(wall, new Vector2Int(4, 5), 0));
            Assert.AreEqual(0, valleys.ValleyFill(wall, new Vector2Int(4, 5), 0));
            Assert.AreEqual(4, valleys.ValleyFill(free, new Vector2Int(4, 5), 0), "an uncapped piece fills all four storeys");
        }
    }
}
