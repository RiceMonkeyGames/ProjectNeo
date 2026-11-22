using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 아군 분대가 중지(Hold) 상태에서 대기 중일 때, 분대 전체를 감싸는 바운딩 원과 최대 탐지 반경을 계산하고
    /// 그 안에 적이 한 명도 없으면 분대원 개별의 타겟 탐색을 건너뛰게 하는 순수 규칙(PERF-siege-ai-scale).
    /// </summary>
    public static class SquadThreatWatch
    {
        /// <summary>
        /// 분대원 위치 목록(<paramref name="memberPositions"/>)과 각 분대원의 탐색 반경(<paramref name="memberScanRadii"/>)으로
        /// 분대 전체의 탐색 중심점(<paramref name="center"/>)과 모든 분대원의 탐색 범위를 완전히 덮는 외접 반경(<paramref name="totalRadius"/>)을 구한다.
        /// 분대원이 없으면 false를 돌려준다.
        /// </summary>
        public static bool TryComputeBounds(
            IReadOnlyList<Vector3> memberPositions,
            IReadOnlyList<float> memberScanRadii,
            out Vector3 center,
            out float totalRadius)
        {
            center = Vector3.zero;
            totalRadius = 0f;
            if (memberPositions == null || memberPositions.Count == 0) return false;

            int count = memberPositions.Count;
            for (int i = 0; i < count; i++)
                center += memberPositions[i];
            center /= count;

            float maxReach = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = memberPositions[i] - center;
                offset.y = 0f;
                float scan = memberScanRadii != null && i < memberScanRadii.Count ? memberScanRadii[i] : 0f;
                float reach = offset.magnitude + (scan > 0f ? scan : 0f);
                if (reach > maxReach) maxReach = reach;
            }
            totalRadius = maxReach;
            return true;
        }

        /// <summary>
        /// 분대 단위로 주변이 깨끗한지(<paramref name="enemyCountInSquadSphere"/> == 0) 판정해
        /// 개별 분대원이 대기 탐색 생략(Sleep)에 들어갈 수 있는지 돌려준다.
        /// </summary>
        public static bool CanSleepInHold(bool squadInHoldOrder, int activeMemberCount, bool anyMemberHasTarget, int enemyCountInSquadSphere) =>
            squadInHoldOrder && activeMemberCount > 0 && !anyMemberHasTarget && enemyCountInSquadSphere <= 0;
    }
}
