using System;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 세워진 사다리를 균형점까지 미는 시간 규칙. 혼자·빈 사다리 기준 시간에
    /// 탑승자 무게 배율을 곱하고, 함께 미는 인원 보너스만큼 빨라진다. 씬을 알지 않는다.
    /// </summary>
    public sealed class LadderPushTuning
    {
        public float HoldSeconds { get; }
        public float OccupiedMultiplier { get; }
        public float TwoPusherBonus { get; }
        public float ThreePlusPusherBonus { get; }

        public LadderPushTuning(float holdSeconds, float occupiedMultiplier, float twoPusherBonus, float threePlusPusherBonus)
        {
            HoldSeconds = Math.Max(0.01f, holdSeconds);
            OccupiedMultiplier = Math.Max(1f, occupiedMultiplier);
            TwoPusherBonus = Math.Max(0f, twoPusherBonus);
            // 인원이 늘어 보너스가 줄어드는 일은 없게 한다.
            ThreePlusPusherBonus = Math.Max(TwoPusherBonus, threePlusPusherBonus);
        }

        /// <summary>함께 미는 인원에 따른 속도 보너스. 1명 0, 2명 TwoPusherBonus, 3명 이상은 상한.</summary>
        public float CooperationBonus(int pushers)
            => pushers >= 3 ? ThreePlusPusherBonus : pushers == 2 ? TwoPusherBonus : 0f;

        public float SecondsToBalance(int pushers, bool occupied)
        {
            if (pushers <= 0) return float.PositiveInfinity;
            float seconds = HoldSeconds * (occupied ? OccupiedMultiplier : 1f);
            return seconds / (1f + CooperationBonus(pushers));
        }
    }
}
