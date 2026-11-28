using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 운반병이 사다리를 걸 빈틈을 고르는 규칙(SIEGE-37). 낮을수록 좋다. 거리에, 근처(몰림 반경 안)에 이미 서 있거나
    /// 다른 운반병이 고른 사다리 하나마다 몰림 벌점, 위쪽 끝 둘레의 수비병 한 명마다 수비 벌점을 더한다.
    /// 이미 고른 자리는 조금 깎아 이리저리 옮기지 않게 한다. 다른 사다리와 너무 가까운 자리는 쓰지 않는다. 씬을 알지 않는다.
    /// </summary>
    public static class LadderSlotRules
    {
        public static float Score(float distance, int nearbyLadders, int defendersAtTop, bool isCurrent,
            float crowdPenalty, float defenderPenalty)
        {
            float score = distance + Mathf.Max(0, nearbyLadders) * crowdPenalty
                + Mathf.Max(0, defendersAtTop) * defenderPenalty;
            if (isCurrent) score -= crowdPenalty;
            return score;
        }

        /// <summary>
        /// 그 자리의 위쪽 끝이 다른 사다리(서 있거나 고른 자리)의 위쪽 끝과 수평으로 <paramref name="spacing"/>보다 가까우면 쓸 수 없다.
        /// </summary>
        public static bool IsFree(Vector3 slotTop, IReadOnlyList<Vector3> takenTops, float spacing)
        {
            if (takenTops == null) return true;
            float limit = spacing * spacing;
            for (int i = 0; i < takenTops.Count; i++)
                if (FlatSqrDistance(slotTop, takenTops[i]) < limit) return false;
            return true;
        }

        /// <summary>그 자리에서 수평으로 <paramref name="radius"/> 안에 있는 다른 사다리 수.</summary>
        public static int CountNearby(Vector3 slotTop, IReadOnlyList<Vector3> takenTops, float radius)
        {
            if (takenTops == null || radius <= 0f) return 0;
            float limit = radius * radius;
            int count = 0;
            for (int i = 0; i < takenTops.Count; i++)
                if (FlatSqrDistance(slotTop, takenTops[i]) <= limit) count++;
            return count;
        }

        static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
