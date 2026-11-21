using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>화살 포물선(COMBAT-09).</summary>
    public class ArrowBallisticsTests
    {
        const float Speed = 18f, Gravity = 9.81f;

        static float TimeToReach(Vector3 origin, Vector3 velocity, Vector3 target)
        {
            var flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            return flat.magnitude / new Vector3(velocity.x, 0f, velocity.z).magnitude;
        }

        [TestCase(16f, 0f)]    // 같은 높이 16m
        [TestCase(20f, -4.5f)] // 성벽 위에서 아래로
        [TestCase(10f, 4.5f)]  // 아래에서 성벽 위로
        [TestCase(30f, 0f)]    // 명사수 사거리
        public void LowArc_LandsOnTarget(float distance, float height)
        {
            Vector3 origin = new Vector3(1f, 1f, 2f);
            Vector3 target = origin + new Vector3(0.6f, 0f, 0.8f) * distance + Vector3.up * height;
            Vector3 velocity = ArrowBallistics.LaunchVelocity(origin, target, Speed, Gravity);
            Assert.AreEqual(Speed, velocity.magnitude, 0.001f, "발사 속도는 그대로다.");
            Vector3 landed = ArrowBallistics.Evaluate(origin, velocity, Gravity, TimeToReach(origin, velocity, target));
            Assert.AreEqual(0f, Vector3.Distance(target, landed), 0.01f);
        }

        [Test]
        public void LowArc_At16Meters_IsAbout14Degrees()
        {
            Vector3 velocity = ArrowBallistics.LaunchVelocity(Vector3.zero, Vector3.forward * 16f, Speed, Gravity);
            float angle = Mathf.Atan2(velocity.y, velocity.z) * Mathf.Rad2Deg;
            Assert.AreEqual(14.5f, angle, 1f);
            Assert.Less(angle, 45f, "낮은 포물선을 고른다.");
        }

        [Test]
        public void OutOfReach_FiresAt45Degrees()
        {
            Vector3 velocity = ArrowBallistics.LaunchVelocity(Vector3.zero, Vector3.forward * 60f, Speed, Gravity);
            Assert.AreEqual(45f, Vector3.Angle(velocity, Vector3.forward), 0.01f);
            Assert.AreEqual(Speed, velocity.magnitude, 0.001f);
        }

        [Test]
        public void StraightUpOrNoGravity_FlyStraightAtTarget()
        {
            Vector3 up = ArrowBallistics.LaunchVelocity(Vector3.zero, Vector3.up * 5f, Speed, Gravity);
            Assert.AreEqual(Vector3.up * Speed, up);
            Vector3 flat = ArrowBallistics.LaunchVelocity(Vector3.zero, new Vector3(0f, 1f, 10f), Speed, 0f);
            Assert.AreEqual(0f, Vector3.Angle(flat, new Vector3(0f, 1f, 10f)), 0.01f);
        }

        [Test]
        public void VelocityAt_FallsWithGravity()
        {
            Vector3 velocity = ArrowBallistics.VelocityAt(new Vector3(0f, 4f, 10f), Gravity, 1f);
            Assert.AreEqual(4f - Gravity, velocity.y, 0.0001f);
            Assert.AreEqual(10f, velocity.z, 0.0001f);
        }

        [Test]
        public void Profile_DefaultsAreSpeed18AndGravity981()
        {
            CombatProfile profile = CombatProfile.Create();
            CombatProfile negative = CombatProfile.Create(projectileGravity: -1f);
            try
            {
                Assert.AreEqual(18f, profile.ProjectileSpeed);
                Assert.AreEqual(9.81f, profile.ProjectileGravity, 0.0001f);
                Assert.AreEqual(0f, negative.ProjectileGravity, "음수 중력은 0으로 자른다.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(negative);
            }
        }
    }
}
