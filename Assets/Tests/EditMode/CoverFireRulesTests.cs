using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>COMBAT-10 성가퀴 뒤 엄호 사격: 웅크려 재장전 규칙, 빈틈 자리, 원거리 분대 빈틈 사격 자리, 받침 높이.</summary>
    public class CoverFireRulesTests
    {
        [Test]
        public void Crouch_OnlyBehindCoverWhileReloading()
        {
            Assert.IsTrue(CoverCrouchRules.ShouldCrouch(true, false, false, 0.9f, 0.5f), "reloading behind cover");
            Assert.IsFalse(CoverCrouchRules.ShouldCrouch(true, false, false, 0.5f, 0.5f), "stands up riseLead before the next shot");
            Assert.IsFalse(CoverCrouchRules.ShouldCrouch(true, false, false, 0f, 0.5f), "ready to shoot or idle: standing");
            Assert.IsFalse(CoverCrouchRules.ShouldCrouch(false, false, false, 0.9f, 0.5f), "no cover: never crouches");
            Assert.IsFalse(CoverCrouchRules.ShouldCrouch(true, true, false, 0.9f, 0.5f), "releasing an arrow: standing");
            Assert.IsFalse(CoverCrouchRules.ShouldCrouch(true, false, true, 0.9f, 0.5f), "moving: standing");
        }

        [Test]
        public void CrouchedHeight_HidesBehindSill()
        {
            var profile = CombatProfile.Create(attackKind: AttackKind.Projectile);
            var settings = BattlementSettings.Create();
            try
            {
                Assert.AreEqual(1.1f, settings.ParapetHeight, 1e-4f, "sill raised to 1.1m");
                float crouched = profile.Height * profile.CrouchHeightRatio;
                Assert.LessOrEqual(crouched * profile.UpperAimRatio, settings.ParapetHeight, "crouched upper body hidden");
                Assert.Greater(profile.Height * profile.UpperAimRatio, settings.ParapetHeight, "standing upper body exposed");
                Assert.Greater(profile.Height * 0.5f, 0f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CrenelCenters_MiddleAndEveryPeriod()
        {
            var settings = BattlementSettings.Create(merlonWidth: 0.75f, crenelWidth: 0.75f);
            var centers = new List<float>();
            try
            {
                BattlementRules.CrenelCenters(BattlementStyle.Crenellated, settings, 3f, centers);
                CollectionAssert.AreEqual(new[] { 0f, 1.5f, 3f }, centers, "half crenels at both ends, full crenel in the middle");
                BattlementRules.CrenelCenters(BattlementStyle.Parapet, settings, 3f, centers);
                Assert.IsEmpty(centers, "parapet has no crenels");
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Arrange_FrontRankTakesNearestCrenelsRestStandBehind()
        {
            // 북쪽(+z)을 보는 5명 한 줄(간격 1.1m, x -2.2..2.2), 앞줄 z=0. 빈틈은 3m 간격으로 앞줄 선 앞 0.3m에 있다.
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 5, 5, 1.1f);
            var crenels = new List<Vector3>();
            for (int i = -4; i <= 4; i++) crenels.Add(new Vector3(i * 3f, 0f, 0.3f));
            var output = new List<Vector3>();
            int used = CrenelSlotRules.Arrange(slots, 5, 0f, 1.1f, crenels, output);
            Assert.AreEqual(3, used, "front half width 2.2m + 1.1m reaches the -3, 0, 3 crenels only");
            Assert.AreEqual(5, output.Count);
            Assert.AreEqual(new Vector3(-3f, 0f, 0.3f), output[0]);
            Assert.AreEqual(new Vector3(0f, 0f, 0.3f), output[1]);
            Assert.AreEqual(new Vector3(3f, 0f, 0.3f), output[2]);
            for (int i = 3; i < 5; i++) Assert.AreEqual(0.3f - 1.1f, output[i].z, 1e-4f, "leftover archers one spacing behind");
        }

        [Test]
        public void Arrange_KeepsSlotsWithoutUsableCrenels()
        {
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 4, 4, 1.1f);
            var output = new List<Vector3>();
            var far = new List<Vector3> { new Vector3(0f, 0f, 5f), new Vector3(0f, 3f, 0f) };
            Assert.AreEqual(0, CrenelSlotRules.Arrange(slots, 4, 0f, 1.1f, far, output), "too deep or another tier");
            CollectionAssert.AreEqual(slots, output);
            Assert.AreEqual(0, CrenelSlotRules.Arrange(slots, 4, 0f, 1.1f, new List<Vector3>(), output));
            CollectionAssert.AreEqual(slots, output);
        }

        [Test]
        public void Arrange_OneArcherPerCrenelUpToFiles()
        {
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 2, 2, 1.1f);
            var crenels = new List<Vector3> { new Vector3(-0.75f, 0f, 0f), new Vector3(0.75f, 0f, 0f), new Vector3(0f, 0f, 0f) };
            var output = new List<Vector3>();
            Assert.AreEqual(2, CrenelSlotRules.Arrange(slots, 2, 0f, 1.1f, crenels, output));
            Assert.AreEqual(2, output.Count);
            Assert.AreNotEqual(output[0], output[1]);
        }
    }
}
