using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 성가퀴 빈틈(크레넬) 자리 옮기기(COMBAT-17, 사용자 결정 2026-11-23). 자리를 지키는 원거리 유닛이 사거리 안의 적을
    /// 구조물에 막혀 정해진 시간 넘게 못 쏘면, 지시받은 자리 근처의 빈 크레넬 사격 자리로 옮겨 그곳을 새 지킬 자리로 삼는다.
    /// </summary>
    public static class CrenelRepositionRules
    {
        /// <summary>이 거리 안이면 이미 그 자리에 서 있는 것으로 본다(m).</summary>
        public const float SameSpotDistance = 0.35f;
        /// <summary>다른 유닛(또는 다른 유닛의 지킬 자리)이 이 거리 안이면 그 자리는 찼다(m).</summary>
        public const float OccupiedDistance = 0.5f;

        /// <summary>막힌 시간이 기다림 시간 이상인지. 기다림이 0 이하면 옮기지 않는다.</summary>
        public static bool ShouldReposition(float blockedSeconds, float delay) => delay > 0f && blockedSeconds >= delay;

        /// <summary>
        /// 후보 자리인지. 지시받은 자리(<paramref name="anchor"/>)에서 수평 <paramref name="radius"/> 안, 지금 높이에서
        /// <paramref name="levelTolerance"/> 안이고, 지금 서 있는 자리가 아니어야 한다.
        /// </summary>
        public static bool IsCandidate(Vector3 point, Vector3 anchor, Vector3 self, float radius, float levelTolerance)
        {
            if (Mathf.Abs(point.y - self.y) > levelTolerance) return false;
            Vector3 fromAnchor = point - anchor;
            fromAnchor.y = 0f;
            if (fromAnchor.sqrMagnitude > radius * radius) return false;
            return !IsNear(point, self, SameSpotDistance);
        }

        /// <summary>두 점이 수평으로 <paramref name="distance"/> 안인지.</summary>
        public static bool IsNear(Vector3 a, Vector3 b, float distance)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.sqrMagnitude <= distance * distance;
        }
    }
}
