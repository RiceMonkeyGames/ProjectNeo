using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>원거리 탄퍼짐(COMBAT-09). 씬을 알지 않는 순수 규칙이다.</summary>
    public static class AimSpreadRules
    {
        /// <summary>탄퍼짐 각도는 0 이상 45° 이하로 자른다.</summary>
        public const float MaximumDegrees = 45f;

        /// <summary>조준 거리에서의 흩어짐 반경. 거리 × tan(각도).</summary>
        public static float Radius(float distance, float spreadDegrees)
        {
            if (!(distance > 0f) || !(spreadDegrees > 0f)) return 0f;
            return distance * Mathf.Tan(Mathf.Min(spreadDegrees, MaximumDegrees) * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 쏘는 지점에서 본 조준선에 수직인 원 안의 고른 무작위 지점. 같은 난수열이면 같은 결과다.
        /// 각도가 0 이하이거나 난수가 없으면 조준점을 그대로 돌려준다.
        /// </summary>
        public static Vector3 Apply(Vector3 origin, Vector3 aim, float spreadDegrees, IRandomSource random)
        {
            Vector3 line = aim - origin;
            float radius = Radius(line.magnitude, spreadDegrees);
            if (random == null || !(radius > 0f)) return aim;
            Vector3 forward = line.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(Vector3.forward, forward);
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right);
            float distance = radius * Mathf.Sqrt(random.NextFloat());
            float angle = random.NextFloat() * Mathf.PI * 2f;
            return aim + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * distance;
        }
    }
}
