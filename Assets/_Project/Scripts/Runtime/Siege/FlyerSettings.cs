using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 비행 적 한 종류의 조합. 목표 선택·이동·공격 자리 부품과 수치를 묶는다.
    /// 새 비행 적은 이 자산의 부품과 값을 바꿔 만든다(Q-43 확장성).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Flyer")]
    public sealed class FlyerSettings : ScriptableObject
    {
        [SerializeField] FlyerTargetStrategy targetStrategy;
        [SerializeField] FlyerMovement movement;
        [SerializeField] FlyerEngagement engagement;
        // 성에서 가장 높은 조각 꼭대기보다 이만큼 위로 날아 성벽을 넘는다.
        [SerializeField, Min(0f)] float cruiseClearance = 3f;
        // 발판에서 이 높이 이하로 떠 있으면 아군 근접 공격이 닿는다.
        [SerializeField, Min(0f)] float meleeExposureAltitude = 1.5f;
        // 땅보다 이만큼 이상 높이 선 아군을 성벽 위 아군으로 본다(대포 공격과 같은 기준).
        [SerializeField, Min(0f)] float wallTopMinimumHeight = 1f;
        [SerializeField, Min(0.1f)] float groundProbeDistance = 60f;
        // 노릴 아군이 없을 때 성 조각들의 가운데에서 성 안쪽(바깥 반대)으로 이만큼 들어간 곳 위에서 기다린다.
        // 성벽이 한 줄뿐인 시험 성에서도 가운데가 성벽 위가 아니라 안마당 위가 되게 한다.
        [SerializeField, Min(0f)] float holdInset = 6f;
        // 죽으면 이 가속도로 떨어진다. 외형 처리일 뿐 낙하 피해는 없다.
        [SerializeField, Min(0.1f)] float corpseFallAcceleration = 9.81f;
        // 켜면 성벽 위 아군이 없을 때 설치된 수성 무기(대포)를 노릴 후보에 넣는다(SIEGE-16). 순서는 대상 전략이 정한다.
        [SerializeField] bool targetsSiegeWeapons = true;
        // 같은 대상을 쫓는 비행 적끼리 공격 자리를 이만큼 이상 떨어뜨린다(SIEGE-16). 몸통 지름보다 작으면 지름을 쓴다.
        [SerializeField, Min(0f)] float hoverSeparation = 1.2f;
        // 머리 위 이 거리 안에 성 조각(지붕·천장)이 있는 아군은 날아서 닿지 않으므로 노리지 않는다(SIEGE-16).
        // 성벽 안 계단방·탑 안·성문 통로 안의 아군이 여기에 든다. 0이면 따지지 않는다.
        [SerializeField, Min(0f)] float roofProbeDistance = 6f;
        // 비용 줄이기(PERF-ai-alloc-and-split). 같이 생긴 비행 적들의 판단 시점을 이만큼의 묶음으로 나눈다. 1이면 나누지 않는다.
        [SerializeField, Min(1)] int decisionStaggerBuckets = 4;
        // 발밑 높이를 다시 재는 최소 간격(초)과, 그 사이라도 다시 재는 수평 이동 거리(m).
        [SerializeField, Min(0f)] float groundProbeInterval = 0.1f;
        [SerializeField, Min(0.01f)] float groundProbeMoveTolerance = 0.25f;
        // 대상 후보의 지붕 검사 결과를 다시 쓰는 시간(초)과, 그 사이 대상이 이만큼 움직이면 다시 잰다(m).
        [SerializeField, Min(0f)] float shelterCacheSeconds = 0.5f;
        [SerializeField, Min(0.01f)] float shelterMoveTolerance = 0.25f;
        // 카메라에서 먼 비행 적은 판단과 발밑 검사를 덜 자주 한다(지상 유닛과 같은 AI LOD 규칙).
        [SerializeField] bool enableAiLod = true;
        [SerializeField, Min(1f)] float lod0Distance = 25f;
        [SerializeField, Min(2f)] float lod1Distance = 60f;
        [SerializeField, Min(0.05f)] float lodEvaluationInterval = 0.5f;
        // 대상을 고른 뒤 이만큼(초)은 바꾸지 않는다(SIEGE-32). 대상이 죽거나 지붕 아래로 숨으면 바로 바꾼다.
        [SerializeField, Min(0f)] float targetLockSeconds = 1.5f;
        // 잠금이 끝난 뒤 같은 순위의 다른 대상이 이만큼(m) 넘게 더 가까워야 바꾼다(SIEGE-32).
        [SerializeField, Min(0f)] float retargetDistanceMargin = 2f;
        // 쫓는 중 사선(성가퀴·기둥 가림)을 다시 보는 간격(초). 가려졌으면 공격 자리를 다시 구한다(SIEGE-32).
        [SerializeField, Min(0.05f)] float sightlineRecheckInterval = 0.25f;
        public FlyerTargetStrategy TargetStrategy => targetStrategy;
        public FlyerMovement Movement => movement;
        public FlyerEngagement Engagement => engagement;
        public float CruiseClearance => cruiseClearance;
        public float MeleeExposureAltitude => meleeExposureAltitude;
        public float WallTopMinimumHeight => wallTopMinimumHeight;
        public float GroundProbeDistance => groundProbeDistance;
        public float HoldInset => holdInset;
        public float CorpseFallAcceleration => corpseFallAcceleration;
        public bool TargetsSiegeWeapons => targetsSiegeWeapons;
        public float HoverSeparation => hoverSeparation;
        public float RoofProbeDistance => roofProbeDistance;
        public int DecisionStaggerBuckets => Mathf.Max(1, decisionStaggerBuckets);
        public float GroundProbeInterval => Mathf.Max(0f, groundProbeInterval);
        public float GroundProbeMoveTolerance => Mathf.Max(0.01f, groundProbeMoveTolerance);
        public float ShelterCacheSeconds => Mathf.Max(0f, shelterCacheSeconds);
        public float ShelterMoveTolerance => Mathf.Max(0.01f, shelterMoveTolerance);
        public bool EnableAiLod => enableAiLod;
        public float Lod0Distance => Mathf.Max(1f, lod0Distance);
        public float Lod1Distance => Mathf.Max(Lod0Distance, lod1Distance);
        public float LodEvaluationInterval => Mathf.Max(0.05f, lodEvaluationInterval);
        public float TargetLockSeconds => Mathf.Max(0f, targetLockSeconds);
        public float RetargetDistanceMargin => Mathf.Max(0f, retargetDistanceMargin);
        public float SightlineRecheckInterval => Mathf.Max(0.05f, sightlineRecheckInterval);

        public static FlyerSettings Create(FlyerTargetStrategy targetStrategy, FlyerMovement movement,
            FlyerEngagement engagement, float cruiseClearance = 3f, float meleeExposureAltitude = 1.5f,
            float wallTopMinimumHeight = 1f, float groundProbeDistance = 60f, float corpseFallAcceleration = 9.81f,
            float holdInset = 6f, bool targetsSiegeWeapons = true, float hoverSeparation = 1.2f,
            float roofProbeDistance = 6f, int decisionStaggerBuckets = 4, float groundProbeInterval = 0.1f,
            float groundProbeMoveTolerance = 0.25f, float shelterCacheSeconds = 0.5f, float shelterMoveTolerance = 0.25f,
            bool enableAiLod = true, float lod0Distance = 25f, float lod1Distance = 60f, float lodEvaluationInterval = 0.5f,
            float targetLockSeconds = 1.5f, float retargetDistanceMargin = 2f, float sightlineRecheckInterval = 0.25f)
        {
            var value = CreateInstance<FlyerSettings>();
            value.targetStrategy = targetStrategy;
            value.movement = movement;
            value.engagement = engagement;
            value.cruiseClearance = Mathf.Max(0f, cruiseClearance);
            value.meleeExposureAltitude = Mathf.Max(0f, meleeExposureAltitude);
            value.wallTopMinimumHeight = Mathf.Max(0f, wallTopMinimumHeight);
            value.groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);
            value.corpseFallAcceleration = Mathf.Max(0.1f, corpseFallAcceleration);
            value.holdInset = Mathf.Max(0f, holdInset);
            value.targetsSiegeWeapons = targetsSiegeWeapons;
            value.hoverSeparation = Mathf.Max(0f, hoverSeparation);
            value.roofProbeDistance = Mathf.Max(0f, roofProbeDistance);
            value.decisionStaggerBuckets = Mathf.Max(1, decisionStaggerBuckets);
            value.groundProbeInterval = Mathf.Max(0f, groundProbeInterval);
            value.groundProbeMoveTolerance = Mathf.Max(0.01f, groundProbeMoveTolerance);
            value.shelterCacheSeconds = Mathf.Max(0f, shelterCacheSeconds);
            value.shelterMoveTolerance = Mathf.Max(0.01f, shelterMoveTolerance);
            value.enableAiLod = enableAiLod;
            value.lod0Distance = Mathf.Max(1f, lod0Distance);
            value.lod1Distance = Mathf.Max(value.lod0Distance, lod1Distance);
            value.lodEvaluationInterval = Mathf.Max(0.05f, lodEvaluationInterval);
            value.targetLockSeconds = Mathf.Max(0f, targetLockSeconds);
            value.retargetDistanceMargin = Mathf.Max(0f, retargetDistanceMargin);
            value.sightlineRecheckInterval = Mathf.Max(0.05f, sightlineRecheckInterval);
            return value;
        }
    }
}
