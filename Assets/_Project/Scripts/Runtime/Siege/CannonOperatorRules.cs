using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>직접 조작 규칙. 대포에 탈 수 있는지, 조준 시점을 회전 범위 안으로 자르는지 판정한다.</summary>
    public static class CannonOperatorRules
    {
        /// <summary>수평 거리 안이고 카메라 정면 각도 안에 있으면 탈 수 있다.</summary>
        public static bool IsWithinReach(Vector3 viewer, Vector3 viewForward, Vector3 cannon, float distance, float halfAngle)
        {
            Vector3 offset = cannon - viewer;
            offset.y = 0f;
            if (offset.magnitude > Mathf.Max(0f, distance)) return false;
            if (offset.sqrMagnitude < 0.0001f) return true;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(viewForward, offset) <= Mathf.Clamp(halfAngle, 0f, 180f) + 0.001f;
        }

        /// <summary>조작 중 시점 방향(도)을 대포 정면 기준 회전 한계 안으로 자른다.</summary>
        public static float ClampViewYaw(float viewYaw, float facingYaw, float halfAngle) =>
            facingYaw + CannonAim.ClampYawOffset(Mathf.DeltaAngle(facingYaw, viewYaw), halfAngle);
    }
}
