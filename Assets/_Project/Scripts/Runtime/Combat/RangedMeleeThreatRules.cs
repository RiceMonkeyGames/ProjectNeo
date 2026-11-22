using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 근접 적이 원거리 유닛에게 붙었을 때의 대응(COMBAT-14, 사용자 결정 2026-11-22: 대상 전환과 짧은 후퇴를 함께 쓴다).
    /// 1) 위협 반경 안의 근접 적을 대상 유지 시간과 상관없이 바로 대상으로 삼는다.
    /// 2) 물러날 공간이 있으면 그 적의 반대쪽으로 짧게 물러난 뒤 다시 쏜다.
    /// </summary>
    public static class RangedMeleeThreatRules
    {
        /// <summary>원거리 유닛에게 근접 적이 위협 반경 안(경계 포함)에 들어왔는지. 반경이 0이면 대응하지 않는다.</summary>
        public static bool IsMeleeThreat(bool selfRanged, bool candidateMelee, float sqrDistance, float radius) =>
            selfRanged && candidateMelee && radius > 0f && sqrDistance <= radius * radius;

        /// <summary>
        /// 위협에서 수평으로 멀어지는 쪽으로 <paramref name="distance"/>만큼 간 지점. 위치가 겹치면 <paramref name="fallbackBack"/>
        /// (보통 유닛 뒤쪽)으로 간다. 높이는 지금 위치를 쓴다.
        /// </summary>
        public static Vector3 FallbackPoint(Vector3 self, Vector3 threat, Vector3 fallbackBack, float distance)
        {
            Vector3 away = self - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = fallbackBack;
                away.y = 0f;
            }
            if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            return self + away.normalized * Mathf.Max(0f, distance);
        }

        /// <summary>
        /// 후퇴 지점을 써도 되는지. 지점이 지금 높이에서 <paramref name="levelTolerance"/> 넘게 다르면 다른 층이라 쓰지 않는다.
        /// 자리를 지키는 중(<paramref name="holding"/>)이면 자리에서 수평으로 <paramref name="postLeash"/> 안이어야 한다.
        /// </summary>
        public static bool AllowsFallback(Vector3 self, Vector3 point, float levelTolerance,
            bool holding, Vector3 post, float postLeash)
        {
            if (Mathf.Abs(point.y - self.y) > levelTolerance) return false;
            if (!holding) return true;
            Vector3 offset = point - post;
            offset.y = 0f;
            return offset.sqrMagnitude <= postLeash * postLeash;
        }

        /// <summary>후퇴 경로가 너무 돌아가지 않는지. 경로 길이가 곧은 거리의 <paramref name="maxDetourRatio"/>배 이하여야 한다.</summary>
        public static bool IsShortPath(float pathLength, float straightDistance, float maxDetourRatio) =>
            pathLength <= Mathf.Max(0.01f, straightDistance) * Mathf.Max(1f, maxDetourRatio);
    }
}
