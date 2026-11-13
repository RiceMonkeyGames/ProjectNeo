using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 공중에 떠 있는 몸통의 상태. 비행을 맡은 부품이 매 프레임 갱신한다.
    /// 근접 공격의 사거리와 길찾기는 몸통이 아니라 바닥 투영점을 기준으로 한다(Q-43).
    /// </summary>
    public sealed class AirborneBody : MonoBehaviour
    {
        /// <summary>몸통 바로 아래 발판(땅·성벽 윗면) 위의 점.</summary>
        public Vector3 GroundPoint { get; private set; }
        /// <summary>아래에 발판을 찾았는지. 못 찾았으면 근접으로 닿지 않는다.</summary>
        public bool HasGround { get; private set; }
        /// <summary>근접 공격이 닿을 만큼 낮게 떠 있는지.</summary>
        public bool IsMeleeExposed { get; private set; }
        public float Altitude => HasGround ? transform.position.y - GroundPoint.y : float.PositiveInfinity;

        public void SetState(Vector3 groundPoint, bool hasGround, bool meleeExposed)
        {
            GroundPoint = groundPoint;
            HasGround = hasGround;
            IsMeleeExposed = hasGround && meleeExposed;
        }
    }
}
