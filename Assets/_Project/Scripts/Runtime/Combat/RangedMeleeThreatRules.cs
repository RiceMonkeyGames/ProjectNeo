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
        public static Vector3 FallbackPoint(Vector3 self, Vector3 threat, Vector3 fallbackBack, float distance) =>
            FallbackPoint(self, threat, fallbackBack, distance, 0f);

        /// <summary>
        /// 후퇴 방향 후보 수(COMBAT-16). 성벽 통로나 모서리처럼 바로 뒤가 비어 있으면 옆으로 비켜서도록
        /// 정반대 방향부터 0°, +45°, -45°, +90°, -90° 순서로 찾는다.
        /// </summary>
        public const int FallbackCandidateCount = 5;

        /// <summary><paramref name="index"/>번째 후보의 회전 각도(위에서 볼 때 시계 방향 +). 범위 밖이면 0.</summary>
        public static float FallbackAngle(int index)
        {
            switch (index)
            {
                case 1: return 45f;
                case 2: return -45f;
                case 3: return 90f;
                case 4: return -90f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 위협 반대 방향을 수평으로 <paramref name="angleDegrees"/>만큼 돌린 쪽으로 <paramref name="distance"/>만큼 간 지점.
        /// ±90°까지는 돌려도 위협에서 수평으로 멀어진다(NavMesh에 맞춘 뒤에는 <see cref="IncreasesSeparation"/>으로 다시 본다).
        /// 높이는 지금 위치를 쓴다.
        /// </summary>
        public static Vector3 FallbackPoint(Vector3 self, Vector3 threat, Vector3 fallbackBack, float distance,
            float angleDegrees)
        {
            Vector3 away = self - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = fallbackBack;
                away.y = 0f;
            }
            if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            Vector3 direction = Quaternion.AngleAxis(angleDegrees, Vector3.up) * away.normalized;
            return self + direction * Mathf.Max(0f, distance);
        }

        /// <summary>지점이 지금 위치보다 위협에서 수평으로 확실히 더 먼지(같은 거리는 아니다).</summary>
        public static bool IncreasesSeparation(Vector3 self, Vector3 threat, Vector3 point)
        {
            Vector3 before = self - threat, after = point - threat;
            before.y = 0f;
            after.y = 0f;
            return after.sqrMagnitude > before.sqrMagnitude + 0.0001f;
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
