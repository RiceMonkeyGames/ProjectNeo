using System.Collections.Generic;
using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Town
{
    /// <summary>성 안 주민(SIEGE-36)의 순수 규칙: 다음 산책 지점, 쉬는 시간, 가장 가까운 피신처.</summary>
    public static class TownRules
    {
        /// <summary>지금 지점(<paramref name="current"/>, 없으면 -1)을 뺀 나머지 중 하나를 고른다. 지점이 없으면 -1.</summary>
        public static int NextWaypoint(int current, int count, IRandomSource random)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (current < 0 || current >= count) return random.NextInt(count);
            int next = random.NextInt(count - 1);
            return next >= current ? next + 1 : next;
        }

        /// <summary>한 지점에서 쉬는 시간(초). min~max 사이.</summary>
        public static float IdleSeconds(float min, float max, IRandomSource random)
        {
            if (max < min) (min, max) = (max, min);
            return min + (max - min) * random.NextFloat();
        }

        /// <summary>바닥 거리로 가장 가까운 피신처 번호. 없으면 -1.</summary>
        public static int NearestShelter(Vector3 from, IReadOnlyList<Vector3> shelters)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < shelters.Count; i++)
            {
                Vector3 delta = shelters[i] - from;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }
    }
}
