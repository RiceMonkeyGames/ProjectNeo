using UnityEngine;

namespace ProjectNeo.Characters
{
    /// <summary>플레이어 입력이 사용하는 시점 방향. 카메라의 위치 방식은 각 시점이 맡는다.</summary>
    public abstract class PlayerViewCamera : MonoBehaviour
    {
        public abstract float Yaw { get; }
        public abstract void Look(Vector2 delta);
    }
}
