using System;

namespace ProjectNeo.Morale
{
    /// <summary>
    /// 부대 하나의 사기 값. 0과 최대값 사이로 유지된다.
    /// 감소, 도망 같은 규칙은 전투 프로토타입에서 이 값 위에 붙인다.
    /// </summary>
    public sealed class SquadMorale
    {
        public float Max { get; }
        public float Value { get; private set; }

        public event Action Changed;

        public SquadMorale(float max, float start)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Value = Clamp(start);
        }

        /// <summary>사기를 올리거나(양수) 내린다(음수).</summary>
        public void Add(float amount)
        {
            float next = Clamp(Value + amount);
            if (next == Value) return;
            Value = next;
            Changed?.Invoke();
        }

        float Clamp(float value) => value < 0f ? 0f : value > Max ? Max : value;
    }
}
