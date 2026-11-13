using UnityEngine;

namespace ProjectNeo.Siege
{
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Wall Breaker")]
    public sealed class WallBreakerSettings : ScriptableObject
    {
        [SerializeField] WallTargetStrategy targetStrategy;
        [SerializeField, Min(0f)] float approachClearance = 0.1f;
        [SerializeField, Min(0.01f)] float navigationSnapDistance = 1f;
        [SerializeField, Min(0f)] float verticalSnapTolerance = 0.5f;
        public WallTargetStrategy TargetStrategy => targetStrategy;
        public float ApproachClearance => approachClearance;
        public float NavigationSnapDistance => navigationSnapDistance;
        public float VerticalSnapTolerance => verticalSnapTolerance;

        public static WallBreakerSettings Create(WallTargetStrategy strategy, float approachClearance = 0.1f,
            float navigationSnapDistance = 1f, float verticalSnapTolerance = 0.5f)
        {
            var value = CreateInstance<WallBreakerSettings>();
            value.targetStrategy = strategy;
            value.approachClearance = Mathf.Max(0f, approachClearance);
            value.navigationSnapDistance = Mathf.Max(0.01f, navigationSnapDistance);
            value.verticalSnapTolerance = Mathf.Max(0f, verticalSnapTolerance);
            return value;
        }
    }
}
