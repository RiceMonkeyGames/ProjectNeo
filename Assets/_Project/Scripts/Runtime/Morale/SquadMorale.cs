using System;

namespace ProjectNeo.Morale
{
    /// <summary>
    /// 부대 하나의 사기 값. 0과 최대값 사이로 유지되며 런 동안 부대에 붙어 다음 전투로 이어진다.
    /// 전투 중 감소·한계 판정·도망 규칙은 <see cref="BattleSquadMorale"/>, 전투 후 회복은 <see cref="MoraleRules"/>.
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
