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

        public static FlyerSettings Create(FlyerTargetStrategy targetStrategy, FlyerMovement movement,
            FlyerEngagement engagement, float cruiseClearance = 3f, float meleeExposureAltitude = 1.5f,
            float wallTopMinimumHeight = 1f, float groundProbeDistance = 60f, float corpseFallAcceleration = 9.81f,
            float holdInset = 6f, bool targetsSiegeWeapons = true, float hoverSeparation = 1.2f,
            float roofProbeDistance = 6f)
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
            return value;
        }
    }
}
