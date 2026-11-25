using UnityEngine;

namespace ProjectNeo.Town
{
    /// <summary>성 안 주민(시설 NPC·시민)의 움직임 값(SIEGE-36). 임시 값이다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Town Settings")]
    public sealed class TownSettings : ScriptableObject
    {
        [Tooltip("산책 걸음 속도(m/s).")]
        [SerializeField, Min(0.1f)] float walkSpeed = 1.4f;
        [Tooltip("전투가 시작되어 건물로 달아날 때의 속도(m/s).")]
        [SerializeField, Min(0.1f)] float runSpeed = 4.5f;
        [Tooltip("산책 지점에서 쉬는 가장 짧은·긴 시간(초).")]
        [SerializeField, Min(0f)] float idleMin = 2f;
        [SerializeField, Min(0f)] float idleMax = 7f;
        [Tooltip("목적지에 닿았다고 보는 거리(m).")]
        [SerializeField, Min(0.1f)] float arriveDistance = 0.8f;
        [Tooltip("달아나다 이 시간(초)이 지나면 닿지 못해도 숨는다.")]
        [SerializeField, Min(1f)] float hideTimeout = 25f;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float IdleMin => idleMin;
        public float IdleMax => idleMax;
        public float ArriveDistance => arriveDistance;
        public float HideTimeout => hideTimeout;

        public static TownSettings Create(float walk = 1.4f, float run = 4.5f, float minIdle = 2f, float maxIdle = 7f,
            float arrive = 0.8f, float timeout = 25f)
        {
            var settings = CreateInstance<TownSettings>();
            settings.walkSpeed = walk; settings.runSpeed = run; settings.idleMin = minIdle; settings.idleMax = maxIdle;
            settings.arriveDistance = arrive; settings.hideTimeout = timeout;
            return settings;
        }
    }
}
