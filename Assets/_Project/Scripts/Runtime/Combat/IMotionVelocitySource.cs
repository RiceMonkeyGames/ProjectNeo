using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// NavMesh 없이 스스로 움직이는 유닛(비행 적 등)이 지금 속도를 알려 주는 공통 창구(SIEGE-33).
    /// 대포 앞지르기처럼 움직임을 예측하는 쪽이 <see cref="MotionVelocity.Of"/>로 읽는다.
    /// </summary>
    public interface IMotionVelocitySource
    {
        /// <summary>지금 속도(m/s, 월드 기준). 멈춰 있으면 0.</summary>
        Vector3 Velocity { get; }
    }
}
