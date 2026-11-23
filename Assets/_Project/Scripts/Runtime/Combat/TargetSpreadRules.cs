using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 대상 분산 점수(COMBAT-17, 사용자 결정 2026-11-23, 값은 플레이 후 조정하는 임시 값).
    /// 배율은 거리 배율이라 점수(제곱 거리)에는 제곱으로 들어간다(<see cref="RangedTargetPriorityRules.Score"/>).
    /// 1) 근접: 대상의 안쪽 근접 자리가 다른 유닛으로 다 찼으면 그 대상을 더 멀리 있는 것처럼 본다.
    /// 2) 원거리: 일반 대상을 이미 쏘는 다른 원거리 유닛이 기준 수 이상이면 한 명 넘을 때마다 더 멀리 있는 것처럼 본다.
    ///    사다리 적·성문/성벽 공격 적·보스는 계속 집중해서 쏜다.
    /// </summary>
    public static class TargetSpreadRules
    {
        /// <summary>
        /// 근접 혼잡 배율. 다른 유닛이 맡은 안쪽 자리 수(<paramref name="takenByOthers"/>)가 안쪽 자리 수
        /// (<paramref name="innerCapacity"/>) 이상이면 <paramref name="congestedFactor"/>(1 이상), 아니면 1.
        /// </summary>
        public static float CongestionFactor(int takenByOthers, int innerCapacity, float congestedFactor) =>
            innerCapacity > 0 && takenByOthers >= innerCapacity ? Mathf.Max(1f, congestedFactor) : 1f;

        /// <summary>화력을 나누지 않고 계속 집중해서 쏠 대상인지(사다리 적, 성문·성벽 공격 적, 보스).</summary>
        public static bool IsFocusTarget(TargetThreat threat, bool isBoss) =>
            isBoss || threat == TargetThreat.LadderClimber || threat == TargetThreat.SiegeBreaker;

        /// <summary>
        /// 원거리 과잉 사격 배율. 그 대상을 쏘는 다른 원거리 유닛(<paramref name="otherShooters"/>)이
        /// <paramref name="threshold"/>명 이상이면(내가 기준을 넘는 사수가 되면) 넘는 사수 한 명마다
        /// <paramref name="stepFactor"/>(1 이상)를 곱한다. 집중 대상이거나 기준이 0 이하면 1.
        /// </summary>
        public static float OverkillFactor(int otherShooters, int threshold, float stepFactor, bool focusTarget)
        {
            if (focusTarget || threshold <= 0 || otherShooters < threshold) return 1f;
            return Mathf.Pow(Mathf.Max(1f, stepFactor), otherShooters - threshold + 1);
        }
    }
}
