using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>중지 임무(<see cref="HoldDuty"/>)의 순수 판정(SIEGE-31).</summary>
    public static class HoldDutyRules
    {
        /// <summary>위치가 중심에서 수평 반경 안, 높이 차 허용치 안에 있는지.</summary>
        public static bool IsNear(Vector3 position, Vector3 center, float radius, float heightTolerance)
        {
            Vector3 offset = position - center;
            if (Mathf.Abs(offset.y) > heightTolerance) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <summary>임무 자리에 도착했는지(수평 거리만 본다).</summary>
        public static bool HasArrived(Vector3 position, Vector3 point, float arrivalDistance)
        {
            Vector3 offset = point - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= arrivalDistance * arrivalDistance;
        }
    }
}
