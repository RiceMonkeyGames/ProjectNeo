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
        // 이동이 막혔을 때의 도착 판정. 목적지까지 이 거리 안에서 이 시간 동안 이만큼도 더 가까워지지 못하면 도착으로 본다.
        // 길이 끊긴 이동에서 여러 유닛의 끝점이 겹쳐 서로 막을 때 이동 지시가 끝나지 않는 것을 막는다.
        [SerializeField, Min(0.01f)] float moveStallDistance = 1.5f;
        [SerializeField, Min(0.01f)] float moveStallSeconds = 0.5f;
        [SerializeField, Min(0f)] float moveStallProgress = 0.1f;
        // 중지 중 자리에서 이 거리보다 멀리 밀려났을 때만 자리로 돌아간다. 살짝 밀린 정도로 자리 다툼을 하지 않게 한다.
        [SerializeField, Min(0.01f)] float holdReturnDistance = 1f;
        // 중지 중 사거리/추격 반경 밖에서 피격 시 반격 추격할 최대 경로/거리 한계. 0이면 자리 밖 반격 추격을 하지 않는다.
        [SerializeField, Min(0f)] float holdRetaliationRadius = 15f;
        public float HoldRetaliationRadius => holdRetaliationRadius;
        // 중지 중에는 자리보다 이 높이 이상 아래로 내려가는 길로 적을 쫓거나 반격하지 않는다(성벽 위 부대가 성벽에 남는다).
        // 0이면 제한하지 않는다.
        [SerializeField, Min(0f)] float holdMaxDescent = 1.5f;
        public float HoldMaxDescent => holdMaxDescent;
        // 타겟 전환 후 최소 유지 시간. 여러 적에게 동시에 맞을 때 핑퐁 현상을 막는다.
        [SerializeField, Min(0f)] float targetSwitchCooldown = 1.5f;
        public float TargetSwitchCooldown => targetSwitchCooldown;
        public float MoveStallDistance => moveStallDistance;
        public float MoveStallSeconds => moveStallSeconds;
        public float MoveStallProgress => moveStallProgress;
        public float HoldReturnDistance => Mathf.Max(holdReturnDistance, arrivalDistance);
        public float StoppingRangeRatio => stoppingRangeRatio;
        public float ArrivalDistance => arrivalDistance;
        public float DestinationRecoveryDistance => destinationRecoveryDistance;

        public static UnitAISettings Create(float detectionRange = 60f, float holdEngageRadius = 6f,
            float holdRangedChaseExtra = 6f, float holdRetaliationRadius = 15f, float targetSwitchCooldown = 1.5f,
            float holdMaxDescent = 1.5f)
        {
            var value = CreateInstance<UnitAISettings>();
            value.detectionRange = Mathf.Max(0f, detectionRange);
            value.holdEngageRadius = Mathf.Max(0f, holdEngageRadius);
            value.holdRangedChaseExtra = Mathf.Max(0f, holdRangedChaseExtra);
            value.holdRetaliationRadius = Mathf.Max(0f, holdRetaliationRadius);
            value.targetSwitchCooldown = Mathf.Max(0f, targetSwitchCooldown);
            value.holdMaxDescent = Mathf.Max(0f, holdMaxDescent);
            return value;
        }
    }
}
