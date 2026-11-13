using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 비행 적의 이동 방식. 지금 위치에서 한 프레임 뒤 위치를 돌려준다. 길찾기(NavMesh)를 쓰지 않는다.
    /// 다른 비행 방식(급강하, 선회 등)은 이 형식을 새로 구현해 데이터로 고른다.
    /// </summary>
    public abstract class FlyerMovement : ScriptableObject
    {
        /// <param name="cruiseHeight">성벽을 넘을 때의 월드 높이.</param>
        /// <param name="speed">수평 이동 속도(m/s).</param>
        public abstract Vector3 Step(Vector3 position, Vector3 destination, float cruiseHeight, float speed, float deltaTime);
    }
}
