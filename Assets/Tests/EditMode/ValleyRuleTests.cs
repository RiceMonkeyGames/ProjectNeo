using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-14 골짜기 규칙: 2층-1층-2층을 만들지 않고, 골짜기 자리는 양옆 높이까지 한 번에 쌓는다.</summary>
    public class ValleyRuleTests
    {
        readonly List<Object> assets = new List<Object>();
        BuildPieceDefinition wall;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        void Use(bool forbidValleys)
        {
            var grid = Track(BuildGridDefinition.Create(9, 9, 5f, 3, forbidValleys));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, 5f));
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        BuildPlacement Place(int x, int z)
        {
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(x, z), 0, out BuildPlacement placement), $"place at {x},{z}");
            return placement;
        }

        [Test]
        public void ValleySpot_IsFilledToTheNeighbourHeight()
        {
            Use(true);
            Place(2, 4); Place(2, 4);
            Place(4, 4); Place(4, 4);
            var cell = new Vector2Int(3, 4);
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, cell, 0));
            Assert.AreEqual(2, layout.ValleyFill(wall, cell, 0), "a collapsed two-storey gap is rebuilt two storeys at once");
        }

        [Test]
        public void StackingThatLeavesANeighbourInAValley_IsRefused()
        {
            Use(true);
            Place(2, 4); Place(2, 4);
            Place(3, 4);
            Place(4, 4);
            Assert.AreEqual(PlacementIssue.Valley, layout.Check(wall, new Vector2Int(4, 4), 0));
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, new Vector2Int(3, 4), 0), "raising the middle one is fine");
        }

        [Test]
        public void RemovingTheMiddleTopOfAThreeHighRow_WouldMakeAValley()
        {
            Use(true);
            BuildPlacement middle = null, end = null;
            for (int x = 2; x <= 4; x++)
            {
                Place(x, 4);
                BuildPlacement top = Place(x, 4);
                if (x == 3) middle = top;
                if (x == 4) end = top;
            }
            Assert.IsTrue(layout.RemovalMakesValley(middle));
            Assert.IsFalse(layout.RemovalMakesValley(end));
        }

        [Test]
        public void WithoutTheRule_ValleysAreAllowed()
        {
            Use(false);
            Place(2, 4); Place(2, 4);
            Place(3, 4);
            Place(4, 4);
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, new Vector2Int(4, 4), 0));
            Assert.AreEqual(1, layout.ValleyFill(wall, new Vector2Int(3, 4), 0));
        }
    }
}
