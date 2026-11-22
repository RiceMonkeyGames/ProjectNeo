using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-15 공성 적 돌격 전환·궁수 위치·재장전 웅크림 참기 규칙.</summary>
    public class SiegeAssaultRulesTests
    {
        const int North = 1 << 0, East = 1 << 1, South = 1 << 2, West = 1 << 3;
        // 적이 남쪽(-z)에서 온다.
        static readonly Vector2Int South2 = new Vector2Int(0, -1);
        readonly List<Object> assets = new List<Object>();
        BuildGridDefinition grid;
        BuildPieceDefinition wall;
        BuildPieceDefinition bastion;
        BuildLayout layout;
        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            grid = Track(BuildGridDefinition.Create(9, 9, 3f, 3));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one));
            bastion = Track(BuildPieceDefinition.Create(BuildPieceKind.Bastion, "bastion", Vector2Int.one));
            layout = new BuildLayout(grid);
        }

        [TearDown]
        public void TearDown() { foreach (Object asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        void Place(BuildPieceDefinition piece, int x, int z) =>
            Assert.IsTrue(layout.TryPlace(piece, new Vector2Int(x, z), 0, out _), $"place {piece.DisplayName} at {x},{z}");

        // 1..5 칸을 두르는 성벽 고리. gap이면 남쪽 (3,1) 칸을 비운다.
        void Ring(bool gap = false)
        {
            for (int i = 1; i <= 5; i++)
            {
                if (!(gap && i == 3)) Place(wall, i, 1);
                Place(wall, i, 5);
                if (i != 1 && i != 5) { Place(wall, 1, i); Place(wall, 5, i); }
            }
        }

        // ── 돌격 전환 ───────────────────────────────────────

        [Test]
        public void BreachTarget_IsInsideCastleOrRaised()
        {
            Assert.IsTrue(SiegeAssaultRules.IsBreachTarget(true, 0f, 1f), "courtyard ally");
            Assert.IsTrue(SiegeAssaultRules.IsBreachTarget(false, 3f, 1f), "wall or bastion top ally");
            Assert.IsFalse(SiegeAssaultRules.IsBreachTarget(false, 0.2f, 1f), "sortie ally on the field");
        }

        [Test]
        public void LeashRadius_UsesReleaseRadiusOnlyWhileLeashAssaulting()
        {
            Assert.AreEqual(8f, SiegeAssaultRules.LeashRadius(false, 8f, 12f));
            Assert.AreEqual(12f, SiegeAssaultRules.LeashRadius(true, 8f, 12f));
            Assert.AreEqual(8f, SiegeAssaultRules.LeashRadius(true, 8f, 4f), "release never shrinks the leash");
        }

        [Test]
        public void SiegeBreakers_IgnoreFarSortie_AndAssaultOnBreachOrLeash()
        {
            foreach (SiegeAttackerRole role in new[] { SiegeAttackerRole.Ladder, SiegeAttackerRole.GateBreaker, SiegeAttackerRole.WallBreaker })
            {
                // 성 밖으로 멀리 나온 아군 하나(근접 반경 밖, 돌파구 없음)에는 끌려가지 않는다.
                Assert.IsFalse(SiegeAssaultRules.ShouldAssault(role, false, true, false, false, false, 0f, 8f), $"{role} far sortie");
                Assert.IsTrue(SiegeAssaultRules.ShouldAssault(role, false, true, false, true, false, 0f, 8f), $"{role} leash");
                Assert.IsTrue(SiegeAssaultRules.ShouldAssault(role, false, true, true, false, false, 0f, 8f), $"{role} breach");
            }
        }

        [Test]
        public void AttackerInside_AssaultsAnyReachableAlly()
        {
            Assert.IsTrue(SiegeAssaultRules.ShouldAssault(SiegeAttackerRole.Ladder, true, true, false, false, false, 0f, 8f));
            Assert.IsFalse(SiegeAssaultRules.ShouldAssault(SiegeAttackerRole.Ladder, true, false, false, false, false, 0f, 8f));
        }

        [Test]
        public void Archer_KeepsSupportingAfterBreach_UntilIdleDelayOrCommitted()
        {
            const SiegeAttackerRole archer = SiegeAttackerRole.Archer;
            Assert.IsFalse(SiegeAssaultRules.ShouldAssault(archer, false, true, true, false, false, 3f, 8f), "still has recent shots");
            Assert.IsTrue(SiegeAssaultRules.ShouldAssault(archer, false, true, true, false, false, 8f, 8f), "idle long enough");
            Assert.IsTrue(SiegeAssaultRules.ShouldAssault(archer, false, true, true, false, true, 0f, 8f), "already committed");
            Assert.IsTrue(SiegeAssaultRules.ShouldAssault(archer, false, true, false, true, false, 0f, 8f), "self-defense in leash");
            Assert.IsFalse(SiegeAssaultRules.ShouldAssault(archer, false, true, false, false, true, 99f, 8f), "no breach, no leash");
        }

        // ── 성 범위 ─────────────────────────────────────────

        [Test]
        public void Footprint_GappedRing_StillHasInsideCourtyard()
        {
            Ring(gap: true);
            var castle = new CastleFootprint();
            castle.Refresh(layout, grid, 1, South2);
            Assert.IsTrue(castle.HasCastle);
            Assert.IsTrue(castle.IsWithinCastle(new Vector2Int(3, 3)), "courtyard");
            Assert.IsTrue(castle.IsInsideCastle(new Vector2Int(3, 3)), "courtyard strictly inside");
            Assert.IsTrue(castle.IsWithinCastle(new Vector2Int(3, 1)), "gap cell counts for breach targets");
            Assert.IsFalse(castle.IsInsideCastle(new Vector2Int(3, 1)), "attacker in the gap is not inside yet");
            Assert.IsFalse(castle.IsWithinCastle(new Vector2Int(3, 0)), "field in front of the gap");
            Assert.AreEqual(new Vector2(10.5f, 10.5f), castle.Center, "centre of cells 1..5 at 3m (3m..18m)");
            Assert.IsTrue(castle.IsInsideCastle(new Vector2Int(3, 8)), "behind the castle (away from the enemy side) counts as inside");
        }

        [Test]
        public void Footprint_StraightWallLine_TreatsTheFarSideAsInside()
        {
            // 시험 장면처럼 z=4 한 줄 성벽만 있고 적은 북쪽(+z)에서 온다.
            for (int x = 0; x < 9; x++) if (x != 4) Place(wall, x, 4);
            var castle = new CastleFootprint();
            castle.Refresh(layout, grid, 1, new Vector2Int(0, 1));
            Assert.IsTrue(castle.IsWithinCastle(new Vector2Int(4, 1)), "ally behind the wall line");
            Assert.IsTrue(castle.IsInsideCastle(new Vector2Int(4, 1)), "attacker that went through the gap");
            Assert.IsTrue(castle.IsWithinCastle(new Vector2Int(4, 4)), "gap cell");
            Assert.IsFalse(castle.IsInsideCastle(new Vector2Int(4, 4)), "attacker standing in the gap is not inside yet");
            Assert.IsFalse(castle.IsWithinCastle(new Vector2Int(4, 6)), "enemy-side field");
            Assert.AreEqual(North | South, castle.OutwardFaceMask(layout, new Vector2Int(2, 4)), "both sides open on a single line");
        }

        [Test]
        public void Footprint_RefreshesOnlyOnLayoutRevision()
        {
            Ring();
            var castle = new CastleFootprint();
            castle.Refresh(layout, grid, 1, South2);
            Place(wall, 7, 3);
            castle.Refresh(layout, grid, 1, South2);
            Assert.IsFalse(castle.IsWithinCastle(new Vector2Int(7, 3)), "same revision keeps the old rectangle");
            castle.Refresh(layout, grid, 2, South2);
            Assert.IsTrue(castle.IsWithinCastle(new Vector2Int(7, 3)), "new revision recounts");
        }

        [Test]
        public void OutwardFaces_FlankWallAndBastion()
        {
            Ring();
            Place(bastion, 6, 3);
            var castle = new CastleFootprint();
            castle.Refresh(layout, grid, 1, South2);
            Assert.AreEqual(West, castle.OutwardFaceMask(layout, new Vector2Int(1, 3)), "west flank wall");
            Assert.AreEqual(South | West, castle.OutwardFaceMask(layout, new Vector2Int(1, 1)), "corner");
            Assert.AreEqual(North | East | South, castle.OutwardFaceMask(layout, new Vector2Int(6, 3)), "protruding bastion");
            Assert.AreEqual(0, castle.OutwardFaceMask(layout, new Vector2Int(5, 3)), "wall behind the bastion");
            Assert.AreEqual(0, castle.OutwardFaceMask(layout, new Vector2Int(3, 3)), "empty courtyard");
        }

        // ── 궁수 위치 ───────────────────────────────────────

        [Test]
        public void ChooseFace_PicksFaceMostTowardArcher()
        {
            Assert.AreEqual(1, ArcherApproachRules.ChooseFace(North | East | South, new Vector2(5f, 1f)), "east of bastion");
            Assert.AreEqual(0, ArcherApproachRules.ChooseFace(North | East | South, new Vector2(1f, 5f)), "north of bastion");
            Assert.AreEqual(3, ArcherApproachRules.ChooseFace(South | West, new Vector2(-4f, -3f)), "corner, more west");
            Assert.AreEqual(-1, ArcherApproachRules.ChooseFace(West, new Vector2(5f, 0f)), "face points away from archer");
            Assert.AreEqual(-1, ArcherApproachRules.ChooseFace(0, new Vector2(5f, 0f)), "no face");
        }

        [Test]
        public void ApproachDirection_UsesFaceNormal_ThenRadial_ThenTowardArcher()
        {
            Vector2 center = new Vector2(9f, 9f);
            // 서쪽 성벽 위 대상, 남서쪽 궁수: 서쪽 면 법선.
            Assert.AreEqual(Vector2.left, ArcherApproachRules.ApproachDirection(West, new Vector2(4.5f, 9f),
                new Vector2(-10f, 2f), center, true, Vector2.up));
            // 안뜰 대상(면 없음), 궁수가 같은 쪽: 성 중심에서 대상 쪽.
            Vector2 radial = ArcherApproachRules.ApproachDirection(0, new Vector2(9f, 12f), new Vector2(2f, 30f), center, true, Vector2.up);
            Assert.That(Vector2.Distance(Vector2.up, radial), Is.LessThan(0.001f));
            // 안뜰 대상, 궁수가 반대쪽: 대상에서 궁수 쪽(성을 돌아가지 않음).
            Vector2 toward = ArcherApproachRules.ApproachDirection(0, new Vector2(9f, 12f), new Vector2(9f, -20f), center, true, Vector2.up);
            Assert.That(Vector2.Distance(Vector2.down, toward), Is.LessThan(0.001f));
        }

        // ── 재장전 웅크림 참기 ──────────────────────────────

        [Test]
        public void DuckedTarget_WaitsWithoutBlockedClock_UntilTolerance()
        {
            var plan = new ArcherCoverPlan(delay: 3f, cooldown: 10f, commit: 5f, duckTolerance: 4f);
            Assert.IsTrue(plan.TargetDucked(0f));
            Assert.IsTrue(plan.IsWaiting, "aims in place");
            Assert.IsTrue(plan.TargetDucked(3.5f), "reload crouch inside tolerance");
            // 참는 동안은 가려진 시간을 재지 않으므로 참기가 끝난 뒤 delay만큼 더 지나야 옆 자리를 찾는다.
            Assert.IsFalse(plan.TargetDucked(4f), "tolerance over");
            Assert.IsFalse(plan.ShouldSearch(4f));
            Assert.IsFalse(plan.ShouldSearch(6.9f));
            Assert.IsTrue(plan.ShouldSearch(7f));
        }

        [Test]
        public void DuckTolerance_RestartsAfterShot()
        {
            var plan = new ArcherCoverPlan(3f, 10f, 5f, 4f);
            Assert.IsTrue(plan.TargetDucked(0f));
            Assert.IsFalse(plan.TargetDucked(4f));
            plan.Shooting();
            Assert.IsTrue(plan.TargetDucked(5f), "new reload crouch gets a fresh tolerance");
            Assert.IsFalse(new ArcherCoverPlan(3f, 10f, 5f).TargetDucked(0f), "default tolerance 0 never waits");
        }
    }
}
