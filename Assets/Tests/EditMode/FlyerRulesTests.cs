using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class FlyerRulesTests
    {
        [Test]
        public void WallTopFirst_PrefersWallTopThenNearestThenStableId()
        {
            var strategy = WallTopFirstFlyerTargetStrategy.Create();
            try
            {
                var candidates = new[]
                {
                    new FlyerTargetCandidate(1, 1f, false),  // 가장 가깝지만 땅의 아군
                    new FlyerTargetCandidate(7, 25f, true),
                    new FlyerTargetCandidate(3, 25f, true),  // 같은 거리면 낮은 번호
                    new FlyerTargetCandidate(4, 36f, true),
                };
                Assert.AreEqual(3, candidates[strategy.Select(candidates)].Id);
                var reversed = new[] { candidates[3], candidates[2], candidates[1], candidates[0] };
                Assert.AreEqual(3, reversed[strategy.Select(reversed)].Id);
                // 성벽 위 아군이 없으면 남은 아군 중 가장 가까운 대상.
                var ground = new[] { new FlyerTargetCandidate(5, 9f, false), new FlyerTargetCandidate(6, 4f, false) };
                Assert.AreEqual(6, ground[strategy.Select(ground)].Id);
            }
            finally { Object.DestroyImmediate(strategy); }
        }

        [Test]
        public void WallTopFirst_RejectsEmptyAndInvalidDistances()
        {
            var strategy = WallTopFirstFlyerTargetStrategy.Create();
            try
            {
                Assert.AreEqual(-1, strategy.Select(new FlyerTargetCandidate[0]));
                Assert.AreEqual(-1, strategy.Select(new[] { new FlyerTargetCandidate(1, float.NaN, true),
                    new FlyerTargetCandidate(2, float.PositiveInfinity, true), new FlyerTargetCandidate(3, -1f, true) }));
                Assert.AreEqual(1, strategy.Select(new[] { new FlyerTargetCandidate(9, float.NaN, true),
                    new FlyerTargetCandidate(8, 4f, false) }));
            }
            finally { Object.DestroyImmediate(strategy); }
        }

        [Test]
        public void DirectFlight_ClimbsToCruiseBeforeTravelling_ThenDescendsNearDestination()
        {
            var movement = DirectFlightMovement.Create(verticalSpeed: 4f, descendRadius: 4f, cruiseTolerance: 0.5f);
            try
            {
                var destination = new Vector3(0f, 3f, 20f);
                // 땅에서 출발하면 먼저 오르기만 한다.
                Vector3 next = movement.Step(Vector3.zero, destination, 10f, 5f, 1f);
                Assert.AreEqual(new Vector3(0f, 4f, 0f), next);
                // 순항 높이 근처면 수평으로 나아간다.
                next = movement.Step(new Vector3(0f, 9.6f, 0f), destination, 10f, 5f, 1f);
                Assert.AreEqual(10f, next.y, 1e-4f);
                Assert.AreEqual(5f, next.z, 1e-4f);
                // 목적지 가까이에서는 목적지 높이로 내려간다.
                next = movement.Step(new Vector3(0f, 10f, 17f), destination, 10f, 5f, 0.5f);
                Assert.AreEqual(8f, next.y, 1e-4f);
                Assert.AreEqual(19.5f, next.z, 1e-4f);
                // 도착하면 그 자리에 머문다.
                Assert.AreEqual(destination, movement.Step(destination, destination, 10f, 5f, 1f));
                Assert.AreEqual(Vector3.one, movement.Step(Vector3.one, destination, 10f, 5f, 0f));
            }
            finally { Object.DestroyImmediate(movement); }
        }

        [Test]
        public void HoverBeside_StaysOnTargetSurface_AtHoverHeight()
        {
            var engagement = HoverBesideEngagement.Create(hoverHeight: 0.5f, standoffRatio: 0.5f);
            try
            {
                var target = new Vector3(0f, 3f, 0f);
                // 비행 적이 +z(성 밖)에서 다가오지만 그쪽은 성벽 바깥 땅(높이 0)이다. 성벽 윗면은 x 방향으로 이어진다.
                float? Surface(Vector3 point) => Mathf.Abs(point.z) < 1f ? 3f : 0f;
                Vector3 point = engagement.HoverPoint(new Vector3(0f, 8f, 10f), target, 2f, 0.4f, 0.4f, Surface);
                Assert.AreEqual(3.5f, point.y, 1e-4f, "대상 발판에서 정한 높이만큼 떠 있어야 한다.");
                Assert.Less(Mathf.Abs(point.z), 1f, "대상과 같은 발판 위에 자리를 잡아야 한다.");
                Assert.AreEqual(1.4f, new Vector2(point.x, point.z).magnitude, 1e-3f, "대상 반지름 + 사거리 비율만큼 떨어진다.");
                // 발판을 따지지 않으면 다가오던 쪽 옆에 뜬다.
                var loose = HoverBesideEngagement.Create(hoverHeight: 6f, standoffRatio: 0.5f, matchTargetSurface: false);
                try
                {
                    Vector3 high = loose.HoverPoint(new Vector3(0f, 8f, 10f), target, 2f, 0.4f, 0.4f, Surface);
                    Assert.AreEqual(0f, high.x, 1e-4f);
                    Assert.AreEqual(9f, high.y, 1e-4f);
                    Assert.AreEqual(1.4f, high.z, 1e-4f);
                }
                finally { Object.DestroyImmediate(loose); }
            }
            finally { Object.DestroyImmediate(engagement); }
        }

        [Test]
        public void AirborneReach_MeleeUsesGroundProjection_OnlyWhenExposed()
        {
            var feet = new Vector3(0f, 3f, 0f);
            Assert.IsTrue(AirborneReach.InMeleeReach(feet, new Vector3(1.8f, 3f, 0f), true, 1.5f, 0.4f));
            Assert.IsFalse(AirborneReach.InMeleeReach(feet, new Vector3(2.0f, 3f, 0f), true, 1.5f, 0.4f));
            Assert.IsFalse(AirborneReach.InMeleeReach(feet, new Vector3(1f, 3f, 0f), false, 1.5f, 0.4f), "높이 뜨면 닿지 않는다.");
            Assert.IsFalse(AirborneReach.InMeleeReach(feet, new Vector3(0.5f, 0f, 0f), true, 1.5f, 0.4f),
                "성벽 아래 땅 위에 뜬 대상에는 성벽 위에서 닿지 않는다.");
            Assert.IsFalse(AirborneReach.InMeleeReach(feet, feet, true, float.NaN, 0.4f));
            Assert.IsTrue(AirborneReach.CanMeleeTarget(true, true));
            Assert.IsFalse(AirborneReach.CanMeleeTarget(true, false));
            Assert.IsFalse(AirborneReach.CanMeleeTarget(false, true));
        }
    }
}
