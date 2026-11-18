using System;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 준비 단계 하나의 시계(Q-53, PREP-06). 저절로 흐르지 않고, 시간을 쓰는 행동만 앞으로 보낸다.
    /// 예산을 넘겨 쓸 수 없고, 다 쓰면 시간을 쓰는 행동만 막힌다. 씬과 화면을 모른다.
    /// </summary>
    public sealed class PrepClock
    {
        /// <summary>시간 비교 허용 오차. 0.1시간 단위 같은 값이 부동소수 오차로 모자라 보이지 않게 한다.</summary>
        public const float Epsilon = 0.0001f;

        public float Budget { get; private set; }
        public float Used { get; private set; }
        public float Remaining => Math.Max(0f, Budget - Used);
        public bool IsExhausted => Remaining <= Epsilon;

        /// <summary>시계가 앞으로 갔다. 인자는 이번에 흐른 시간이다(SIEGE-08 작업 진행이 듣는다).</summary>
        public event Action<float> Advanced;
        /// <summary>새 준비로 예산이 다시 찼다.</summary>
        public event Action Reset;

        public PrepClock(float budget = 0f) { Budget = Math.Max(0f, budget); }

        /// <summary>이만큼 쓸 수 있는가. 0 이하는 언제나 된다.</summary>
        public bool CanSpend(float hours) => hours <= 0f || hours <= Remaining + Epsilon;

        /// <summary>시간을 쓰고 시계를 보낸다. 모자라면 아무것도 바꾸지 않고 false다. 0 이하는 아무 일 없이 true다.</summary>
        public bool TrySpend(float hours)
        {
            if (hours <= 0f) return true;
            if (!CanSpend(hours)) return false;
            Used = Math.Min(Budget, Used + hours);
            Advanced?.Invoke(hours);
            return true;
        }

        /// <summary>새 준비를 시작한다. 쓴 시간은 0, 예산은 새 값이다. 남은 시간은 넘어가지 않는다.</summary>
        public void Begin(float budget)
        {
            Budget = Math.Max(0f, budget);
            Used = 0f;
            Reset?.Invoke();
        }

        /// <summary>저장한 값으로 되돌린다(FLOW-02). 이벤트를 내지 않는다.</summary>
        public void Restore(float budget, float used)
        {
            Budget = Math.Max(0f, budget);
            Used = Math.Min(Budget, Math.Max(0f, used));
        }
    }
}
