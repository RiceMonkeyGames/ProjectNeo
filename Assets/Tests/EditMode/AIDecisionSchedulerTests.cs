using NUnit.Framework;
using ProjectNeo.Combat;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-query-budget: 판단 주기 분산, 프레임당 길찾기 예산, 상위 K개 후보 정렬 버퍼.</summary>
    public class AIDecisionSchedulerTests
    {
        [Test]
        public void InitialOffset_DistributesUnitsAcrossBuckets_WithZeroForFirstBucket()
        {
            Assert.AreEqual(0f, AIDecisionScheduler.InitialOffset(0, 8, 0.24f), 1e-5f);
            Assert.AreEqual(0.03f, AIDecisionScheduler.InitialOffset(1, 8, 0.24f), 1e-5f);
            Assert.AreEqual(0.21f, AIDecisionScheduler.InitialOffset(7, 8, 0.24f), 1e-5f);
            Assert.AreEqual(0f, AIDecisionScheduler.InitialOffset(8, 8, 0.24f), 1e-5f);
            Assert.AreEqual(0f, AIDecisionScheduler.InitialOffset(3, 1, 0.24f), 1e-5f);
            Assert.AreEqual(0f, AIDecisionScheduler.InitialOffset(-1, 8, 0.24f), 1e-5f);
        }

        [Test]
        public void PathBudget_LimitsQueriesPerFrame_AndResetsOnNewFrame()
        {
            var scheduler = new AIDecisionScheduler();
            Assert.IsTrue(scheduler.HasPathBudget(10, 2));
            Assert.IsTrue(scheduler.TryConsumePathToken(10, 2));
            Assert.IsTrue(scheduler.TryConsumePathToken(10, 2));
            Assert.IsFalse(scheduler.HasPathBudget(10, 2));
            Assert.IsFalse(scheduler.TryConsumePathToken(10, 2));
            Assert.AreEqual(2, scheduler.QueriesThisFrame);
            Assert.AreEqual(2, scheduler.TotalQueries);

            // 다음 프레임으로 넘어가면 프레임 예산이 다시 열린다.
            Assert.IsTrue(scheduler.HasPathBudget(11, 2));
            Assert.IsTrue(scheduler.TryConsumePathToken(11, 2));
            Assert.AreEqual(1, scheduler.QueriesThisFrame);
            Assert.AreEqual(3, scheduler.TotalQueries);

            // maxPerFrame <= 0이면 제한 없이 통과한다.
            Assert.IsTrue(scheduler.HasPathBudget(11, 0));
            Assert.IsTrue(scheduler.TryConsumePathToken(11, 0));
            Assert.AreEqual(4, scheduler.TotalQueries);
        }

        [Test]
        public void TargetCandidateBuffer_KeepsTopKInAscendingScoreOrder()
        {
            var buffer = new TargetCandidateBuffer<string>(3);
            buffer.Reset(3);
            Assert.AreEqual(float.PositiveInfinity, buffer.WorstScore);

            Assert.IsTrue(buffer.TryAdd("Far", 50f));
            Assert.IsTrue(buffer.TryAdd("Mid", 20f));
            Assert.IsTrue(buffer.TryAdd("Near", 5f));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(50f, buffer.WorstScore);
            Assert.AreEqual("Near", buffer[0]);
            Assert.AreEqual("Mid", buffer[1]);
            Assert.AreEqual("Far", buffer[2]);

            // 버퍼가 꽉 찼을 때 최악(50) 이상인 후보는 거절된다.
            Assert.IsFalse(buffer.TryAdd("TooFar", 50f));
            Assert.IsFalse(buffer.TryAdd("WayTooFar", 80f));

            // 더 나은 후보(12)가 들어오면 50이 밀려나고 순서가 유지된다.
            Assert.IsTrue(buffer.TryAdd("SecondNear", 12f));
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual("Near", buffer[0]);
            Assert.AreEqual("SecondNear", buffer[1]);
            Assert.AreEqual("Mid", buffer[2]);
            Assert.AreEqual(20f, buffer.WorstScore);
        }

        [Test]
        public void TargetCandidateBuffer_PreservesInsertionOrder_OnEqualScores()
        {
            var buffer = new TargetCandidateBuffer<string>(2);
            buffer.Reset(2);
            Assert.IsTrue(buffer.TryAdd("First", 10f));
            Assert.IsTrue(buffer.TryAdd("Second", 10f));
            Assert.IsFalse(buffer.TryAdd("Third", 10f));
            Assert.AreEqual("First", buffer[0]);
            Assert.AreEqual("Second", buffer[1]);
        }
    }
}
