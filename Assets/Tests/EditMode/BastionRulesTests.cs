using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-11 보루: 성 바깥·성벽 접촉·사다리 자리·쌓기 규칙, 성가퀴 면, 무기가 향한 면.</summary>
    public class BastionRulesTests
    {
        const int North = 1 << 0, East = 1 << 1, South = 1 << 2, West = 1 << 3;
        readonly List<Object> assets = new List<Object>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition bastion;
        SiegeWeaponDefinition cannon;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(9, 9, 3f, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            bastion = Track(BuildPieceDefinition.Create(BuildPieceKind.Bastion, "bastion", Vector2Int.one, 3f, 150f, null,
                new BuildMountPoint("top", Vector3.up)));
            cannon = Track(SiegeWeaponDefinition.Create("cannon", "cannon", Vector3.one));
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        void Place(BuildPieceDefinition piece, int x, int z, int rotation = 0) =>
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), rotation, out _), $"place {piece.DisplayName} at {x},{z}");

        // 2..6 칸을 두르는 성벽 고리. 안쪽은 3..5, 바깥 한 칸(1, 7)과 가장자리(0, 8)가 비어 있다.
        void Ring()
        {
            for (int i = 2; i <= 6; i++)
            {
                Place(wall, i, 2); Place(wall, i, 6);
                if (i != 2 && i != 6) { Place(wall, 2, i); Place(wall, 6, i); }
            }
        }

        PlacementIssue Check(BuildPieceDefinition piece, int x, int z)
        {
            var cell = new Vector2Int(x, z);
            PlacementIssue issue = layout.Check(piece, cell, 0, false, out int level, out float elevation);
            if (issue != PlacementIssue.None) return issue;
            BattlementRules.CollectOutside(layout, grid, outside);
            return BastionRules.Check(layout, grid, outside, piece, cell, 0, level, elevation);
        }

        [Test]
        public void GroundBastion_OutsideAgainstWall_IsAllowed()
        {
            Ring();
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 4, 7), "north wall front");
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 1, 4), "west wall front");
        }

        [Test]
        public void Bastion_InsideCourtyard_IsRejected()
        {
            Ring();
            Assert.AreEqual(PlacementIssue.BastionNeedsWall, Check(bastion, 4, 5));
        }

        [Test]
        public void Bastion_NotTouchingWall_IsRejected()
        {
            Ring();
            Assert.AreEqual(PlacementIssue.BastionNeedsWall, Check(bastion, 4, 8));
            // 보루끼리만 이어지는 사슬도 안 된다.
            Place(bastion, 4, 7);
            Assert.AreEqual(PlacementIssue.BastionNeedsWall, Check(bastion, 4, 8));
        }

        // SIEGE-37: 사다리 자리가 없어져 운반 사다리가 서던 칸에도 보루를 짓는다.
        [Test]
        public void Bastion_InFrontOfAnyWallCell_IsAllowed_NoLadderLanes()
        {
            Ring();
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 4, 7));
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 3, 7));
        }

        [Test]
        public void Stacking_BastionOnlyOnBastion_AndMustTouchWallAtThatTier()
        {
            Ring();
            Place(bastion, 4, 7);
            Assert.AreEqual(PlacementIssue.BastionNeedsWall, Check(bastion, 4, 7), "no tier-2 wall beside it yet");
            Place(wall, 4, 6);
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 4, 7), "matches the raised wall");
            Assert.AreEqual(PlacementIssue.BastionStack, Check(wall, 4, 7), "nothing else on a bastion");
            Assert.AreEqual(PlacementIssue.BastionStack, Check(bastion, 3, 6), "a bastion does not stack on a wall");
            Assert.AreEqual(PlacementIssue.None, Check(wall, 3, 6), "walls still stack on walls");
        }

        [Test]
        public void GroundBastion_BesideTallerWall_IsAllowedButNotJoinedToWalkway()
        {
            Ring();
            Place(wall, 4, 6);
            var cell = new Vector2Int(4, 7);
            Assert.AreEqual(PlacementIssue.None, Check(bastion, 4, 7), "building it is allowed (warning only)");
            Assert.IsFalse(BastionRules.JoinsWalkway(layout, grid, cell, Vector2Int.one, 3f), "the two-tier wall top is 6m");
            Assert.IsTrue(BastionRules.JoinsWalkway(layout, grid, new Vector2Int(3, 7), Vector2Int.one, 3f),
                "the next one-tier wall is level with it");
            Place(bastion, 4, 7);
            Assert.IsTrue(BastionRules.JoinsWalkway(layout, grid, cell, Vector2Int.one, 6f), "a second bastion tier joins it");
        }

        [Test]
        public void BastionTop_JoinsWalkway_AndGetsOuterBattlements()
        {
            Ring();
            Place(bastion, 4, 7);
            BattlementRules.CollectOutside(layout, grid, outside);
            Assert.AreEqual(North | East | West, BattlementRules.Faces(layout, grid, outside, new Vector2Int(4, 7)));
            Assert.AreEqual(0, BattlementRules.Faces(layout, grid, outside, new Vector2Int(4, 6)) & North,
                "the wall face toward the bastion stays open");
        }

        [Test]
        public void FacingFaces_CombinesPieceAndWeaponRotation()
        {
            Ring();
            Assert.IsTrue(layout.TryPlace(bastion, new Vector2Int(4, 7), 0, out BuildPlacement north));
            Assert.IsTrue(layout.TryPlace(bastion, new Vector2Int(7, 4), 1, out BuildPlacement east));
            var mounts = new MountLayout(layout);
            Assert.AreEqual(0, mounts.FacingFaces(north));
            Assert.IsTrue(mounts.TryMount(cannon, north.Id, "top", out _));
            Assert.AreEqual(North, mounts.FacingFaces(north));
            Assert.IsTrue(mounts.TryMount(cannon, east.Id, "top", out _, 1));
            Assert.AreEqual(South, mounts.FacingFaces(east), "piece 90 + weapon 90 faces south");
        }

        [Test]
        public void WallWithoutMountPoint_RejectsWeapons()
        {
            Ring();
            var mounts = new MountLayout(layout);
            Assert.AreEqual(MountIssue.NoMountPoint, mounts.Check(cannon, layout.Top(new Vector2Int(4, 6)).Id, "top"));
        }
    }
}
