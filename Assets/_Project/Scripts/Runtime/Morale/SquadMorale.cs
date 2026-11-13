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

        // 비율 감소를 여러 번 더하면 부동소수 오차로 0 근처의 아주 작은 값이 남을 수 있어 0으로 본다.
        const float ZeroTolerance = 0.001f;

        float Clamp(float value) => value < ZeroTolerance ? 0f : value > Max ? Max : value;
    }
}
