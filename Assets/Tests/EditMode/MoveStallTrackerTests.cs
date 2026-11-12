using NUnit.Framework;
using ProjectNeo.Combat;

namespace ProjectNeo.Tests
{
    public class MoveStallTrackerTests
    {
        const float Near = 1.5f, Progress = 0.1f, Seconds = 0.5f, Frame = 0.1f;

        static bool Step(MoveStallTracker tracker, float remaining) =>
            tracker.IsStuck(remaining, Frame, Near, Progress, Seconds);

        [Test]
        public void NearDestinationWithoutProgress_IsStuckAfterStallTime()
        {
            var tracker = new MoveStallTracker();
            // 끝점에서 0.5m 떨어진 채 앞 유닛에 막혀 더 가지 못한다.
            for (int i = 0; i < 5; i++) Assert.IsFalse(Step(tracker, 0.5f), "막힌 시간이 짧으면 아직 도착이 아니다.");
            Assert.IsTrue(Step(tracker, 0.5f));
        }

        [Test]
        public void SteadyProgress_IsNeverStuck()
        {
            var tracker = new MoveStallTracker();
            for (float remaining = 1.4f; remaining > 0.4f; remaining -= 0.15f)
                Assert.IsFalse(Step(tracker, remaining));
        }

        [Test]
        public void FarFromDestination_IsNeverStuck()
        {
            // 멀리서 잠깐 막힌 것(예: 길목 혼잡)은 도착으로 보지 않는다.
            var tracker = new MoveStallTracker();
            for (int i = 0; i < 20; i++) Assert.IsFalse(Step(tracker, 5f));
        }

        [Test]
        public void JitterSmallerThanProgress_StillCountsAsStuck()
        {
            // 서로 밀며 제자리에서 조금씩 흔들리는 것은 나아가는 것으로 보지 않는다.
            var tracker = new MoveStallTracker();
            float[] jitter = { 0.60f, 0.58f, 0.61f, 0.57f, 0.59f, 0.56f };
            bool stuck = false;
            foreach (float remaining in jitter) stuck = Step(tracker, remaining);
            Assert.IsTrue(stuck);
        }

        [Test]
        public void ProgressAfterStall_RestartsTheTimer()
        {
            var tracker = new MoveStallTracker();
            for (int i = 0; i < 4; i++) Step(tracker, 0.8f);
            Assert.IsFalse(Step(tracker, 0.5f), "다시 가까워지면 막힌 시간을 처음부터 센다.");
            Assert.AreEqual(0f, tracker.StalledSeconds);
            Assert.IsFalse(Step(tracker, float.PositiveInfinity), "남은 거리를 모르면 막힘을 세지 않는다.");
            Assert.AreEqual(0f, tracker.StalledSeconds);
        }
    }
}
