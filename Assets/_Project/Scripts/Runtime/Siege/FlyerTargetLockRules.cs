using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 비행 적이 비슷한 거리의 대상 사이를 오가며 공중에서 자리를 바꾸지 않게 하는 규칙(SIEGE-32). 순수 규칙이다.
    /// 대상을 고른 뒤 잠금 시간 동안은 그 대상을 지킨다. 잠금이 끝나면 지금 대상을 <c>margin</c>(m)만큼 가깝게 친 채
    /// 전략을 다시 돌려, 그래도 다른 대상이 뽑힐 때만 바꾼다. 그래서 전략의 우선순위(성벽 위 → 대포 → 나머지)는 그대로이고,
    /// 같은 순위 안에서만 거리 차이가 <c>margin</c>을 넘어야 바뀐다. 순위가 더 높은 대상(성벽 위 아군, 먼저 노릴 대상 SIEGE-09 등)이
    /// 새로 나타나면 잠금과 관계없이 바꾼다. 잠금은 같은 순위 안의 흔들림만 막는다.
    /// </summary>
    public static class FlyerTargetLockRules
    {
        /// <summary>
        /// 이번 판단에 노릴 후보 번호. <paramref name="currentIndex"/>는 지금 대상이 후보 목록에 있는 번호이고 없으면 -1이다.
        /// <paramref name="candidates"/>는 잠깐 바꿨다가 되돌린다.
        /// </summary>
        public static int Select(List<FlyerTargetCandidate> candidates, FlyerTargetStrategy strategy, bool usePriority,
            int currentIndex, float lockRemaining, float margin)
        {
            int fresh = Pick(candidates, strategy, usePriority);
            if (fresh < 0 || currentIndex < 0 || currentIndex >= candidates.Count || fresh == currentIndex) return fresh;
            FlyerTargetCandidate current = candidates[currentIndex];
            if (!IsValidDistance(current.DistanceSquared)) return fresh;
            // 지금 대상을 거리 0으로 쳐도 다른 대상이 뽑히면, 거리가 아닌 순위(성벽 위·먼저 노릴 대상 등)에서 앞선 것이다.
            // 그런 대상은 잠금과 관계없이 바로 노린다.
            if (PickWithDistance(candidates, strategy, usePriority, currentIndex, 0f) != currentIndex) return fresh;
            if (lockRemaining > 0f) return currentIndex;
            if (margin <= 0f) return fresh;
            float biased = Mathf.Max(0f, Mathf.Sqrt(current.DistanceSquared) - margin);
            return PickWithDistance(candidates, strategy, usePriority, currentIndex, biased * biased);
        }

        static int PickWithDistance(List<FlyerTargetCandidate> candidates, FlyerTargetStrategy strategy, bool usePriority,
            int index, float distanceSquared)
        {
            FlyerTargetCandidate original = candidates[index];
            candidates[index] = new FlyerTargetCandidate(original.Id, distanceSquared, original.OnWallTop,
                original.IsPriority, original.IsSiegeWeapon);
            int chosen = Pick(candidates, strategy, usePriority);
            candidates[index] = original;
            return chosen;
        }

        static int Pick(IReadOnlyList<FlyerTargetCandidate> candidates, FlyerTargetStrategy strategy, bool usePriority) =>
            usePriority ? FlyerPriorityTargeting.Select(candidates, strategy)
                : strategy != null ? strategy.Select(candidates) : -1;

        static bool IsValidDistance(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
