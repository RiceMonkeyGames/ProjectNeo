using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class BuildLayoutTests
    {
        readonly List<Object> assets = new List<Object>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition stairs;
        BuildPieceDefinition gate;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }
        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(8, 8, 2.75f));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", new Vector2Int(1, 2)));
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", new Vector2Int(2, 1)));
            layout = new BuildLayout(grid);
        }
        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test]
        public void CellCoordinates_UseConfigurableScale_AndFloorNegativePositions()
        {
            Vector3 center = layout.Center(new Vector2Int(3, 4), Vector2Int.one);
            Assert.AreEqual(new Vector2Int(3, 4), layout.WorldToCell(center));
            Assert.AreEqual(new Vector2Int(-1, -1), layout.WorldToCell(new Vector3(-0.01f, 0f, -0.01f)));
            Assert.AreEqual(9.625f, center.x);
        }

        [TestCase(0, 1, 2)] [TestCase(1, 2, 1)] [TestCase(2, 1, 2)] [TestCase(3, 2, 1)]
        public void Rotation_ReservesTheEntireRotatedFootprint(int rotation, int width, int depth)
        {
            Assert.IsTrue(layout.TryPlace(stairs, new Vector2Int(2, 2), rotation, out var placement));
            Assert.AreEqual(new Vector2Int(width, depth), placement.Size);
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
                Assert.AreSame(placement, layout.At(new Vector2Int(2 + x, 2 + z)));
        }

        [Test]
        public void FailedPlacement_DoesNotChangeOccupancyOrConsumeId()
        {
            Assert.IsFalse(layout.TryPlace(stairs, new Vector2Int(7, 7), 0, out _));
            Assert.IsTrue(layout.TryPlace(wall, Vector2Int.zero, 0, out var first));
            Assert.AreEqual(1, first.Id);
            Assert.IsFalse(layout.TryPlace(wall, Vector2Int.zero, 0, out _));
            Assert.AreEqual(1, layout.Placements.Count);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void IndependentStairs_AreAllowed_AndConnectionUpdatesWhenWallChanges(int rotation)
        {
            Vector2Int cell = new Vector2Int(3, 3);
            Assert.IsTrue(layout.TryPlace(stairs, cell, rotation, out var placement));
            Assert.IsFalse(layout.HasLanding(placement));
            foreach (Vector2Int landing in BuildLayout.Landings(stairs.Footprint, cell, rotation))
                Assert.IsTrue(layout.TryPlace(wall, landing, 0, out _));
            Assert.IsTrue(layout.HasLanding(placement));
            BuildPlacement support = null;
            foreach (Vector2Int landing in BuildLayout.Landings(stairs.Footprint, cell, rotation)) support = layout.At(landing);
            layout.Remove(support.Id, true);
            Assert.IsFalse(layout.HasLanding(placement));
        }

        [Test]
        public void Gate_IsSingleAndFixed_AndItsSiteRemainsReservedAfterDestruction()
        {
            Assert.IsFalse(layout.TryPlace(gate, Vector2Int.zero, 0, out _));
            Assert.IsTrue(layout.TryPlace(gate, Vector2Int.zero, 0, out var first, true));
            Assert.IsFalse(layout.Remove(first.Id));
            Assert.IsTrue(layout.Remove(first.Id, true));
            Assert.AreEqual(PlacementIssue.ReservedGate, layout.Check(wall, Vector2Int.zero, 0));
            Assert.IsFalse(layout.TryPlace(gate, new Vector2Int(4, 4), 0, out _, true));
        }

        [TestCase(0f, 0f)] [TestCase(1f, 0f)] [TestCase(3f, 24f)] [TestCase(5f, 48f)]
        public void FallDamage_DependsOnHeightAndConfiguredSafeHeight(float height, float expected)
        {
            Assert.AreEqual(expected, FallDamageRules.Calculate(height, 1f, 12f));
        }
    }
}
