using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 유닛의 지금 속도를 한 곳에서 읽는다(SIEGE-33). 속도 창구(<see cref="IMotionVelocitySource"/>)가 있으면 그 값(3차원),
    /// 없으면 켜진 NavMeshAgent의 수평 속도, 둘 다 없으면 0이다.
    /// </summary>
    public static class MotionVelocity
    {
        public static Vector3 Of(Component unit)
        {
            if (unit == null) return Vector3.zero;
            var source = unit.GetComponent<IMotionVelocitySource>();
            if (source != null) return source.Velocity;
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled) return Vector3.zero;
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            return velocity;
        }
    }
}
