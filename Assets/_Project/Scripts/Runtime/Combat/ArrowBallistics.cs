using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>화살 포물선 계산(COMBAT-09). 씬을 알지 않는 순수 규칙이다.</summary>
    public static class ArrowBallistics
    {
        /// <summary>
        /// 고정 발사 속도로 목표에 닿는 낮은 포물선 발사 속도. 속도가 모자라 닿지 않으면 목표 쪽으로 45°(가장 멀리 가는 각)로 쏜다.
        /// 수평 거리가 0에 가깝거나 중력이 없으면 목표를 향해 곧게 쏜다.
        /// </summary>
        public static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, float speed, float gravity)
        {
            speed = Mathf.Max(0.01f, speed);
            Vector3 offset = target - origin;
            var horizontal = new Vector3(offset.x, 0f, offset.z);
            float distance = horizontal.magnitude;
            if (distance < 0.0001f || !(gravity > 0f))
                return (offset.sqrMagnitude > 0f ? offset.normalized : Vector3.forward) * speed;
            Vector3 flat = horizontal / distance;
            float speedSquared = speed * speed;
            float root = speedSquared * speedSquared - gravity * (gravity * distance * distance + 2f * offset.y * speedSquared);
            float angle = root < 0f ? Mathf.PI * 0.25f
                : Mathf.Atan((speedSquared - Mathf.Sqrt(root)) / (gravity * distance));
            return flat * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        }

        /// <summary>발사 후 time초 뒤의 위치.</summary>
        public static Vector3 Evaluate(Vector3 origin, Vector3 velocity, float gravity, float time) =>
            origin + velocity * time + Vector3.down * (0.5f * gravity * time * time);

        /// <summary>발사 후 time초 뒤의 속도.</summary>
        public static Vector3 VelocityAt(Vector3 velocity, float gravity, float time) =>
            velocity + Vector3.down * (gravity * time);
    }
}
