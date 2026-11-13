using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class MountLayoutTests
    {
        readonly List<Object> assets = new List<Object>();
        BuildPieceDefinition wall;
        BuildPieceDefinition tower;
        BuildPieceDefinition stairs;
        SiegeWeaponDefinition cannon;
        SiegeWeaponDefinition ballista;
        BuildLayout layout;
        MountLayout mounts;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            var grid = Track(BuildGridDefinition.Create(8, 8, 3f));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one,
                mountPoints: new[] { new BuildMountPoint("top", Vector3.up) }));
            tower = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "tower", Vector2Int.one,
                mountPoints: new[] { new BuildMountPoint("left", Vector3.up), new BuildMountPoint("right", Vector3.up) }));
            stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", new Vector2Int(1, 2)));
            cannon = Track(SiegeWeaponDefinition.Create("cannon", "cannon", Vector3.one));
            ballista = Track(SiegeWeaponDefinition.Create("ballista", "ballista", new Vector3(1f, 1f, 2f)));
            layout = new BuildLayout(grid);
            mounts = new MountLayout(layout);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test]
        public void WeaponDefinition_MaxHealth_DefaultsAndClampsToAtLeastOne()
        {
            Assert.AreEqual(100f, cannon.MaxHealth);
            Assert.AreEqual(150f, Track(SiegeWeaponDefinition.Create("a", "a", Vector3.one, null, 150f)).MaxHealth);
            Assert.AreEqual(1f, Track(SiegeWeaponDefinition.Create("b", "b", Vector3.one, null, -5f)).MaxHealth);
        }

        [Test]
        public void Mount_OccupiesOnlyItsPoint_AndDoesNotTouchFloorOccupancy()
        {
            Assert.IsTrue(layout.TryPlace(wall, Vector2Int.zero, 0, out var support));
            Assert.IsTrue(mounts.TryMount(cannon, support.Id, "top", out var weapon));
            Assert.AreSame(weapon, mounts.At(support.Id, "top"));
            Assert.AreSame(support, layout.At(Vector2Int.zero));
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, Vector2Int.right, 0));
        }

        [Test]
        public void FailedMount_ReportsTheReason_AndDoesNotConsumeId()
        {
            Assert.IsTrue(layout.TryPlace(wall, Vector2Int.zero, 0, out var support));
            Assert.IsTrue(layout.TryPlace(stairs, new Vector2Int(2, 2), 0, out var ramp));
            Assert.AreEqual(MountIssue.NoWeapon, mounts.Check(null, support.Id, "top"));
            Assert.AreEqual(MountIssue.NoSupport, mounts.Check(cannon, 999, "top"));
            Assert.AreEqual(MountIssue.NoMountPoint, mounts.Check(cannon, support.Id, "side"));
            Assert.AreEqual(MountIssue.NoMountPoint, mounts.Check(cannon, support.Id, null));
            Assert.AreEqual(MountIssue.NoMountPoint, mounts.Check(cannon, ramp.Id, "top"));
            Assert.IsFalse(mounts.TryMount(cannon, ramp.Id, "top", out _));
            Assert.IsTrue(mounts.TryMount(cannon, support.Id, "top", out var first));
            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(MountIssue.Occupied, mounts.Check(ballista, support.Id, "top"));
            Assert.IsFalse(mounts.TryMount(ballista, support.Id, "top", out _));
            Assert.AreEqual(1, mounts.Mounted.Count);
        }

        [Test]
        public void DifferentWeaponKinds_ShareTheSameRules_OnSeparatePoints()
        {
            Assert.IsTrue(layout.TryPlace(tower, Vector2Int.zero, 0, out var support));
            Assert.IsTrue(mounts.TryMount(cannon, support.Id, "left", out var first));
            Assert.IsTrue(mounts.TryMount(ballista, support.Id, "right", out var second));
            Assert.AreSame(cannon, first.Definition);
            Assert.AreSame(ballista, second.Definition);
            Assert.AreEqual(2, mounts.Mounted.Count);
        }

        [Test]
        public void RemovingSupport_ReleasesEveryWeaponOnIt_AndLeavesOthers()
        {
            Assert.IsTrue(layout.TryPlace(tower, Vector2Int.zero, 0, out var broken));
            Assert.IsTrue(layout.TryPlace(wall, Vector2Int.right, 0, out var standing));
            mounts.TryMount(cannon, broken.Id, "left", out var a);
            mounts.TryMount(ballista, broken.Id, "right", out var b);
            mounts.TryMount(cannon, standing.Id, "top", out var kept);
            List<MountedWeapon> removed = mounts.RemoveSupportedBy(broken.Id);
            CollectionAssert.AreEquivalent(new[] { a, b }, removed);
            Assert.IsNull(mounts.At(broken.Id, "left"));
            Assert.AreSame(kept, mounts.At(standing.Id, "top"));
            Assert.AreEqual(1, mounts.Mounted.Count);
            Assert.IsEmpty(mounts.RemoveSupportedBy(broken.Id));
            Assert.IsTrue(mounts.Remove(kept.Id));
            Assert.IsFalse(mounts.Remove(kept.Id));
            Assert.AreEqual(MountIssue.None, mounts.Check(ballista, standing.Id, "top"));
        }
    }
}
