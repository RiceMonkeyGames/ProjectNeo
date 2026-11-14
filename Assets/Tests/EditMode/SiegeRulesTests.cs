using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class SiegeRulesTests
    {
        [TestCase(3, 0, false, true, true)]
        [TestCase(3, 1, false, true, false)]
        [TestCase(3, 0, true, true, false)]
        [TestCase(3, 0, false, false, false)]
        [TestCase(0, 0, false, true, false)]
        public void Retreat_RequiresLivingEnemiesWithoutSiegeUnitsOrEntryRoute(int enemies, int siegeUnits,
            bool route, bool navigationReady, bool expected)
        {
            Assert.AreEqual(expected, SiegeOutcomeRules.ShouldRetreat(enemies, siegeUnits, route, navigationReady));
        }

        [Test]
        public void Budget_SpendsOnlyWhatItHas_AndRefundsUpToTotal()
        {
            var budget = new BuildBudget(10);
            Assert.IsTrue(budget.TrySpend(4));
            Assert.AreEqual(6, budget.Remaining);
            Assert.IsFalse(budget.TrySpend(7));
            Assert.IsFalse(budget.TrySpend(-1));
            Assert.AreEqual(6, budget.Remaining);
            budget.Refund(4);
            Assert.AreEqual(10, budget.Remaining);
            budget.Refund(50);
            Assert.AreEqual(10, budget.Remaining);
        }

        [Test]
        public void Outcome_FollowsDesign()
        {
            // 성문만 부서지고 아군이 남아 있으면 전투는 계속된다.
            Assert.AreEqual(SiegeOutcome.Ongoing, SiegeOutcomeRules.Evaluate(false, true, 3, 5, 0));
            Assert.AreEqual(SiegeOutcome.Defeat, SiegeOutcomeRules.Evaluate(false, true, 0, 5, 0));
            // 성문이 멀쩡하면 아군이 전멸해도 플레이어가 살아 있는 동안은 계속된다.
            Assert.AreEqual(SiegeOutcome.Ongoing, SiegeOutcomeRules.Evaluate(false, false, 0, 5, 0));
            Assert.AreEqual(SiegeOutcome.Defeat, SiegeOutcomeRules.Evaluate(true, false, 8, 5, 0));
            Assert.AreEqual(SiegeOutcome.Victory, SiegeOutcomeRules.Evaluate(false, true, 2, 0, 0));
            // 나올 예정인 적이 남아 있으면 아직 승리가 아니다.
            Assert.AreEqual(SiegeOutcome.Ongoing, SiegeOutcomeRules.Evaluate(false, false, 2, 0, 3));
            // 같은 순간에 둘 다 성립하면 패배(Q-09 확정 전 임시 처리).
            Assert.AreEqual(SiegeOutcome.Defeat, SiegeOutcomeRules.Evaluate(true, false, 2, 0, 0));
        }

        [Test]
        public void Retreat_OnlyWhenNoBreachersAndNoWayIn()
        {
            // 궁수 한 명만 남고 들어올 길이 없으면 물러난다.
            Assert.IsTrue(SiegeOutcomeRules.ShouldRetreat(1, 0, 0, 0));
            // 사다리병이나 성문 파괴병이 남아 있으면 계속 싸운다.
            Assert.IsFalse(SiegeOutcomeRules.ShouldRetreat(3, 1, 0, 0));
            // 걸어서 아군에게 닿는 적이 있으면(뚫린 곳, 무너진 성문) 계속 싸운다.
            Assert.IsFalse(SiegeOutcomeRules.ShouldRetreat(3, 0, 1, 0));
            // 아직 판단하지 않은 적이 있으면 기다린다.
            Assert.IsFalse(SiegeOutcomeRules.ShouldRetreat(3, 0, 0, 1));
            // 남은 적이 없으면 후퇴가 아니라 이미 승리다.
            Assert.IsFalse(SiegeOutcomeRules.ShouldRetreat(0, 0, 0, 0));
        }

        [Test]
        public void Ladder_RaisesOverTime_AndFallsBackWhenRaiserLeaves()
        {
            var ladder = new SiegeLadderState(3f, 2f);
            Assert.IsFalse(ladder.TryBeginClimb());
            ladder.Tick(0.1f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            ladder.Tick(0.75f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            ladder.Tick(0.1f, false, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            Assert.That(ladder.RaiseProgress, Is.GreaterThan(0f).And.LessThan(0.25f));
            Assert.IsTrue(ladder.IsSettling);
            ladder.Tick(3f, false, false, false);
            Assert.AreEqual(LadderPhase.Down, ladder.Phase);
            Assert.AreEqual(0f, ladder.RaiseProgress);
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(3f, true, false, false);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
        }

        [Test]
        public void Ladder_OneClimberAtATime()
        {
            SiegeLadderState ladder = Raised();
            Assert.IsTrue(ladder.TryBeginClimb());
            Assert.IsFalse(ladder.TryBeginClimb());
            ladder.EndClimb();
            Assert.IsTrue(ladder.TryBeginClimb());
        }

        [Test]
        public void Ladder_PushTopplesItAndDropsClimber_UnlessBlocked()
        {
            SiegeLadderState ladder = Raised();
            ladder.TryBeginClimb();
            bool? droppedClimber = null;
            int dislodged = 0;
            ladder.ClimberDislodged += () => dislodged++;
            ladder.Toppled += hadClimber => droppedClimber = hadClimber;
            // 올라온 적이 있으면 밀지 못하고 진행도도 쌓이지 않는다.
            ladder.Tick(5f, false, true, true);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
            Assert.AreEqual(0f, ladder.PushProgress);
            ladder.Tick(0.4f, false, true, false);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
            // 균형점 전에 놓으면 진행도를 순간 초기화하지 않고 천천히 돌아온다.
            ladder.Tick(0.1f, false, false, false);
            Assert.That(ladder.PushProgress, Is.GreaterThan(0f).And.LessThan(0.2f));
            ladder.Tick(2f, false, false, false);
            Assert.AreEqual(0f, ladder.PushProgress);
            Assert.AreEqual(0, dislodged, "균형점 전의 취소로 추락했다.");
            ladder.Tick(2f, false, true, false);
            Assert.AreEqual(LadderPhase.Down, ladder.Phase);
            Assert.AreEqual(1, dislodged);
            Assert.AreEqual(false, droppedClimber, "균형점에서 떨어진 탑승자를 완료 시 다시 떨어뜨렸다.");
            Assert.IsFalse(ladder.HasClimber);
            Assert.AreEqual(1, ladder.ToppleCount);
            // 쓰러진 사다리는 다시 세울 수 있다.
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(3f, true, false, false);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
        }

        static SiegeLadderState Raised()
        {
            var ladder = new SiegeLadderState(3f, 2f);
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(3f, true, false, false);
            return ladder;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Ladder_BalancePoint_AutoCompletesWithoutInput(bool pushing)
        {
            var ladder = new SiegeLadderState(3f, 2f, 0.8f);
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(pushing ? 3f : 2.5f, true, false, false);
            if (pushing) ladder.Tick(0.5f, false, true, false);
            float before = ladder.RaisedFraction;
            Assert.IsTrue(ladder.IsSettling);
            Assert.IsFalse(ladder.TryBeginClimb());
            ladder.Tick(0.1f, false, false, true);
            Assert.That(ladder.RaisedFraction, pushing ? Is.LessThan(before) : Is.GreaterThan(before));
            ladder.Tick(3f, false, false, true);
            Assert.AreEqual(pushing ? LadderPhase.Down : LadderPhase.Up, ladder.Phase);
            Assert.IsFalse(ladder.IsSettling);
            Assert.AreEqual(pushing ? 1 : 0, ladder.ToppleCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Ladder_BeforeBalance_ReturnsContinuously_AndCannotReverseUntilSettled(bool pushing)
        {
            var ladder = new SiegeLadderState(3f, 2f, 0.8f);
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(pushing ? 3f : 1f, true, false, false);
            if (pushing) ladder.Tick(0.2f, false, true, false);
            float before = ladder.RaisedFraction;
            ladder.Tick(0.05f, false, false, false);
            float returning = ladder.RaisedFraction;
            Assert.That(returning, pushing ? Is.GreaterThan(before).And.LessThan(1f)
                : Is.LessThan(before).And.GreaterThan(0f));
            Assert.IsTrue(ladder.IsSettling);
            Assert.IsFalse(ladder.TryBeginClimb());
            // 다시 눌러도 복귀가 끝나기 전에는 반대 방향으로 꺾이지 않는다.
            ladder.Tick(0.05f, true, true, false);
            Assert.That(ladder.RaisedFraction, pushing ? Is.GreaterThan(returning) : Is.LessThan(returning));
            ladder.Tick(3f, false, false, false);
            Assert.AreEqual(pushing ? LadderPhase.Up : LadderPhase.Down, ladder.Phase);
            Assert.IsFalse(ladder.IsSettling);
            Assert.AreEqual(0, ladder.ToppleCount);
            Assert.AreEqual(pushing, ladder.CanBoard);
        }

        [Test]
        public void Ladder_CommittedPush_DropsOccupantAtBalanceOnce_AndForcedCollapseResetsMotion()
        {
            var ladder = Raised();
            Assert.IsTrue(ladder.TryBeginClimb());
            int notifications = 0;
            int dislodged = 0;
            ladder.ClimberDislodged += () =>
            {
                Assert.That(ladder.PushProgress, Is.EqualTo(1f - ladder.BalancePoint).Within(0.0001f));
                Assert.AreEqual(LadderPhase.Up, ladder.Phase);
                dislodged++;
            };
            System.Action<bool> onToppled = occupied => { Assert.IsFalse(occupied); notifications++; };
            ladder.Toppled += onToppled;
            ladder.Tick(1.1f, false, true, false);
            Assert.IsTrue(ladder.IsSettling);
            Assert.IsFalse(ladder.HasClimber);
            Assert.AreEqual(1, dislodged);
            Assert.AreEqual(0, notifications);
            ladder.Tick(1f, false, false, true);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(1, dislodged);
            Assert.IsFalse(ladder.HasClimber);
            ladder.Topple();
            Assert.AreEqual(1, notifications);
            ladder.Tick(0.1f, true, false, false);
            ladder.Tick(2f, true, false, false);
            Assert.IsTrue(ladder.IsSettling);
            // 지지 성벽 파괴나 전투 초기화는 자동 세우기도 즉시 취소해야 한다.
            ladder.Toppled -= onToppled;
            ladder.Topple();
            ladder.Tick(3f, false, false, false);
            Assert.AreEqual(LadderPhase.Down, ladder.Phase);
            Assert.AreEqual(0f, ladder.RaisedFraction);
            Assert.IsFalse(ladder.IsSettling);
        }

        [TestCase(3f, 0.5f)]   // 성벽 위를 올려 쏘면 절반
        [TestCase(1.5f, 0.5f)]
        [TestCase(1f, 1f)]     // 비슷한 높이는 그대로
        [TestCase(-3f, 1f)]    // 내려 쏘면 그대로
        public void UpwardShot_HalvesDamageOnlyAboveMinimumRise(float rise, float expected)
        {
            Assert.AreEqual(expected, DamageRules.ElevationMultiplier(rise, 1.5f, 0.5f));
        }

        [Test]
        public void LadderFallSeparation_MovesOnlyInsideClearanceAndPreservesHeight()
        {
            var basePoint = new Vector3(10f, 0f, 10f);
            var outward = Vector3.forward;
            var inside = new Vector3(10f, 2.2f, 9.9f);

            Vector3 separated = SiegeLadder.ComputeFallSeparation(inside, basePoint, Vector3.up, outward, 0.55f);
            Assert.AreEqual(2.2f, separated.y, 0.001f);
            Assert.GreaterOrEqual(Vector3.Dot(separated - basePoint, outward), 0.55f - 0.001f);

            var alreadyOutside = new Vector3(10f, 1.8f, 11.2f);
            Vector3 unchanged = SiegeLadder.ComputeFallSeparation(alreadyOutside, basePoint, Vector3.up, outward, 0.55f);
            Assert.AreEqual(alreadyOutside, unchanged);
        }
    }
}
