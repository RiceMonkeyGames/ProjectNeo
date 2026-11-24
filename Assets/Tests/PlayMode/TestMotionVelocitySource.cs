using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>시험용 속도 창구(SIEGE-33). 움직이지 않고 정한 속도만 알려 준다.</summary>
    public sealed class TestMotionVelocitySource : MonoBehaviour, IMotionVelocitySource
    {
        public Vector3 Value;
        public Vector3 Velocity => Value;
    }
}
