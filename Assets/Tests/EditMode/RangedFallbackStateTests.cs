using NUnit.Framework;
using ProjectNeo.Combat;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-alloc-and-split: 원거리 근접 위협 후퇴 상태(시작·진행·마침·취소·대기 시간) 규칙.</summary>
    public class RangedFallbackStateTests
    {
        [Test]
        public void Advance_RunsUntilTimeLimit()
        {
            var state = new RangedFallbackState();
            state.Begin(1f);
            Assert.IsTrue(state.IsActive);
            Assert.IsTrue(state.Advance(0.6f));
            Assert.IsFalse(state.Advance(0.6f), "후퇴 시간을 다 쓰면 false.");
        }

        [Test]
        public void Finish_StartsCooldownAndCancelKeepsIt()
        {
            var state = new RangedFallbackState();
            state.Begin(1f);
            state.Finish(2f);
            Assert.IsFalse(state.IsActive);
            Assert.IsTrue(state.IsCoolingDown);
            state.Begin(1f);
            state.Cancel();
            Assert.IsFalse(state.IsActive);
            Assert.IsFalse(state.Advance(0.1f), "취소한 뒤에는 진행하지 않는다.");
            Assert.IsTrue(state.IsCoolingDown, "취소는 대기 시간을 지우지 않는다.");
            state.TickCooldown(2.5f);
            Assert.IsFalse(state.IsCoolingDown);
            Assert.AreEqual(0f, state.CooldownRemaining);
        }

        [Test]
        public void StartCooldown_ClampsNegative()
        {
            var state = new RangedFallbackState();
            state.StartCooldown(-1f);
            Assert.IsFalse(state.IsCoolingDown);
        }
    }
}
