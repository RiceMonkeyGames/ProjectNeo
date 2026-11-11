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
        public float StoppingRangeRatio => stoppingRangeRatio;
        public float ArrivalDistance => arrivalDistance;

        public static UnitAISettings Create(float detectionRange = 60f)
        {
            var value = CreateInstance<UnitAISettings>();
            value.detectionRange = Mathf.Max(0f, detectionRange);
            return value;
        }
    }
}
