using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-34 공성 적 역할 협동: 궁수 엄호 우선순위, 가려진 대상 바꾸기, 집중 돌파, 사다리 고르기 점수.</summary>
    public class SiegeCoordinationRulesTests
    {
        readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        // ── 궁수 엄호 ─────────────────

        [Test]
        public void ArcherCover_DefenderAtLadderOrOpening_LooksAboutPoint7TimesAsFar()
        {
            Assert.AreEqual(100f, ArcherCoverRules.Score(100f, false, 0.5f));
            Assert.AreEqual(50f, ArcherCoverRules.Score(100f, true, 0.5f));
            // 10m 거리의 엄호 대상(제곱 50)은 7m 거리의 보통 대상(49)보다 조금 뒤, 8m(64)보다는 앞이다.
            float cover10 = ArcherCoverRules.Score(10f * 10f, true, 0.5f);
            Assert.Greater(cover10, ArcherCoverRules.Score(7f * 7f, false, 0.5f), "훨씬 가까운 수비병이 여전히 먼저다.");
            Assert.Less(cover10, ArcherCoverRules.Score(8f * 8f, false, 0.5f));
            Assert.AreEqual(100f, ArcherCoverRules.Score(100f, true, 1f), "배율 1이면 우선순위가 없다.");
        }

        [Test]
        public void ArcherCover_OnlyRaisedOrRaisingLadders_AndNearTheirTop()
        {
            Assert.IsTrue(ArcherCoverRules.IsActiveLadder(LadderPhase.Up));
            Assert.IsTrue(ArcherCoverRules.IsActiveLadder(LadderPhase.Raising));
            Assert.IsFalse(ArcherCoverRules.IsActiveLadder(LadderPhase.Down));
            Vector3 top = new Vector3(0f, 5f, 0f);
            Assert.IsTrue(ArcherCoverRules.IsNearLadderTop(new Vector3(3f, 5f, -2f), top, 4f, 1.5f));
            Assert.IsFalse(ArcherCoverRules.IsNearLadderTop(new Vector3(5f, 5f, 0f), top, 4f, 1.5f), "위쪽 끝에서 4m 밖");
            Assert.IsFalse(ArcherCoverRules.IsNearLadderTop(new Vector3(1f, 0f, 0f), top, 4f, 1.5f), "성벽 아래");
        }

        // ── 가려진 대상 바꾸기 ─────────────────

        [Test]
        public void BlockedTracker_AvoidsAfterTwoFailedSearches_For15Seconds()
        {
            var tracker = new ArcherBlockedTargetTracker<string>(2, 15f);
            const string hidden = "hidden";
            Assert.IsFalse(tracker.RecordSearch(hidden, false, 0f));
            Assert.IsFalse(tracker.IsAvoided(hidden, 0f));
            Assert.IsTrue(tracker.RecordSearch(hidden, false, 10f), "두 번째 연속 실패에서 뺀다.");
            Assert.IsTrue(tracker.IsAvoided(hidden, 10f));
            Assert.IsTrue(tracker.IsAvoided(hidden, 24.9f));
            Assert.IsFalse(tracker.IsAvoided(hidden, 25f), "15초가 지나면 다시 노린다.");
        }

        [Test]
        public void BlockedTracker_SuccessOrAnotherTarget_RestartsTheCount()
        {
            var tracker = new ArcherBlockedTargetTracker<string>(2, 15f);
            tracker.RecordSearch("a", false, 0f);
            Assert.IsFalse(tracker.RecordSearch("a", true, 1f), "찾기에 성공하면 빼지 않는다.");
            Assert.AreEqual(0, tracker.Failures);
            tracker.RecordSearch("a", false, 2f);
            Assert.IsFalse(tracker.RecordSearch("b", false, 3f), "다른 대상의 실패는 따로 센다.");
            Assert.AreEqual(1, tracker.Failures);
            tracker.RecordSearch("b", false, 4f);
            Assert.IsTrue(tracker.IsAvoided("b", 4f));
            Assert.IsFalse(tracker.IsAvoided("a", 4f));
            tracker.Reset();
            Assert.IsTrue(tracker.IsAvoided("b", 5f), "실패 횟수를 지워도 빼 둔 대상은 남는다.");
            tracker.Clear();
            Assert.IsFalse(tracker.IsAvoided("b", 5f));
            Assert.IsFalse(tracker.RecordSearch(null, false, 6f));
        }

        [Test]
        public void BlockedTracker_RemembersOnlyCapacityTargets()
        {
            var tracker = new ArcherBlockedTargetTracker<string>(1, 15f, 2);
            tracker.RecordSearch("a", false, 0f);
            tracker.RecordSearch("b", false, 0f);
            tracker.RecordSearch("c", false, 0f);
            Assert.IsFalse(tracker.IsAvoided("a", 1f), "가장 오래된 기록부터 지운다.");
            Assert.IsTrue(tracker.IsAvoided("b", 1f));
            Assert.IsTrue(tracker.IsAvoided("c", 1f));
        }

        // ── 집중 돌파 ─────────────────

        FocusedBreachWallTargetStrategy Focused()
        {
            var strategy = FocusedBreachWallTargetStrategy.Create();
            assets.Add(strategy);
            return strategy;
        }

        static WallTargetCandidate Wall(int id, float distance, float health = 1f, int engaged = 0) =>
            new WallTargetCandidate(id, distance * distance, health, engaged);

        [Test]
        public void FocusedBreach_WithoutDamageOrEngagement_PicksNearest()
        {
            var candidates = new List<WallTargetCandidate> { Wall(1, 10f), Wall(2, 6f), Wall(3, 6f) };
            Assert.AreEqual(1, Focused().Select(candidates), "같으면 배치 번호가 작은 쪽");
            Assert.AreEqual(-1, Focused().Select(new List<WallTargetCandidate>()));
        }

        [Test]
        public void FocusedBreach_FavorsDamagedWalls()
        {
            // 0 HP에 가까울수록 최대 15m 가깝게 본다: 20m·체력 20%는 20-12=8로 10m 멀쩡한 벽보다 앞이다.
            var candidates = new List<WallTargetCandidate> { Wall(1, 10f), Wall(2, 20f, 0.2f) };
            Assert.AreEqual(1, Focused().Select(candidates));
            var tooFar = new List<WallTargetCandidate> { Wall(1, 10f), Wall(2, 30f, 0f) };
            Assert.AreEqual(0, Focused().Select(tooFar), "15m보다 더 먼 벽은 다쳐도 가까운 벽이 먼저다.");
        }

        [Test]
        public void FocusedBreach_JoinsEngagedWalls_UpToThreeBreakers()
        {
            var candidates = new List<WallTargetCandidate> { Wall(1, 5f), Wall(2, 12f, 1f, 1) };
            Assert.AreEqual(1, Focused().Select(candidates), "동료 한 명이 치는 벽은 8m 가깝게 본다.");
            FocusedBreachWallTargetStrategy strategy = Focused();
            Assert.AreEqual(strategy.Score(Wall(1, 40f, 1f, 3)), strategy.Score(Wall(1, 40f, 1f, 7)), 1e-4f, "3명까지만 센다.");
            Assert.AreEqual(40f - 24f, strategy.Score(Wall(1, 40f, 1f, 3)), 1e-4f);
        }

        [Test]
        public void WallTargetCandidate_DefaultsToFullHealthAndNoEngagement()
        {
            var candidate = new WallTargetCandidate(4, 9f);
            Assert.AreEqual(1f, candidate.HealthRatio);
            Assert.AreEqual(0, candidate.EngagedBreakers);
            var clamped = new WallTargetCandidate(4, 9f, 1.5f, -2);
            Assert.AreEqual(1f, clamped.HealthRatio);
            Assert.AreEqual(0, clamped.EngagedBreakers);
        }

        // ── 사다리 고르기 ─────────────────

        [Test]
        public void LadderScore_RaisedBonusAndDefenderPenalty()
        {
            float plain = LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f);
            Assert.AreEqual(plain, LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f, false, 0, 8f, 6f),
                "세워지지 않고 수비병이 없으면 예전 점수와 같다.");
            Assert.AreEqual(plain - 8f, LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f, true, 0, 8f, 6f));
            Assert.AreEqual(plain + 12f, LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f, false, 2, 8f, 6f));
            Assert.AreEqual(plain, LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f, false, -1, 8f, 6f));
        }

        [Test]
        public void LadderScore_PrefersWeaklyDefendedRaisedLadder_OneLadderOver()
        {
            // 가까운 사다리(20m)는 세워졌지만 수비병 2명, 옆 사다리(28m)는 세워졌고 수비병이 없다.
            float near = LadderChoiceRules.Score(20f, 0, false, false, 4f, 30f, true, 2, 8f, 6f);
            float far = LadderChoiceRules.Score(28f, 0, false, false, 4f, 30f, true, 0, 8f, 6f);
            Assert.Less(far, near);
            int best = LadderChoiceRules.NextBest(new[] { near, far }, new bool[2], 2);
            Assert.AreEqual(1, best);
        }
    }
}
