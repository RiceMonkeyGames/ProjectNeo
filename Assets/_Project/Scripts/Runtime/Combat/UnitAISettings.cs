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
        // 근접 접근 자리(COMBAT-12). 끄면 예전처럼 대상 중심으로 간다. 임시 값이며 플레이 후 조정한다.
        [SerializeField] bool meleeSlots = true;
        // 공격자와 대상 몸 사이의 틈(m).
        [SerializeField, Min(0f)] float meleeSlotGap = 0.15f;
        // 링 위 자리 간격(m, 호 길이). 몸 지름(0.8m)보다 조금 넓게.
        [SerializeField, Min(0.1f)] float meleeSlotSpacing = 0.9f;
        // 바깥(대기) 링이 안쪽 링에서 더 나간 거리(m).
        [SerializeField, Min(0f)] float meleeOuterRingOffset = 1f;
        // 자리를 길찾기 메시에 맞출 때 찾는 거리와, 대상과 허용하는 높이 차(m). 다른 층의 자리는 쓰지 않는다.
        [SerializeField, Min(0.05f)] float meleeSlotSampleDistance = 0.5f;
        [SerializeField, Min(0.05f)] float meleeSlotLevelTolerance = 1f;
        // 원거리 위협 우선순위(COMBAT-14). 위협 종류별 거리 배율(1 이하): 0.5면 그 대상을 절반 거리에 있는 것처럼 본다.
        // 임시 값이며 플레이 후 조정한다. 끄면 예전처럼 거리만으로 고른다.
        [SerializeField] bool rangedThreatPriority = true;
        [SerializeField, Range(0.05f, 1f)] float ladderClimberDistanceFactor = 0.5f;
        [SerializeField, Range(0.05f, 1f)] float siegeBreakerDistanceFactor = 0.6f;
        [SerializeField, Range(0.05f, 1f)] float rangedThreatDistanceFactor = 0.75f;
        // 근접 위협 대응(COMBAT-14). 원거리 유닛은 이 반경 안의 근접 적을 바로 대상으로 삼는다. 0이면 대응하지 않는다.
        [SerializeField, Min(0f)] float rangedMeleeThreatRadius = 3f;
        // 짧은 후퇴 거리(m). 0이면 물러나지 않고 대상만 바꾼다.
        [SerializeField, Min(0f)] float rangedFallbackDistance = 3f;
        // 후퇴를 마친 뒤 다시 물러나기까지 기다리는 시간(초). 이 사이에 쏜다.
        [SerializeField, Min(0f)] float rangedFallbackCooldown = 3f;
        // 한 번 물러나는 최대 시간(초). 막혀서 못 가도 이 시간이 지나면 다시 쏜다.
        [SerializeField, Min(0.1f)] float rangedFallbackMaxSeconds = 1.5f;
        // 자리를 지키는 중 후퇴 지점이 자리에서 수평으로 이 거리 안이어야 한다.
        [SerializeField, Min(0f)] float rangedFallbackHoldLeash = 4f;
        // 자리 방향 복귀(COMBAT-14). 끄면 자리에 돌아와도 마지막으로 보던 방향을 그대로 본다.
        [SerializeField] bool holdFacingRestore = true;
        // 대규모 전투 탐색 최적화(PERF-ai-query-budget).
        // 판단 주기마다 점수 순으로 먼저 추린 뒤 실제로 NavMesh.CalculatePath를 시험할 최대 후보 수.
        [SerializeField, Min(1)] int maxPathCandidatesPerTick = 4;
        // 한 프레임에 전체 AI가 수행할 수 있는 최대 길찾기(CalculatePath) 횟수. 0이면 제한하지 않는다.
        [SerializeField, Min(0)] int maxPathQueriesPerFrame = 0;
        // 동시에 켜지거나 부대 지시를 받을 때 판단 주기를 나눌 버킷 수. 1 이하면 나누지 않는다.
        [SerializeField, Min(1)] int decisionStaggerBuckets = 8;
        // 2D 공간 해시 그리드 칸 크기(m, PERF-ai-spatial-grid).
        [SerializeField, Min(1f)] float spatialCellSize = 8f;
        // 대규모 전투 3단계 AI LOD(PERF-ai-lod).
        [SerializeField] bool enableAiLod = true;
        [SerializeField, Min(1f)] float lod0Distance = 25f;
        [SerializeField, Min(2f)] float lod1Distance = 60f;
        [SerializeField, Min(0.05f)] float lodEvaluationInterval = 0.5f;
        // 원거리(LOD 2) 하이브리드 Flow Field 길찾기(PERF-ai-flow-field).
        [SerializeField] bool enableFlowField = true;
        // 대상까지 거리가 이 값(m) 이상이고 LOD 2인 지상 유닛은 개별 CalculatePath 대신 공유 Flow Field를 따라 나아간다.
        [SerializeField, Min(4f)] float flowFieldMinDistance = 18f;
        // Flow Field 한 걸음 앞으로 내딛는 웨이포인트 거리(m).
        [SerializeField, Min(2f)] float flowFieldStepDistance = 8f;
        public bool RangedThreatPriority => rangedThreatPriority;
        public float RangedMeleeThreatRadius => rangedMeleeThreatRadius;
        public float RangedFallbackDistance => rangedFallbackDistance;
        public float RangedFallbackCooldown => rangedFallbackCooldown;
        public float RangedFallbackMaxSeconds => rangedFallbackMaxSeconds;
        public float RangedFallbackHoldLeash => rangedFallbackHoldLeash;
        public bool HoldFacingRestore => holdFacingRestore;
        public int MaxPathCandidatesPerTick => Mathf.Max(1, maxPathCandidatesPerTick);
        public int MaxPathQueriesPerFrame => Mathf.Max(0, maxPathQueriesPerFrame);
        public int DecisionStaggerBuckets => Mathf.Max(1, decisionStaggerBuckets);
        public float SpatialCellSize => Mathf.Max(1f, spatialCellSize);
        public bool EnableAiLod => enableAiLod;
        public float Lod0Distance => Mathf.Max(1f, lod0Distance);
        public float Lod1Distance => Mathf.Max(Lod0Distance, lod1Distance);
        public float LodEvaluationInterval => Mathf.Max(0.05f, lodEvaluationInterval);
        public bool EnableFlowField => enableFlowField;
        public float FlowFieldMinDistance => Mathf.Max(4f, flowFieldMinDistance);
        public float FlowFieldStepDistance => Mathf.Max(2f, flowFieldStepDistance);

        /// <summary>위협 종류의 거리 배율. 우선순위를 끄면 언제나 1이다.</summary>
        public float ThreatDistanceFactor(TargetThreat threat) => rangedThreatPriority
            ? RangedTargetPriorityRules.DistanceFactor(threat, ladderClimberDistanceFactor, siegeBreakerDistanceFactor,
                rangedThreatDistanceFactor)
            : 1f;

        public bool MeleeSlots => meleeSlots;
        public float MeleeSlotGap => meleeSlotGap;
        public float MeleeSlotSpacing => meleeSlotSpacing;
        public float MeleeOuterRingOffset => meleeOuterRingOffset;
        public float MeleeSlotSampleDistance => meleeSlotSampleDistance;
        public float MeleeSlotLevelTolerance => meleeSlotLevelTolerance;
        public float MoveStallDistance => moveStallDistance;
        public float MoveStallSeconds => moveStallSeconds;
        public float MoveStallProgress => moveStallProgress;
        public float HoldReturnDistance => Mathf.Max(holdReturnDistance, arrivalDistance);
        public float StoppingRangeRatio => stoppingRangeRatio;
        public float ArrivalDistance => arrivalDistance;
        public float DestinationRecoveryDistance => destinationRecoveryDistance;

        public static UnitAISettings Create(float detectionRange = 60f, float holdEngageRadius = 6f,
            float holdRangedChaseExtra = 6f, float holdRetaliationRadius = 15f, float targetSwitchCooldown = 1.5f,
            float holdMaxDescent = 1.5f, bool meleeSlots = true, bool rangedThreatPriority = true,
            float rangedMeleeThreatRadius = 3f, float rangedFallbackDistance = 3f, bool holdFacingRestore = true,
            float ladderClimberDistanceFactor = 0.5f, float siegeBreakerDistanceFactor = 0.6f,
            float rangedThreatDistanceFactor = 0.75f, int maxPathCandidatesPerTick = 4,
            int maxPathQueriesPerFrame = 0, int decisionStaggerBuckets = 8, float spatialCellSize = 8f,
            bool enableAiLod = true, float lod0Distance = 25f, float lod1Distance = 60f,
            float lodEvaluationInterval = 0.5f, bool enableFlowField = true, float flowFieldMinDistance = 18f,
            float flowFieldStepDistance = 8f)
        {
            var value = CreateInstance<UnitAISettings>();
            value.meleeSlots = meleeSlots;
            value.rangedThreatPriority = rangedThreatPriority;
            value.rangedMeleeThreatRadius = Mathf.Max(0f, rangedMeleeThreatRadius);
            value.rangedFallbackDistance = Mathf.Max(0f, rangedFallbackDistance);
            value.holdFacingRestore = holdFacingRestore;
            value.ladderClimberDistanceFactor = Mathf.Clamp(ladderClimberDistanceFactor, RangedTargetPriorityRules.MinimumFactor, 1f);
            value.siegeBreakerDistanceFactor = Mathf.Clamp(siegeBreakerDistanceFactor, RangedTargetPriorityRules.MinimumFactor, 1f);
            value.rangedThreatDistanceFactor = Mathf.Clamp(rangedThreatDistanceFactor, RangedTargetPriorityRules.MinimumFactor, 1f);
            value.detectionRange = Mathf.Max(0f, detectionRange);
            value.holdEngageRadius = Mathf.Max(0f, holdEngageRadius);
            value.holdRangedChaseExtra = Mathf.Max(0f, holdRangedChaseExtra);
            value.holdRetaliationRadius = Mathf.Max(0f, holdRetaliationRadius);
            value.targetSwitchCooldown = Mathf.Max(0f, targetSwitchCooldown);
            value.holdMaxDescent = Mathf.Max(0f, holdMaxDescent);
            value.maxPathCandidatesPerTick = Mathf.Max(1, maxPathCandidatesPerTick);
            value.maxPathQueriesPerFrame = Mathf.Max(0, maxPathQueriesPerFrame);
            value.decisionStaggerBuckets = Mathf.Max(1, decisionStaggerBuckets);
            value.spatialCellSize = Mathf.Max(1f, spatialCellSize);
            value.enableAiLod = enableAiLod;
            value.lod0Distance = Mathf.Max(1f, lod0Distance);
            value.lod1Distance = Mathf.Max(value.lod0Distance, lod1Distance);
            value.lodEvaluationInterval = Mathf.Max(0.05f, lodEvaluationInterval);
            value.enableFlowField = enableFlowField;
            value.flowFieldMinDistance = Mathf.Max(4f, flowFieldMinDistance);
            value.flowFieldStepDistance = Mathf.Max(2f, flowFieldStepDistance);
            return value;
        }
    }
}
