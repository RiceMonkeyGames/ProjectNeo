using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class CannonRulesTests
    {
        const float Gravity = 9.81f;

        // ── 포물선 ──────────────────────────────────────────────

        [TestCase(10f, 0f)]
        [TestCase(40f, 0f)]
        [TestCase(60f, -9f)]
        [TestCase(25f, 3f)]
        public void Ballistics_LandsOnTarget(float distance, float height)
        {
            Vector3 origin = new Vector3(1f, 9f, 2f);
            Vector3 target = origin + new Vector3(distance * 0.6f, height, distance * 0.8f);
            Assert.IsTrue(CannonBallistics.TrySolve(origin, target, 30f, Gravity, out Vector3 velocity, out float time));
            Assert.AreEqual(30f, velocity.magnitude, 0.001f);
            Vector3 landed = CannonBallistics.Evaluate(origin, velocity, Gravity, time);
            Assert.AreEqual(0f, Vector3.Distance(landed, target), 0.01f);
        }

        [Test]
        public void Ballistics_ChoosesLowArc()
        {
            Vector3 origin = Vector3.zero;
            Assert.IsTrue(CannonBallistics.TrySolve(origin, new Vector3(0f, 0f, 30f), 30f, Gravity, out Vector3 velocity, out _));
            float elevation = Mathf.Atan2(velocity.y, new Vector2(velocity.x, velocity.z).magnitude) * Mathf.Rad2Deg;
            Assert.Less(elevation, 45f);
            Assert.Greater(elevation, 0f);
        }

        [Test]
        public void Ballistics_FailsBeyondReachOrStraightDown()
        {
            // 속도 30, 중력 9.81이면 평지 최대 거리는 약 91.7m다.
            Assert.IsFalse(CannonBallistics.TrySolve(Vector3.zero, new Vector3(0f, 0f, 100f), 30f, Gravity, out _, out _));
            Assert.IsFalse(CannonBallistics.TrySolve(Vector3.zero, new Vector3(0f, -5f, 0f), 30f, Gravity, out _, out _));
            Assert.IsFalse(CannonBallistics.TrySolve(Vector3.zero, new Vector3(0f, 0f, 10f), 0f, Gravity, out _, out _));
        }

        // ── 회전 범위·사거리 ───────────────────────────────────

        static CannonAimContext Context() =>
            new CannonAimContext(new Vector3(0f, 9f, 0f), Vector3.forward, 6f, 60f, 45f);

        [TestCase(0f, 20f, true)]
        [TestCase(14f, 15f, true)]     // 약 43도
        [TestCase(16f, 15f, false)]    // 약 47도
        [TestCase(-14f, 15f, true)]
        [TestCase(0f, -20f, false)]    // 뒤쪽
        public void Aim_RespectsHalfArc(float x, float z, bool expected)
        {
            Assert.AreEqual(expected, CannonAim.IsWithinArc(Context(), new Vector3(x, 0f, z)));
        }

        [TestCase(5.9f, false)]
        [TestCase(6f, true)]
        [TestCase(60f, true)]
        [TestCase(60.1f, false)]
        public void Aim_UsesHorizontalRange_IgnoringHeight(float distance, bool expected)
        {
            // 성벽 위(높이 9m) 대포가 땅의 적을 볼 때도 수평 거리만 따진다.
            Assert.AreEqual(expected, CannonAim.IsInRange(Context(), new Vector3(0f, 0f, distance)));
        }

        [TestCase(30f, 30f)]
        [TestCase(70f, 45f)]
        [TestCase(-90f, -45f)]
        [TestCase(350f, -10f)]
        public void Aim_ClampsManualYaw(float yaw, float expected)
        {
            Assert.AreEqual(expected, CannonAim.ClampYawOffset(yaw, 45f), 0.001f);
        }

        // ── 포신 회전·조준 상태(SIEGE-07) ─────────────────────

        [TestCase(0f, 90f, 0.5f, 90f)]     // 초당 180도로 0.5초면 90도
        [TestCase(0f, 90f, 0.25f, 45f)]
        [TestCase(30f, -60f, 0.25f, -15f)]
        [TestCase(10f, 12f, 1f, 12f)]      // 목표를 지나치지 않는다
        [TestCase(10f, 50f, 0f, 10f)]
        public void Aim_SwivelMovesTowardTargetAtSpeed(float current, float target, float seconds, float expected)
        {
            Assert.AreEqual(expected, CannonAim.StepSwivel(current, target, 180f, seconds), 0.001f);
        }

        [Test]
        public void Aim_ManualContextWidensArcOnly()
        {
            CannonSettings settings = CannonSettings.Create();
            try
            {
                Assert.AreEqual(90f, settings.ManualAimHalfAngle);
                Assert.AreEqual(180f, settings.SwivelSpeed);
                CannonAimContext auto = CannonAimContext.From(settings, Vector3.zero, Vector3.forward);
                CannonAimContext manual = CannonAimContext.Manual(settings, Vector3.zero, Vector3.forward);
                var side = new Vector3(20f, 0f, 5f); // 약 76도
                Assert.IsFalse(CannonAim.CanTarget(auto, side), "자동 사격 범위는 그대로 ±45도다.");
                Assert.IsTrue(CannonAim.CanTarget(manual, side), "직접 조작은 ±90도까지 겨눈다.");
                Assert.IsFalse(CannonAim.CanTarget(manual, new Vector3(5f, 0f, -20f)), "뒤쪽은 직접 조작으로도 쏘지 못한다.");
                Assert.AreEqual(auto.MinimumRange, manual.MinimumRange);
                Assert.AreEqual(auto.MaximumRange, manual.MaximumRange);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Settings_ManualArcNeverNarrowerThanAutoArc()
        {
            CannonSettings settings = CannonSettings.Create(aimHalfAngle: 60f, manualAimHalfAngle: 30f);
            try { Assert.AreEqual(60f, settings.ManualAimHalfAngle); }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Operator_AimStatePriority_ReloadThenPointThenRangeThenArc()
        {
            var context = new CannonAimContext(Vector3.zero, Vector3.forward, 6f, 60f, 90f);
            var good = new Vector3(0f, 0f, 20f);
            Assert.AreEqual(CannonAimState.Ready, CannonOperatorRules.EvaluateAim(true, true, context, good));
            Assert.AreEqual(CannonAimState.Reloading, CannonOperatorRules.EvaluateAim(false, true, context, good));
            Assert.AreEqual(CannonAimState.Reloading, CannonOperatorRules.EvaluateAim(false, false, context, default));
            Assert.AreEqual(CannonAimState.NoTarget, CannonOperatorRules.EvaluateAim(true, false, context, good));
            Assert.AreEqual(CannonAimState.OutOfRange, CannonOperatorRules.EvaluateAim(true, true, context, new Vector3(0f, 0f, 3f)));
            Assert.AreEqual(CannonAimState.OutOfRange, CannonOperatorRules.EvaluateAim(true, true, context, new Vector3(0f, 0f, 70f)));
            Assert.AreEqual(CannonAimState.OutOfArc, CannonOperatorRules.EvaluateAim(true, true, context, new Vector3(0f, 0f, -20f)));
        }

        [Test]
        public void OperatorSettings_StateColorsDiffer()
        {
            CannonOperatorSettings settings = CannonOperatorSettings.Create();
            try
            {
                Assert.AreEqual(settings.ReadyColor, settings.ColorFor(CannonAimState.Ready));
                Assert.AreEqual(settings.ReloadingColor, settings.ColorFor(CannonAimState.Reloading));
                foreach (CannonAimState blocked in new[] { CannonAimState.NoTarget, CannonAimState.OutOfRange, CannonAimState.OutOfArc })
                    Assert.AreEqual(settings.BlockedColor, settings.ColorFor(blocked));
                Assert.AreNotEqual(settings.ReadyColor, settings.ReloadingColor);
                Assert.AreNotEqual(settings.ReadyColor, settings.BlockedColor);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Aim_ContextFlattensFacing()
        {
            var context = new CannonAimContext(Vector3.zero, new Vector3(0f, 5f, 1f), 0f, 10f, 45f);
            Assert.AreEqual(0f, context.Facing.y);
            Assert.AreEqual(1f, context.Facing.magnitude, 0.0001f);
        }

        // ── 폭발 피해 ──────────────────────────────────────────

        [TestCase(0f, 1f)]
        [TestCase(1.5f, 0.75f)]
        [TestCase(3f, 0.5f)]
        [TestCase(3.01f, 0f)]
        public void Blast_FalloffIsLinearToEdge(float distance, float expected)
        {
            Assert.AreEqual(expected, CannonBlast.Falloff(distance, 3f, 0.5f), 0.001f);
        }

        [Test]
        public void Blast_AppliesDefenseAfterFalloff_WithMinimumInsideRadius()
        {
            Assert.AreEqual(35f, CannonBlast.Damage(40f, 0f, 3f, 0.5f, 5f, 1f), 0.001f);
            Assert.AreEqual(15f, CannonBlast.Damage(40f, 3f, 3f, 0.5f, 5f, 1f), 0.001f);
            Assert.AreEqual(1f, CannonBlast.Damage(40f, 3f, 3f, 0.5f, 100f, 1f), 0.001f);
            Assert.AreEqual(0f, CannonBlast.Damage(40f, 4f, 3f, 0.5f, 0f, 1f));
        }

        [Test]
        public void Blast_FriendlyFireSwitch()
        {
            Assert.IsTrue(CannonBlast.ShouldDamage(CombatTeam.Allied, CombatTeam.Hostile, false));
            Assert.IsFalse(CannonBlast.ShouldDamage(CombatTeam.Allied, CombatTeam.Allied, false));
            Assert.IsTrue(CannonBlast.ShouldDamage(CombatTeam.Allied, CombatTeam.Allied, true));
            Assert.IsFalse(CannonBlast.ShouldDamageOwnStructures(false));
            Assert.IsTrue(CannonBlast.ShouldDamageOwnStructures(true));
        }

        // ── 자동 조준 ──────────────────────────────────────────

        [Test]
        public void Selector_PicksNearestValidCandidate()
        {
            var candidates = new List<Vector3>
            {
                new Vector3(0f, 0f, 3f),    // 최소 사거리 안
                new Vector3(0f, 0f, -10f),  // 뒤쪽
                new Vector3(0f, 0f, 30f),
                new Vector3(5f, 0f, 12f),   // 13m, 가장 가까운 유효 후보
                new Vector3(0f, 0f, 80f),   // 최대 사거리 밖
            };
            Assert.AreEqual(3, new NearestCannonTargetSelector().Select(Context(), candidates));
        }

        [Test]
        public void Selector_ReturnsMinusOneWithoutValidCandidates()
        {
            var selector = new NearestCannonTargetSelector();
            Assert.AreEqual(-1, selector.Select(Context(), new List<Vector3>()));
            Assert.AreEqual(-1, selector.Select(Context(), new List<Vector3> { new Vector3(0f, 0f, -20f) }));
            Assert.AreEqual(-1, selector.Select(Context(), null));
        }

        [Test]
        public void Selector_TieKeepsEarlierIndex()
        {
            var candidates = new List<Vector3> { new Vector3(-5f, 0f, 12f), new Vector3(5f, 0f, 12f) };
            Assert.AreEqual(0, new NearestCannonTargetSelector().Select(Context(), candidates));
        }

        // ── 흩어짐 ────────────────────────────────────────────

        [Test]
        public void Scatter_StaysInsideRadius_OnHorizontalPlane()
        {
            var random = new SeededRandom(7);
            Vector3 point = new Vector3(4f, 1f, 20f);
            for (int i = 0; i < 200; i++)
            {
                Vector3 scattered = CannonScatter.Apply(point, 2f, random);
                Assert.AreEqual(point.y, scattered.y);
                Assert.LessOrEqual(CannonAim.HorizontalDistance(point, scattered), 2f + 0.0001f);
            }
        }

        [Test]
        public void Scatter_IsDeterministicPerSeed_AndZeroRadiusIsExact()
        {
            Vector3 point = new Vector3(0f, 0f, 20f);
            Vector3 first = CannonScatter.Apply(point, 2f, new SeededRandom(42));
            Vector3 second = CannonScatter.Apply(point, 2f, new SeededRandom(42));
            Assert.AreEqual(first, second);
            Assert.AreEqual(point, CannonScatter.Apply(point, 0f, new SeededRandom(42)));
            Assert.AreEqual(point, CannonScatter.Apply(point, 2f, null));
        }

        // ── 재장전 ────────────────────────────────────────────

        [Test]
        public void Reload_StartsReady_BlocksUntilDurationPasses()
        {
            var reload = new CannonReload(6f);
            Assert.IsTrue(reload.TryFire());
            Assert.IsFalse(reload.TryFire());
            reload.Tick(5.9f);
            Assert.IsFalse(reload.IsReady);
            reload.Tick(0.2f);
            Assert.IsTrue(reload.IsReady);
            Assert.AreEqual(1f, reload.Progress, 0.0001f);
            Assert.IsTrue(reload.TryFire());
            Assert.AreEqual(0f, reload.Progress, 0.0001f);
        }

        [Test]
        public void Reload_IgnoresNegativeTick_AndResetMakesReady()
        {
            var reload = new CannonReload(6f);
            reload.TryFire();
            reload.Tick(-3f);
            Assert.AreEqual(6f, reload.Remaining);
            reload.Reset();
            Assert.IsTrue(reload.IsReady);
        }

        // ── 직접 조작 ─────────────────────────────────────────

        [TestCase(0f, 2f, true)]      // 정면 2m
        [TestCase(0f, 3.1f, false)]   // 거리 밖
        [TestCase(2f, 1f, false)]     // 약 63도, 각도 밖
        [TestCase(1f, 2f, true)]      // 약 27도
        [TestCase(0f, -2f, false)]    // 뒤쪽
        public void Operator_ReachNeedsDistanceAndViewAngle(float x, float z, bool expected)
        {
            Vector3 viewer = new Vector3(0f, 9f, 0f);
            Vector3 cannon = viewer + new Vector3(x, 0.5f, z);
            Assert.AreEqual(expected, CannonOperatorRules.IsWithinReach(viewer, Vector3.forward, cannon, 3f, 60f));
        }

        [TestCase(10f, 0f, 10f)]
        [TestCase(80f, 0f, 45f)]
        [TestCase(-80f, 0f, -45f)]
        [TestCase(200f, 180f, 200f)]   // 정면 180도 기준 +20도
        [TestCase(100f, 180f, 135f)]   // 정면 180도 기준 -80도 → -45도
        public void Operator_ClampsViewYawAroundFacing(float viewYaw, float facingYaw, float expected)
        {
            float clamped = CannonOperatorRules.ClampViewYaw(viewYaw, facingYaw, 45f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, clamped), 0.001f);
        }

        [Test]
        public void OperatorSettings_DefaultReachValues()
        {
            CannonOperatorSettings settings = CannonOperatorSettings.Create();
            try
            {
                Assert.AreEqual(3f, settings.InteractDistance);
                Assert.AreEqual(60f, settings.InteractHalfAngle);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        // ── 적의 대포 공격 거리 ───────────────────────────────

        [TestCase(1.5f, 0f, true)]    // 사거리 1 + 반 폭 0.9 = 1.9
        [TestCase(1.95f, 0f, false)]
        [TestCase(1.5f, 3f, false)]   // 한 층 아래
        [TestCase(0f, 0.5f, true)]
        public void StrikeReach_UsesWeaponSizeInsteadOfCollider(float distance, float drop, bool expected)
        {
            Vector3 weapon = new Vector3(0f, 9f, 0f);
            Vector3 attacker = weapon + new Vector3(distance, -drop, 0f);
            Assert.AreEqual(expected, SiegeWeaponStrikeRules.InReach(attacker, weapon, new Vector3(1.2f, 1.1f, 1.8f), 1f));
        }

        // ── 설정 에셋 ─────────────────────────────────────────

        [Test]
        public void Settings_DefaultsMatchQ21TemporaryValues()
        {
            CannonSettings settings = CannonSettings.Create();
            try
            {
                Assert.AreEqual(6f, settings.MinimumRange);
                Assert.AreEqual(60f, settings.MaximumRange);
                Assert.AreEqual(45f, settings.AimHalfAngle);
                Assert.AreEqual(3f, settings.BlastRadius);
                Assert.AreEqual(40f, settings.CenterDamage);
                Assert.AreEqual(0.5f, settings.EdgeDamageRatio);
                Assert.AreEqual(6f, settings.ReloadSeconds);
                Assert.AreEqual(2f, settings.AutoScatterRadius);
                Assert.IsFalse(settings.FriendlyFire);
                // 기본 발사 속도로 최대 사거리(평지)에 닿아야 한다.
                Assert.IsTrue(CannonBallistics.TrySolve(Vector3.zero, new Vector3(0f, 0f, settings.MaximumRange),
                    settings.ShellSpeed, settings.ShellGravity, out _, out _));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Settings_MaximumRangeNeverBelowMinimum()
        {
            CannonSettings settings = CannonSettings.Create(minimumRange: 20f, maximumRange: 10f);
            try { Assert.AreEqual(20f, settings.MaximumRange); }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
