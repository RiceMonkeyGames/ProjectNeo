using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;

namespace ProjectNeo.Tests
{
    public class SiegeRulesTests
    {
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
        public void Ladder_RaisesOverTime_AndFallsBackWhenRaiserLeaves()
        {
            var ladder = new SiegeLadderState(3f, 2f);
            Assert.IsFalse(ladder.TryBeginClimb());
            ladder.Tick(0.1f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            ladder.Tick(1.5f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            ladder.Tick(0.1f, false, false, false);
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
            ladder.Toppled += hadClimber => droppedClimber = hadClimber;
            // 올라온 적이 있으면 밀지 못하고 진행도도 쌓이지 않는다.
            ladder.Tick(5f, false, true, true);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
            Assert.AreEqual(0f, ladder.PushProgress);
            ladder.Tick(1f, false, true, false);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
            // 밀다가 멈추면 처음부터 다시 민다.
            ladder.Tick(0.1f, false, false, false);
            Assert.AreEqual(0f, ladder.PushProgress);
            ladder.Tick(2f, false, true, false);
            Assert.AreEqual(LadderPhase.Down, ladder.Phase);
            Assert.AreEqual(true, droppedClimber);
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

        [TestCase(3f, 0.5f)]   // 성벽 위를 올려 쏘면 절반
        [TestCase(1.5f, 0.5f)]
        [TestCase(1f, 1f)]     // 비슷한 높이는 그대로
        [TestCase(-3f, 1f)]    // 내려 쏘면 그대로
        public void UpwardShot_HalvesDamageOnlyAboveMinimumRise(float rise, float expected)
        {
            Assert.AreEqual(expected, DamageRules.ElevationMultiplier(rise, 1.5f, 0.5f));
        }
    }
}
