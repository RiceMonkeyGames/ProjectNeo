using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>고정 발사 속도로 목표 지점에 떨어지는 포물선 계산. 씬을 알지 않는다.</summary>
    public static class CannonBallistics
    {
        /// <summary>
        /// 목표에 닿는 낮은 포물선 발사 속도를 구한다. 속도가 모자라 닿을 수 없거나 수평 거리가 0이면 false.
        /// </summary>
        public static bool TrySolve(Vector3 origin, Vector3 target, float speed, float gravity,
            out Vector3 velocity, out float flightTime)
        {
            velocity = Vector3.zero;
            flightTime = 0f;
            if (!(speed > 0f) || !(gravity > 0f)) return false;
            Vector3 offset = target - origin;
            var horizontal = new Vector3(offset.x, 0f, offset.z);
            float distance = horizontal.magnitude;
            if (distance < 0.0001f) return false;
            float height = offset.y;
            float speedSquared = speed * speed;
            float root = speedSquared * speedSquared - gravity * (gravity * distance * distance + 2f * height * speedSquared);
            if (root < 0f) return false;
            float angle = Mathf.Atan((speedSquared - Mathf.Sqrt(root)) / (gravity * distance));
            float horizontalSpeed = speed * Mathf.Cos(angle);
            velocity = horizontal / distance * horizontalSpeed + Vector3.up * (speed * Mathf.Sin(angle));
            flightTime = distance / horizontalSpeed;
            return true;
        }

        /// <summary>발사 후 time초 뒤의 위치.</summary>
        public static Vector3 Evaluate(Vector3 origin, Vector3 velocity, float gravity, float time) =>
            origin + velocity * time + Vector3.down * (0.5f * gravity * time * time);
    }
}
