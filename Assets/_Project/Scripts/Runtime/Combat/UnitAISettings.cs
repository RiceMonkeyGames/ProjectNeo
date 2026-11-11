using UnityEngine;

namespace ProjectNeo.Combat
{
    [CreateAssetMenu(menuName = "ProjectNeo/Unit AI Settings")]
    public sealed class UnitAISettings : ScriptableObject
    {
        [SerializeField, Min(0.01f)] float decisionInterval = 0.25f;
        [SerializeField, Min(0f)] float detectionRange = 60f;
        [SerializeField, Min(0f)] float turnSpeed = 540f;
        [SerializeField, Min(0.01f)] float acceleration = 24f;
        [SerializeField, Range(0f, 1f)] float stoppingRangeRatio = 0.8f;
        public float DecisionInterval => decisionInterval;
        public float DetectionRange => detectionRange;
        public float TurnSpeed => turnSpeed;
        public float Acceleration => acceleration;
        // 이동 지시에서 이 거리 안으로 들어오면 도착으로 본다.
        [SerializeField, Min(0.01f)] float arrivalDistance = 0.35f;
        // 목적지 조각이 사라졌을 때 주변의 남은 길을 찾는 거리. 조각 규격에 맞춰 조정한다.
        [SerializeField, Min(0.1f)] float destinationRecoveryDistance = 4f;
        // 중지(자리 지키기) 중인 근접 유닛이 다가가 싸우는 범위. 자리에서 이 거리 안의 적만 쫓는다. 0이면 쫓지 않는다.
        [SerializeField, Min(0f)] float holdEngageRadius = 6f;
        public float HoldEngageRadius => holdEngageRadius;
        // 중지 중인 원거리 유닛이 쫓아가는 범위. 자리에서 (공격 사거리 + 이 거리) 안의 적에게 사거리가 닿을 때까지 다가간다.
        // 0이면 원거리는 자리를 떠나지 않는다.
        [SerializeField, Min(0f)] float holdRangedChaseExtra = 6f;
        public float HoldRangedChaseExtra => holdRangedChaseExtra;
        public float StoppingRangeRatio => stoppingRangeRatio;
        public float ArrivalDistance => arrivalDistance;
        public float DestinationRecoveryDistance => destinationRecoveryDistance;

        public static UnitAISettings Create(float detectionRange = 60f, float holdEngageRadius = 6f,
            float holdRangedChaseExtra = 6f)
        {
            var value = CreateInstance<UnitAISettings>();
            value.detectionRange = Mathf.Max(0f, detectionRange);
            value.holdEngageRadius = Mathf.Max(0f, holdEngageRadius);
            value.holdRangedChaseExtra = Mathf.Max(0f, holdRangedChaseExtra);
            return value;
        }
    }
}
