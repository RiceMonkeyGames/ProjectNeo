using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽 파괴병이 성벽 면을 따라 나눠 설 자리(SIEGE-17). 면 길이와 간격만으로 정하므로 칸 크기와 무관하다.
    /// 다른 파괴병이 이미 잡은 자리 근처는 벌점을 받아, 여러 명이 한 자리로 몰리지 않고 면을 따라 함께 친다.
    /// </summary>
    public static class WallBreakerSpreadRules
    {
        /// <summary>면 하나에 놓을 자리 수. 양 끝에서 <paramref name="margin"/>만큼 띄우고 최소 간격 <paramref name="spacing"/>를 지킨다.</summary>
        public static int SlotCount(float faceLength, float margin, float spacing)
        {
            float usable = faceLength - 2f * Mathf.Max(0f, margin);
            if (usable <= 0f || spacing <= 0f) return 1;
            return 1 + Mathf.FloorToInt(usable / spacing + 0.0001f);
        }

        /// <summary>면 시작점에서 <paramref name="index"/>번째 자리까지의 거리. 자리를 쓸 수 있는 구간에 고르게 편다.</summary>
        public static float SlotOffset(int index, int count, float faceLength, float margin)
        {
            margin = Mathf.Max(0f, margin);
            float usable = faceLength - 2f * margin;
            if (count <= 1 || usable <= 0f) return faceLength * 0.5f;
            return margin + usable * Mathf.Clamp(index, 0, count - 1) / (count - 1);
        }

        /// <summary>자리에서 <paramref name="spacing"/>보다 가까운, 다른 파괴병이 잡은 자리 수(수평 거리).</summary>
        public static int CountClaims(Vector3 slot, IReadOnlyList<Vector3> claims, float spacing)
        {
            int count = 0;
            float limit = spacing * spacing;
            for (int i = 0; i < claims.Count; i++)
            {
                Vector3 delta = claims[i] - slot;
                delta.y = 0f;
                if (delta.sqrMagnitude < limit) count++;
            }
            return count;
        }

        /// <summary>낮을수록 좋다. 거리에 이미 잡힌 자리 하나당 벌점을 더한다.</summary>
        public static float Score(float distance, int claims, float claimPenalty) =>
            distance + Mathf.Max(0, claims) * Mathf.Max(0f, claimPenalty);
    }
}
