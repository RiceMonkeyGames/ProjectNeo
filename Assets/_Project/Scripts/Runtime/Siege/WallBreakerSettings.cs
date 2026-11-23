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
        // 성벽 면을 따라 나눠 서는 자리 사이 최소 간격(몸 지름보다 좁으면 몸 지름을 쓴다)과,
        // 다른 파괴병이 잡은 자리 하나를 몇 미터로 칠지, 대상 하나에서 길찾기를 시험할 최대 자리 수(SIEGE-17).
        [SerializeField, Min(0.1f)] float slotSpacing = 1.2f;
        [SerializeField, Min(0f)] float slotClaimPenalty = 5f;
        [SerializeField, Range(1, 32)] int maxSlotPathChecks = 8;
        public WallTargetStrategy TargetStrategy => targetStrategy;
        public float ApproachClearance => approachClearance;
        public float NavigationSnapDistance => navigationSnapDistance;
        public float VerticalSnapTolerance => verticalSnapTolerance;
        public float SlotSpacing => slotSpacing;
        public float SlotClaimPenalty => slotClaimPenalty;
        public int MaxSlotPathChecks => maxSlotPathChecks;

        public static WallBreakerSettings Create(WallTargetStrategy strategy, float approachClearance = 0.1f,
            float navigationSnapDistance = 1f, float verticalSnapTolerance = 0.5f,
            float slotSpacing = 1.2f, float slotClaimPenalty = 5f, int maxSlotPathChecks = 8)
        {
            var value = CreateInstance<WallBreakerSettings>();
            value.targetStrategy = strategy;
            value.approachClearance = Mathf.Max(0f, approachClearance);
            value.navigationSnapDistance = Mathf.Max(0.01f, navigationSnapDistance);
            value.verticalSnapTolerance = Mathf.Max(0f, verticalSnapTolerance);
            value.slotSpacing = Mathf.Max(0.1f, slotSpacing);
            value.slotClaimPenalty = Mathf.Max(0f, slotClaimPenalty);
            value.maxSlotPathChecks = Mathf.Clamp(maxSlotPathChecks, 1, 32);
            return value;
        }
    }
}
