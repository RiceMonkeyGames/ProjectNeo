using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>층 쌓기(Q-27 A안): 가리킨 칸 위에 자동으로 쌓기, 2단 계단, 받침을 잃은 조각의 내려앉기.</summary>
    public class BuildStackingTests
    {
        readonly List<Object> assets = new List<Object>();
        BuildPieceDefinition wall, stairs, gate;
        SiegeWeaponDefinition cannon;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one,
                mountPoints: new[] { new BuildMountPoint("top", Vector3.up) }));
            stairs = Track(BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", new Vector2Int(1, 2)));
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", new Vector2Int(2, 1)));
            cannon = Track(SiegeWeaponDefinition.Create("cannon", "cannon", Vector3.one));
            layout = new BuildLayout(Track(BuildGridDefinition.Create(8, 8, 3f, 3)));
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        BuildPlacement Place(BuildPieceDefinition piece, int x, int z, int rotation = 0, bool fixedGate = false)
        {
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), rotation, out BuildPlacement placement, fixedGate));
            return placement;
        }

        [Test]
        public void 쌓기를_켜지_않은_격자는_같은_칸에_다시_놓을_수_없다()
        {
            var flat = new BuildLayout(Track(BuildGridDefinition.Create(8, 8, 3f)));
            Assert.IsTrue(flat.TryPlace(wall, Vector2Int.zero, 0, out _));
            Assert.AreEqual(PlacementIssue.Occupied, flat.Check(wall, Vector2Int.zero, 0));
        }

        [Test]
        public void 성벽은_가리킨_칸의_맨_위에_쌓이고_최대_층을_넘지_못한다()
        {
            BuildPlacement first = Place(wall, 2, 2);
            BuildPlacement second = Place(wall, 2, 2);
            BuildPlacement third = Place(wall, 2, 2);
            Assert.AreEqual(0, first.Level); Assert.AreEqual(0f, first.Elevation);
            Assert.AreEqual(1, second.Level); Assert.AreEqual(3f, second.Elevation);
            Assert.AreEqual(2, third.Level); Assert.AreEqual(6f, third.Elevation);
            Assert.AreSame(first, layout.At(new Vector2Int(2, 2)));
            Assert.AreSame(second, layout.At(new Vector2Int(2, 2), 1));
            Assert.AreSame(third, layout.Top(new Vector2Int(2, 2)));
            Assert.AreEqual(3, layout.ColumnHeight(new Vector2Int(2, 2)));
            Assert.IsTrue(layout.IsCovered(first));
            Assert.IsFalse(layout.IsCovered(third));
            Assert.AreEqual(PlacementIssue.Occupied, layout.Check(wall, new Vector2Int(2, 2), 0));
        }

        [Test]
        public void 계단_위에는_쌓을_수_없다()
        {
            Place(stairs, 1, 1);
            Assert.AreEqual(PlacementIssue.Occupied, layout.Check(wall, new Vector2Int(1, 1), 0));
            Assert.AreEqual(PlacementIssue.Occupied, layout.Check(wall, new Vector2Int(1, 2), 0));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void 고정_성문의_각_칸_위에_성벽을_쌓고_파괴하면_내려앉는다(int rotation)
        {
            BuildPlacement lower = Place(gate, 4, 4, rotation, true);
            var upperWalls = new List<BuildPlacement>();
            for (int x = 0; x < lower.Size.x; x++)
                for (int z = 0; z < lower.Size.y; z++)
                {
                    BuildPlacement upper = Place(wall, 4 + x, 4 + z);
                    upperWalls.Add(upper);
                    Assert.AreEqual(1, upper.Level);
                    Assert.AreEqual(gate.Height, upper.Elevation);
                }
            Assert.IsFalse(layout.Remove(lower.Id), "성문은 여전히 철거할 수 없다.");
            Assert.IsTrue(layout.Remove(lower.Id, true));
            var dropped = new List<BuildPlacement>(); var unsupported = new List<BuildPlacement>();
            layout.Settle(dropped, unsupported);
            CollectionAssert.AreEquivalent(upperWalls, dropped);
            CollectionAssert.IsEmpty(unsupported);
            foreach (BuildPlacement upper in upperWalls)
            {
                Assert.AreEqual(0, upper.Level);
                Assert.AreEqual(0f, upper.Elevation);
                Assert.AreSame(upper, layout.At(upper.Cell));
                layout.Remove(upper.Id, true);
                Assert.AreEqual(PlacementIssue.ReservedGate, layout.Check(wall, upper.Cell, 0));
            }
        }

        [Test]
        public void 여러_칸_조각은_모든_칸의_받침_높이가_같아야_한다()
        {
            Place(wall, 3, 3);
            Assert.AreEqual(PlacementIssue.UnevenSupport,
                layout.Check(stairs, new Vector2Int(3, 3), 0, false, out _, out float previewHeight));
            Assert.AreEqual(3f, previewHeight, "놓을 수 없어도 미리보기는 받침 위에 보인다.");
            Assert.IsFalse(layout.TryPlace(stairs, new Vector2Int(3, 3), 0, out _));
            Assert.IsNull(layout.At(new Vector2Int(3, 4)), "실패한 배치는 상태를 남기지 않는다.");
            Place(wall, 3, 4);
            BuildPlacement upper = Place(stairs, 3, 3);
            Assert.AreEqual(1, upper.Level);
            Assert.AreEqual(3f, upper.Elevation);
        }

        [Test]
        public void 이단_계단은_같은_층의_성벽에_닿아야_연결된다()
        {
            // 1층 성벽 세 칸을 잇고 그 위에 계단을 올린다. 계단의 높은 쪽은 (3,5)다.
            Place(wall, 3, 3); Place(wall, 3, 4); Place(wall, 3, 5);
            BuildPlacement upperStairs = Place(stairs, 3, 3);
            Assert.AreEqual(1, upperStairs.Level);
            Assert.IsFalse(layout.HasLanding(upperStairs), "1층 성벽 윗면은 2단 계단의 도착점이 아니다.");
            BuildPlacement landing = Place(wall, 3, 5);
            Assert.AreEqual(1, landing.Level);
            Assert.IsTrue(layout.HasLanding(upperStairs));
        }

        [Test]
        public void 아래_조각이_사라지면_위에_쌓인_조각이_한_층씩_내려앉는다()
        {
            BuildPlacement bottom = Place(wall, 2, 2);
            BuildPlacement middle = Place(wall, 2, 2);
            BuildPlacement top = Place(wall, 2, 2);
            BuildPlacement neighbor = Place(wall, 3, 2);
            var dropped = new List<BuildPlacement>(); var unsupported = new List<BuildPlacement>();
            Assert.IsTrue(layout.Remove(bottom.Id, true));
            layout.Settle(dropped, unsupported);
            CollectionAssert.AreEqual(new[] { middle, top }, dropped);
            CollectionAssert.IsEmpty(unsupported);
            Assert.AreEqual(0, middle.Level); Assert.AreEqual(0f, middle.Elevation);
            Assert.AreEqual(1, top.Level); Assert.AreEqual(3f, top.Elevation);
            Assert.AreSame(middle, layout.At(new Vector2Int(2, 2)));
            Assert.AreSame(top, layout.At(new Vector2Int(2, 2), 1));
            Assert.IsNull(layout.At(new Vector2Int(2, 2), 2));
            Assert.AreEqual(0, neighbor.Level, "옆 칸은 그대로다.");
            Assert.AreEqual(PlacementIssue.None, layout.Check(wall, new Vector2Int(2, 2), 0), "내려앉아 생긴 자리에 다시 쌓을 수 있다.");
        }

        [Test]
        public void 한쪽_받침만_잃어_내려앉을_수_없는_조각은_배치에서_빠진다()
        {
            BuildPlacement left = Place(wall, 3, 3);
            BuildPlacement right = Place(wall, 3, 4);
            BuildPlacement upper = Place(stairs, 3, 3);
            var dropped = new List<BuildPlacement>(); var unsupported = new List<BuildPlacement>();
            layout.Remove(left.Id, true);
            layout.Settle(dropped, unsupported);
            CollectionAssert.IsEmpty(dropped);
            CollectionAssert.AreEqual(new[] { upper }, unsupported);
            Assert.IsNull(layout.Find(upper.Id));
            Assert.IsNull(layout.At(new Vector2Int(3, 4), 1));
            Assert.AreSame(right, layout.At(new Vector2Int(3, 4)));
        }

        [Test]
        public void 받침이_모두_사라진_여러_칸_조각은_통째로_내려앉는다()
        {
            BuildPlacement left = Place(wall, 3, 3);
            BuildPlacement right = Place(wall, 3, 4);
            BuildPlacement upper = Place(stairs, 3, 3);
            var dropped = new List<BuildPlacement>(); var unsupported = new List<BuildPlacement>();
            layout.Remove(left.Id, true);
            layout.Remove(right.Id, true);
            layout.Settle(dropped, unsupported);
            CollectionAssert.AreEqual(new[] { upper }, dropped);
            CollectionAssert.IsEmpty(unsupported);
            Assert.AreEqual(0, upper.Level);
            Assert.AreSame(upper, layout.At(new Vector2Int(3, 3)));
            Assert.AreSame(upper, layout.At(new Vector2Int(3, 4)));
        }

        [Test]
        public void 위에_조각이_얹힌_성벽에는_수성_무기를_놓을_수_없다()
        {
            var mounts = new MountLayout(layout);
            BuildPlacement lower = Place(wall, 2, 2);
            Assert.AreEqual(MountIssue.None, mounts.Check(cannon, lower.Id, "top"));
            BuildPlacement upper = Place(wall, 2, 2);
            Assert.AreEqual(MountIssue.Occupied, mounts.Check(cannon, lower.Id, "top"));
            Assert.IsFalse(mounts.HasWeaponOn(upper.Id));
            Assert.IsTrue(mounts.TryMount(cannon, upper.Id, "top", out _));
            Assert.IsTrue(mounts.HasWeaponOn(upper.Id));
        }
    }
}
