using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-16 비행 적 대포 노리기 순서와 공격 자리 나누기·시야 확보 규칙.</summary>
    public class FlyerHoverSlotTests
    {
        static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [Test]
        public void WallTopFirst_PicksWeaponAfterWallTopAllies_BeforeGroundAllies()
        {
            var strategy = WallTopFirstFlyerTargetStrategy.Create();
            try
            {
                var mixed = new[]
                {
                    new FlyerTargetCandidate(0, 1f, false),            // 가장 가까운 안마당 아군
                    FlyerTargetCandidate.SiegeWeapon(1, 16f),
                    FlyerTargetCandidate.SiegeWeapon(2, 9f),           // 더 가까운 대포
                    new FlyerTargetCandidate(3, 100f, true),           // 멀어도 성벽 위 아군이 먼저
                };
                Assert.AreEqual(3, mixed[strategy.Select(mixed)].Id);
                var noWallTop = new[] { mixed[0], mixed[1], mixed[2] };
                Assert.AreEqual(2, noWallTop[strategy.Select(noWallTop)].Id, "성벽 위 아군이 없으면 가장 가까운 대포.");
                var onlyGround = new[] { mixed[0] };
                Assert.AreEqual(0, onlyGround[strategy.Select(onlyGround)].Id);
                Assert.IsTrue(mixed[1].IsSiegeWeapon);
                Assert.IsFalse(mixed[1].OnWallTop);
                Assert.IsFalse(mixed[1].IsPriority);
            }
            finally { Object.DestroyImmediate(strategy); }
        }

        [Test]
        public void PriorityTargeting_PriorityAllyBeatsWeapon()
        {
            var strategy = WallTopFirstFlyerTargetStrategy.Create();
            try
            {
                var candidates = new[]
                {
                    FlyerTargetCandidate.SiegeWeapon(0, 1f),
                    new FlyerTargetCandidate(1, 64f, false, true),
                };
                Assert.AreEqual(1, FlyerPriorityTargeting.Select(candidates, strategy));
                Assert.AreEqual(0, FlyerPriorityTargeting.Select(new[] { candidates[0] }, strategy));
            }
            finally { Object.DestroyImmediate(strategy); }
        }

        [Test]
        public void HoverBeside_SpreadsFlyersOnSameTarget_BySeparation()
        {
            var engagement = HoverBesideEngagement.Create(hoverHeight: 0.5f, standoffRatio: 0.7f, matchTargetSurface: false);
            try
            {
                var target = new Vector3(0f, 3f, 0f);
                var approach = new Vector3(0f, 3f, 10f);
                const float separation = 1.2f;
                var taken = new List<Vector3>();
                for (int i = 0; i < 4; i++)
                {
                    // 모두 같은 쪽에서 다가와도 앞선 비행 적의 자리를 피한다.
                    Vector3 point = engagement.HoverPoint(new FlyerHoverQuery(approach, target, 2f, 0.4f, 0.4f, null,
                        taken, separation));
                    foreach (Vector3 other in taken)
                        Assert.GreaterOrEqual(Horizontal(point, other), separation - 1e-3f, $"자리 {i}가 앞선 자리와 겹친다.");
                    Assert.AreEqual(3.5f, point.y, 1e-4f);
                    Assert.LessOrEqual(Horizontal(point, target), 0.4f + 2f + 1e-3f, "사거리 바깥 바퀴로는 나가지 않는다.");
                    taken.Add(point);
                }
                // 첫 비행 적은 다가오던 쪽 바로 옆(기존 자리)에 뜬다.
                Assert.AreEqual(0f, taken[0].x, 1e-4f);
                Assert.AreEqual(1.8f, taken[0].z, 1e-4f);
            }
            finally { Object.DestroyImmediate(engagement); }
        }

        [Test]
        public void HoverBeside_WhenEverySlotIsTaken_ReusesValidSlotInsteadOfLeavingSurface()
        {
            var engagement = HoverBesideEngagement.Create(hoverHeight: 0.5f, standoffRatio: 0.5f);
            try
            {
                var target = new Vector3(0f, 3f, 0f);
                // 대상 바로 옆(반경 1.6m 안)만 성벽 윗면이다.
                float? Surface(Vector3 point) => new Vector2(point.x, point.z).magnitude < 1.6f ? 3f : 0f;
                var crowd = new List<Vector3>();
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2f / 64f;
                    crowd.Add(new Vector3(Mathf.Cos(angle) * 1.4f, 3.5f, Mathf.Sin(angle) * 1.4f));
                }
                Vector3 point = engagement.HoverPoint(new FlyerHoverQuery(new Vector3(0f, 8f, 10f), target, 2f, 0.4f, 0.4f,
                    Surface, crowd, 1.2f));
                Assert.AreEqual(1.4f, Horizontal(point, target), 1e-3f, "겹치더라도 발판 위 자리를 쓴다.");
                Assert.AreEqual(3.5f, point.y, 1e-4f);
            }
            finally { Object.DestroyImmediate(engagement); }
        }

        [Test]
        public void HoverBeside_SkipsBlockedSightline_AndUsesOuterRingWhenNeeded()
        {
            var engagement = HoverBesideEngagement.Create(hoverHeight: 3f, standoffRatio: 0.1f, matchTargetSurface: false);
            try
            {
                var target = Vector3.zero;
                var approach = new Vector3(0f, 0f, 20f);
                // 높은 성벽(+z 쪽)이 대상 가까운 자리의 시야를 막는다. 바깥으로 2m 넘게 나가거나 성벽 반대쪽이면 보인다.
                bool ClearSide(Vector3 point) => point.z < -0.1f;
                Vector3 side = engagement.HoverPoint(new FlyerHoverQuery(approach, target, 12f, 0.4f, 0.4f, null,
                    null, 1.2f, ClearSide));
                Assert.Less(side.z, -0.1f, "시야가 막힌 자리는 건너뛴다.");
                Assert.AreEqual(3f, side.y, 1e-4f);

                bool ClearFar(Vector3 point) => Horizontal(point, target) > 2f;
                Vector3 far = engagement.HoverPoint(new FlyerHoverQuery(approach, target, 12f, 0.4f, 0.4f, null,
                    null, 1.2f, ClearFar));
                Assert.Greater(Horizontal(far, target), 2f, "가까운 바퀴가 모두 막히면 사거리 안의 바깥 바퀴를 쓴다.");
                Assert.LessOrEqual(Horizontal(far, target), 0.4f + 12f + 1e-3f);

                // 어디도 보이지 않으면 기존처럼 다가오던 쪽 옆에 뜬다.
                Vector3 blind = engagement.HoverPoint(new FlyerHoverQuery(approach, target, 12f, 0.4f, 0.4f, null,
                    null, 1.2f, _ => false));
                Assert.AreEqual(0f, blind.x, 1e-4f);
                Assert.Greater(blind.z, 0f);
            }
            finally { Object.DestroyImmediate(engagement); }
        }

        [Test]
        public void FlyerSettings_DefaultsTargetWeaponsAndKeepSeparationNonNegative()
        {
            var settings = FlyerSettings.Create(null, null, null);
            var custom = FlyerSettings.Create(null, null, null, targetsSiegeWeapons: false, hoverSeparation: -3f);
            try
            {
                Assert.IsTrue(settings.TargetsSiegeWeapons);
                Assert.AreEqual(1.2f, settings.HoverSeparation, 1e-4f);
                Assert.IsFalse(custom.TargetsSiegeWeapons);
                Assert.AreEqual(0f, custom.HoverSeparation);
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(custom);
            }
        }
    }
}
