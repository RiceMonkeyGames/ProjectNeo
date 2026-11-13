using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class CastleStateTests
    {
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition gate;
        SiegeWeaponDefinition cannon;

        [SetUp]
        public void SetUp()
        {
            grid = BuildGridDefinition.Create(8, 8, 3f, 3);
            wall = BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one, 3f, 100f, null,
                new BuildMountPoint("top", new Vector3(0.5f, 1f, 0.5f)));
            gate = BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", new Vector2Int(2, 1), 3f, 300f);
            cannon = SiegeWeaponDefinition.Create("cannon", "cannon", Vector3.one);
        }

        [Test]
        public void 배치_층_회전_체력_무기를_기록하고_빈_격자에_그대로_되살린다()
        {
            var layout = new BuildLayout(grid);
            var mounts = new MountLayout(layout);
            Assert.IsTrue(layout.TryPlace(gate, new Vector2Int(2, 0), 0, out BuildPlacement gatePiece, true));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(5, 3), 1, out BuildPlacement lower));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(5, 3), 0, out BuildPlacement upper));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(2, 0), 0, out BuildPlacement onGate));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(6, 6), 0, out BuildPlacement armed));
            Assert.IsTrue(mounts.TryMount(cannon, armed.Id, "top", out _));
            var health = new Dictionary<int, float> { { upper.Id, 35f }, { gatePiece.Id, 120f } };

            CastleState state = CastleState.Capture(layout, mounts,
                p => health.TryGetValue(p.Id, out float h) ? h : p.Definition.MaxHealth);

            Assert.AreEqual(5, state.Pieces.Count);
            for (int i = 1; i < state.Pieces.Count; i++)
                Assert.LessOrEqual(state.Pieces[i - 1].Level, state.Pieces[i].Level, "아래층부터 정렬한다.");
            Assert.IsTrue(state.HasGate);
            Assert.AreEqual(1, state.Weapons.Count);

            var restoredLayout = new BuildLayout(grid);
            var restoredMounts = new MountLayout(restoredLayout);
            Assert.IsTrue(state.TryRestore(restoredLayout, restoredMounts, out List<BuildPlacement> placed));
            Assert.AreEqual(state.Pieces.Count, placed.Count);
            for (int i = 0; i < placed.Count; i++)
            {
                Assert.AreSame(state.Pieces[i].Definition, placed[i].Definition);
                Assert.AreEqual(state.Pieces[i].Cell, placed[i].Cell);
                Assert.AreEqual(state.Pieces[i].Rotation, placed[i].Rotation);
                Assert.AreEqual(state.Pieces[i].Level, placed[i].Level);
            }

            BuildPlacement restoredUpper = restoredLayout.At(new Vector2Int(5, 3), 1);
            Assert.IsNotNull(restoredUpper);
            Assert.AreEqual(3f, restoredUpper.Elevation);
            Assert.AreEqual(35f, state.Pieces[placed.IndexOf(restoredUpper)].Health);
            BuildPlacement restoredGate = restoredLayout.At(new Vector2Int(2, 0));
            Assert.IsTrue(restoredGate.IsFixed);
            Assert.AreEqual(120f, state.Pieces[placed.IndexOf(restoredGate)].Health);
            Assert.IsNotNull(restoredLayout.At(new Vector2Int(2, 0), 1), "성문 위 성벽도 되살린다.");
            Assert.AreEqual(PlacementIssue.ReservedGate,
                restoredLayout.Check(gate, new Vector2Int(0, 4), 0, true), "성문은 다시 하나뿐이다.");

            BuildPlacement restoredArmed = restoredLayout.At(new Vector2Int(6, 6));
            Assert.IsNotNull(restoredMounts.At(restoredArmed.Id, "top"));
        }

        [Test]
        public void 무기의_방향과_깎인_체력을_기록하고_방향대로_되살린다()
        {
            SiegeWeaponDefinition sturdy = SiegeWeaponDefinition.Create("sturdy", "sturdy", Vector3.one, null, 150f);
            try
            {
                var layout = new BuildLayout(grid);
                var mounts = new MountLayout(layout);
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(1, 1), 0, out BuildPlacement first));
                Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(4, 4), 0, out BuildPlacement second));
                Assert.IsTrue(mounts.TryMount(sturdy, first.Id, "top", out MountedWeapon damaged, 3));
                Assert.IsTrue(mounts.TryMount(sturdy, second.Id, "top", out MountedWeapon intact, 5));
                Assert.AreEqual(1, intact.Rotation, "회전은 0~3으로 맞춘다.");

                CastleState state = CastleState.Capture(layout, mounts, null,
                    weapon => weapon.Id == damaged.Id ? 40f : weapon.Definition.MaxHealth);
                Assert.AreEqual(2, state.Weapons.Count);
                Assert.AreEqual(3, state.Weapons[0].Rotation);
                Assert.AreEqual(40f, state.Weapons[0].Health);
                Assert.AreEqual(1, state.Weapons[1].Rotation);
                Assert.AreEqual(150f, state.Weapons[1].Health);

                CastleState withoutHealth = CastleState.Capture(layout, mounts);
                Assert.AreEqual(150f, withoutHealth.Weapons[0].Health, "체력을 주지 않으면 최대 체력으로 기록한다.");

                var restoredLayout = new BuildLayout(grid);
                var restoredMounts = new MountLayout(restoredLayout);
                Assert.IsTrue(state.TryRestore(restoredLayout, restoredMounts, out List<BuildPlacement> placed));
                Assert.AreEqual(3, restoredMounts.At(placed[state.Weapons[0].SupportIndex].Id, "top").Rotation);
                Assert.AreEqual(1, restoredMounts.At(placed[state.Weapons[1].SupportIndex].Id, "top").Rotation);
            }
            finally { Object.DestroyImmediate(sturdy); }
        }

        [Test]
        public void 무기_기록의_체력은_0과_최대_체력_사이로_맞춘다()
        {
            Assert.AreEqual(100f, new CastleWeaponState(cannon, 0, "top", 0, 500f).Health);
            Assert.AreEqual(0f, new CastleWeaponState(cannon, 0, "top", 0, -5f).Health);
            Assert.AreEqual(100f, new CastleWeaponState(cannon, 0, "top").Health);
            Assert.AreEqual(2, new CastleWeaponState(cannon, 0, "top", -2).Rotation);
        }

        [Test]
        public void 내려앉은_조각은_바뀐_층으로_기록되고_부서진_조각은_없다()
        {
            var layout = new BuildLayout(grid);
            layout.TryPlace(wall, new Vector2Int(1, 1), 0, out BuildPlacement bottom);
            layout.TryPlace(wall, new Vector2Int(1, 1), 0, out BuildPlacement top);
            layout.Remove(bottom.Id, true);
            layout.Settle(new List<BuildPlacement>(), new List<BuildPlacement>());

            CastleState state = CastleState.Capture(layout, null);
            Assert.AreEqual(1, state.Pieces.Count);
            Assert.AreEqual(0, state.Pieces[0].Level);
            Assert.AreEqual(100f, state.Pieces[0].Health, "체력을 주지 않으면 최대 체력이다.");
            Assert.IsFalse(state.HasGate);
        }

        [Test]
        public void 체력은_0과_최대_체력_사이로_맞춘다()
        {
            var high = new CastlePieceState(wall, Vector2Int.zero, 5, 0, false, 999f);
            var low = new CastlePieceState(wall, Vector2Int.zero, -1, 0, false, -5f);
            Assert.AreEqual(100f, high.Health);
            Assert.AreEqual(0f, low.Health);
            Assert.AreEqual(1, high.Rotation);
            Assert.AreEqual(3, low.Rotation);
        }

        [Test]
        public void 이미_조각이_있는_격자에는_되살리지_않는다()
        {
            var layout = new BuildLayout(grid);
            layout.TryPlace(wall, new Vector2Int(1, 1), 0, out _);
            CastleState state = CastleState.Capture(layout, null);

            var occupied = new BuildLayout(grid);
            occupied.TryPlace(wall, new Vector2Int(4, 4), 0, out _);
            Assert.IsFalse(state.TryRestore(occupied, null, out _));
            Assert.AreEqual(1, occupied.Placements.Count);
        }

        [Test]
        public void 복사본은_원본과_같은_내용의_다른_객체다()
        {
            var layout = new BuildLayout(grid);
            layout.TryPlace(wall, new Vector2Int(1, 1), 0, out _);
            CastleState state = CastleState.Capture(layout, null);
            CastleState copy = state.Clone();
            Assert.AreNotSame(state, copy);
            Assert.AreNotSame(state.Pieces, copy.Pieces);
            Assert.AreEqual(state.Pieces[0].Cell, copy.Pieces[0].Cell);
        }
    }
}
