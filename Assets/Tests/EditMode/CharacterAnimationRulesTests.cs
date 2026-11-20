using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>ART-04: 캐릭터 모델 애니메이션의 순수 규칙(활 순서, 근접 시작 지점, 조준 높낮이, 교전 유지).</summary>
    public class CharacterAnimationRulesTests
    {
        const float Load = 0.8f;
        const float Release = 0.8f;

        [Test]
        public void Bow_FireFromLowered_DrawsFastThenReleasesAtImpact()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Fire(0.2f);
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
            Assert.AreEqual(Load / 0.2f, bow.PhaseSpeed, 0.001f, "장전을 선딜레이 안에 끝내도록 빨리 재생한다.");
            Assert.IsFalse(bow.Tick(0.1f, true));
            Assert.IsTrue(bow.Tick(0.11f, true));
            Assert.AreEqual(BowDrawPhase.Releasing, bow.Phase);
            Assert.AreEqual(1f, bow.PhaseSpeed);
        }

        [Test]
        public void Bow_AfterReleaseWhileEngaged_ReloadsAndHolds()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Fire(0f);
            bow.Tick(0.01f, true);
            Assert.AreEqual(BowDrawPhase.Releasing, bow.Phase);
            bow.Tick(Release, true);
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
            Assert.AreEqual(1f, bow.PhaseSpeed, "교전 중 재장전은 원래 속도로 한다.");
            bow.Tick(Load, true);
            Assert.AreEqual(BowDrawPhase.Holding, bow.Phase);
            bow.Tick(5f, true);
            Assert.AreEqual(BowDrawPhase.Holding, bow.Phase, "교전 중에는 당긴 채 기다린다.");
        }

        [Test]
        public void Bow_ReleaseWhenNotEngaged_Lowers()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Fire(0f);
            bow.Tick(0.01f, false);
            bow.Tick(Release, false);
            Assert.AreEqual(BowDrawPhase.Lowered, bow.Phase);
        }

        [Test]
        public void Bow_FireWhileHolding_StaysDrawnUntilImpact()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Tick(0.01f, true);
            bow.Tick(Load, true);
            Assert.AreEqual(BowDrawPhase.Holding, bow.Phase);
            bow.Fire(0.3f);
            Assert.AreEqual(BowDrawPhase.Holding, bow.Phase);
            bow.Tick(0.2f, true);
            Assert.AreEqual(BowDrawPhase.Holding, bow.Phase);
            bow.Tick(0.11f, true);
            Assert.AreEqual(BowDrawPhase.Releasing, bow.Phase);
        }

        [Test]
        public void Bow_FireDuringRelease_StartsNextDraw()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Fire(0f);
            bow.Tick(0.01f, true);
            bow.Fire(0.4f);
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
            Assert.AreEqual(Load / 0.4f, bow.PhaseSpeed, 0.001f);
        }

        [Test]
        public void Bow_FireMidDraw_SpeedsUpOnlyTheRemainingPart()
        {
            var bow = new BowDrawCycle(Load, Release);
            bow.Tick(0.01f, true);
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
            bow.Tick(Load * 0.5f, true);
            bow.Fire(0.2f);
            // 남은 장전(0.4초 분량)을 0.2초 안에: 2배속.
            Assert.AreEqual(2f, bow.PhaseSpeed, 0.05f);
            bow.Fire(1f);
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
        }

        [Test]
        public void Bow_LoweredAndEngaged_StartsDrawing()
        {
            var bow = new BowDrawCycle(Load, Release);
            Assert.IsFalse(bow.Tick(0.1f, false));
            Assert.IsTrue(bow.Tick(0.1f, true));
            Assert.AreEqual(BowDrawPhase.Drawing, bow.Phase);
        }

        [TestCase(0.45f, 0.18f, 1.1f, 0.27f)]
        [TestCase(0.1f, 0.18f, 1.1f, 0f)]
        [TestCase(2f, 0f, 1f, 0.95f)]
        public void MeleeStart_AlignsContactWithImpact(float contact, float impact, float clip, float expected)
        {
            Assert.AreEqual(expected, CharacterAnimationRules.MeleeStartSeconds(contact, impact, clip), 0.001f);
        }

        [Test]
        public void AimPitch_MapsAngleToMinusOneToOne()
        {
            Vector3 from = Vector3.zero;
            Assert.AreEqual(0f, CharacterAnimationRules.AimPitch(from, new Vector3(10f, 0f, 0f), 40f), 0.001f);
            Assert.AreEqual(1f, CharacterAnimationRules.AimPitch(from, new Vector3(1f, 10f, 0f), 40f), 0.001f);
            Assert.AreEqual(-1f, CharacterAnimationRules.AimPitch(from, new Vector3(0f, -10f, 1f), 40f), 0.001f);
            Assert.AreEqual(0.5f, CharacterAnimationRules.AimPitch(from, Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward, 40f), 0.001f);
            Assert.AreEqual(-1f, CharacterAnimationRules.AimPitch(from, Vector3.down, 40f), 0.001f, "바로 아래도 끝값이다.");
        }

        [Test]
        public void Engaged_AfterRecentAttackOrHoldingOrder()
        {
            Assert.IsTrue(CharacterAnimationRules.IsEngaged(1f, 2.5f, false));
            Assert.IsFalse(CharacterAnimationRules.IsEngaged(3f, 2.5f, false));
            Assert.IsFalse(CharacterAnimationRules.IsEngaged(float.PositiveInfinity, 2.5f, false));
            Assert.IsTrue(CharacterAnimationRules.IsEngaged(float.PositiveInfinity, 2.5f, true));
        }

        [Test]
        public void KeepsBowDrawn_LowersWhileRunningUnlessOrderedToHold()
        {
            Assert.IsTrue(CharacterAnimationRules.KeepsBowDrawn(true, 0.5f, 1.5f, false));
            Assert.IsFalse(CharacterAnimationRules.KeepsBowDrawn(true, 3.5f, 1.5f, false), "달리면 활을 내린다.");
            Assert.IsFalse(CharacterAnimationRules.KeepsBowDrawn(false, 0f, 1.5f, false));
            Assert.IsTrue(CharacterAnimationRules.KeepsBowDrawn(false, 3.5f, 1.5f, true), "겨눈 채 기다리라는 지시는 따른다.");
        }

        [Test]
        public void PickOther_IsRandomInRangeAndNeverRepeatsPrevious()
        {
            var random = new ProjectNeo.Core.SeededRandom(7);
            var seen = new bool[5];
            int previous = -1;
            for (int i = 0; i < 200; i++)
            {
                int pick = CharacterAnimationRules.PickOther(random, 5, previous);
                Assert.That(pick, Is.InRange(0, 4));
                Assert.AreNotEqual(previous, pick, "같은 공격을 연달아 고르지 않는다.");
                seen[pick] = true;
                previous = pick;
            }
            CollectionAssert.DoesNotContain(seen, false, "다섯 동작이 모두 나온다.");
            Assert.AreEqual(0, CharacterAnimationRules.PickOther(random, 1, 0), "하나뿐이면 그것을 고른다.");
            Assert.AreEqual(-1, CharacterAnimationRules.PickOther(random, 0, -1));
        }

        [Test]
        public void BowModelYaw_TurnsModelSoArrowLinePointsAtTarget()
        {
            Vector3 Dir(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
            // 클립에서 화살 선이 모델 정면보다 왼쪽 30도면 그 각도를 익힌다.
            Assert.AreEqual(-30f, CharacterAnimationRules.AimOffset(Vector3.forward, Dir(-30f)), 0.01f);
            Assert.AreEqual(-30f, CharacterAnimationRules.AimOffset(Dir(40f), Dir(10f)), 0.01f, "모델이 돌아가 있어도 모델 기준으로 잰다.");
            // 대상이 정면이면 모델을 오른쪽으로 30도 돌려야 화살 선이 대상을 향한다.
            Assert.AreEqual(30f, CharacterAnimationRules.BowModelYaw(Vector3.forward, Vector3.forward, -30f), 0.01f);
            // 대상이 오른쪽 20도면 50도.
            Assert.AreEqual(50f, CharacterAnimationRules.BowModelYaw(Vector3.forward, Dir(20f), -30f), 0.01f);
            Assert.AreEqual(90f, CharacterAnimationRules.BowModelYaw(Vector3.forward, Dir(80f), -30f), 0.01f, "±90도로 자른다.");
            Assert.AreEqual(0f, CharacterAnimationRules.BowModelYaw(Vector3.forward, Vector3.up, -30f), "수평 방향이 없으면 돌리지 않는다.");
        }

        [Test]
        public void Sheath_DrawsAtOnceAndSheathesAfterCalmDelay()
        {
            var rule = new WeaponSheathRule(3f);
            Assert.IsFalse(rule.Armed, "처음에는 허리에 차고 있다.");
            Assert.IsFalse(rule.Tick(1f, false));
            Assert.IsTrue(rule.Tick(0.1f, true), "적이 가까우면 바로 뽑는다.");
            Assert.IsTrue(rule.Armed);
            Assert.IsFalse(rule.Tick(2f, false));
            Assert.IsFalse(rule.Tick(0.5f, true), "다시 이유가 생기면 넣기까지의 시간이 처음부터 다시 센다.");
            Assert.IsFalse(rule.Tick(2.9f, false));
            Assert.IsTrue(rule.Tick(0.2f, false), "3초 동안 조용하면 넣는다.");
            Assert.IsFalse(rule.Armed);
            Assert.IsTrue(rule.ForceArmed(), "공격이 먼저 나오면 곧바로 뽑은 상태가 된다.");
            Assert.IsFalse(rule.ForceArmed());
        }

        [Test]
        public void Sheath_WantsArmedWhenEngagedOrTargetClose()
        {
            Assert.IsTrue(WeaponSheathRule.WantsArmed(true, float.PositiveInfinity, 8f));
            Assert.IsTrue(WeaponSheathRule.WantsArmed(false, 7.9f, 8f));
            Assert.IsFalse(WeaponSheathRule.WantsArmed(false, 8.1f, 8f));
            Assert.IsFalse(WeaponSheathRule.WantsArmed(false, float.PositiveInfinity, 8f));
        }

        [Test]
        public void FlatDirection_LaysWeaponAxisOnGround()
        {
            Assert.AreEqual(Vector3.right, CharacterAnimationRules.FlatDirection(new Vector3(2f, 5f, 0f), Vector3.forward));
            Assert.AreEqual(Vector3.forward, CharacterAnimationRules.FlatDirection(Vector3.up, new Vector3(0f, 3f, 2f)),
                "칼이 똑바로 서 있으면 대신 받은 방향으로 눕힌다.");
            Assert.AreEqual(Vector3.forward, CharacterAnimationRules.FlatDirection(Vector3.down, Vector3.up));
        }

        [Test]
        public void Variant_IsStableAndInRange()
        {
            Assert.AreEqual(-1, CharacterAnimationRules.Variant(5, 0));
            Assert.AreEqual(2, CharacterAnimationRules.Variant(-4, 6));
            Assert.AreEqual(CharacterAnimationRules.Variant(12345, 6), CharacterAnimationRules.Variant(12345, 6));
        }

        [Test]
        public void Profile_Create_ClampsAndExposesMeleeTimes()
        {
            var profile = CharacterAnimationProfile.Create(meleeContactSeconds: new[] { 0.4f, 0.5f },
                meleeClipSeconds: new[] { 1.1f }, deathCount: -2, aimPitchRange: 200f);
            try
            {
                Assert.AreEqual(1, profile.MeleeCount, "짝이 맞는 개수만 쓴다.");
                Assert.AreEqual(0.4f, profile.MeleeContactSeconds(0));
                Assert.AreEqual(0, profile.DeathCount);
                Assert.AreEqual(90f, profile.AimPitchRange);
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
