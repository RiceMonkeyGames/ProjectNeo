using System;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    [Serializable]
    public struct PieceCost
    {
        public BuildPieceDefinition piece;
        public int cost;
    }

    [Serializable]
    public struct WeaponCost
    {
        public SiegeWeaponDefinition weapon;
        public int cost;
    }

    /// <summary>같은 유닛 여러 명. 아군은 한 줄이 부대 하나, 적은 한 줄이 한 무리다.</summary>
    [Serializable]
    public struct SiegeUnitGroup
    {
        public GameObject unitPrefab;
        [Min(0)] public int count;
    }

    /// <summary>수성전 프로토타입의 수치와 키. 임시 값이며 이 에셋의 인스펙터에서 바꾼다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege Settings")]
    public sealed class SiegeSettings : ScriptableObject
    {
        [Header("편성 (Play를 시작할 때 이 숫자대로 만든다)")]
        // 한 줄이 아군 부대 하나다. 부대 수와 부대당 인원은 SquadConfig의 상한(현재 4개 × 10명)까지만 쓴다.
        [SerializeField] SiegeUnitGroup[] allySquads = new SiegeUnitGroup[0];
        // 한 줄이 적 한 무리다. 프리팹의 SiegeAttacker와 데이터/모듈 조합이 행동을 정한다.
        [SerializeField] SiegeUnitGroup[] enemyGroups = new SiegeUnitGroup[0];
        public System.Collections.Generic.IReadOnlyList<SiegeUnitGroup> AllySquads => allySquads;
        public System.Collections.Generic.IReadOnlyList<SiegeUnitGroup> EnemyGroups => enemyGroups;

        public void ConfigureRoster(SiegeUnitGroup[] allies, SiegeUnitGroup[] enemies)
        {
            allySquads = allies ?? new SiegeUnitGroup[0];
            enemyGroups = enemies ?? new SiegeUnitGroup[0];
        }

        [Header("임시 건설 코스트")]
        [SerializeField, Min(0)] int startingBudget = 40;
        [SerializeField] PieceCost[] pieceCosts = new PieceCost[0];
        [SerializeField] WeaponCost[] weaponCosts = new WeaponCost[0];

        [Header("사다리")]
        [SerializeField, Min(0.01f)] float ladderRaiseSeconds = 3f;
        // 눕는 전체 회전 속도. 균형점을 넘은 뒤 자동으로 넘어가는 속도와 밀기 취소 시 복귀 속도다.
        [SerializeField, Min(0.01f)] float ladderPushSeconds = 2f;
        // 혼자, 탑승자 없는 사다리를 균형점까지 밀며 버텨야 하는 시간(SIEGE-05).
        [SerializeField, Min(0.01f)] float ladderPushHoldSeconds = 3f;
        // 탑승자가 있을 때 균형점까지의 시간 배율.
        [SerializeField, Min(1f)] float ladderOccupiedPushMultiplier = 1.5f;
        // 함께 미는 아군 2명, 3명 이상일 때 밀기 속도 보너스(0.5 = 50% 빠름). 3명 이상은 상한이다.
        [SerializeField, Min(0f)] float ladderTwoPusherBonus = 0.5f;
        [SerializeField, Min(0f)] float ladderThreePlusPusherBonus = 0.75f;
        [SerializeField, Min(0.01f)] float ladderClimbSeconds = 2.5f;
        [SerializeField, Min(0.05f)] float ladderTapSeconds = 0.25f;
        // 사다리 밑에서 세우기·오르기를 시작할 수 있는 거리.
        [SerializeField, Min(0.1f)] float ladderUseRadius = 3.5f;
        // 성벽 위에서 사다리를 밀 수 있는 거리와, 올라온 적이 밀기를 막는 거리.
        [SerializeField, Min(0.1f)] float ladderPushRadius = 2.5f;
        [SerializeField, Min(0.1f)] float ladderBlockRadius = 3f;
        // 성벽 위에 있다고 보는 높이 차이.
        [SerializeField, Min(0.1f)] float ladderHeightTolerance = 1f;
        // 성벽 바깥면에서 사다리 밑까지, 성벽 가장자리에서 올라선 자리까지의 거리.
        [SerializeField, Min(0.1f)] float ladderBaseOffset = 1.2f;
        [SerializeField, Min(0.1f)] float ladderTopInset = 1f;
        // 사다리를 고를 때 이미 몰린 인원 한 명을 몇 미터로 칠지.
        [SerializeField, Min(0f)] float ladderCrowdPenalty = 4f;
        // 사다리에서 떨어질 때 레일 외형과 겹치지 않도록 확보하는 최소 바깥 간격과 수평 낙하 속도.
        [SerializeField, Min(0f)] float ladderFallClearance = 0.55f;
        [SerializeField, Min(0f)] float ladderFallOutwardSpeed = 3.5f;
        [SerializeField, Min(0f)] float ladderPushPoise = 10f;
        [SerializeField, Min(0f)] float ladderPushInterruptCooldown = 0.3f;

        [Header("적 판단")]
        [SerializeField, Min(0.05f)] float attackerDecisionInterval = 0.5f;
        // 성문 바깥면에서 서는 자리까지의 거리. 음수면 문기둥 사이 안쪽으로 들어가 문에 닿는다.
        [SerializeField] float gateStandOffset = -0.3f;
        [SerializeField, Min(0f)] float gateSpread = 3f;
        [SerializeField, Range(0.1f, 1f)] float archerApproachRatio = 0.7f;
        // 사거리 안에서 가려진 채 이 시간이 지나면 정면 자리에서 성벽을 따라 옆으로 비켜선 자리를 한 번 찾는다(SIEGE-10 성가퀴 막힘 방지).
        // 찾은 자리는 최소 commit초 지키고, 다음 찾기는 cooldown초 뒤에만 한다. 그 사이에는 제자리에서 겨눈 채 기다린다.
        [SerializeField, Min(0f)] float archerRepositionDelay = 3f;
        [SerializeField, Min(0.5f)] float archerSideStep = 3f;
        [SerializeField, Range(1, 8)] int archerSideStepCount = 3;
        [SerializeField, Min(0f)] float archerSearchCooldown = 10f;
        [SerializeField, Min(0f)] float archerSpotCommitSeconds = 5f;
        // 대규모 수성전 최적화(PERF-siege-ai-scale): 한 프레임에 옆 사격 자리를 탐색할 수 있는 최대 궁수 수(0 = 무제한).
        [SerializeField, Min(0)] int maxArcherRepositionSearchesPerFrame = 2;
        // 지상 공성 적들이 '걸어서 닿는 아군 없음' 결과를 공유하는 시간(초). 성문/성벽 파괴나 사다리 연결 시 즉시 무효화된다.
        [SerializeField, Min(0f)] float reachabilityCacheTtl = 0.5f;
        public float ArcherSearchCooldown => archerSearchCooldown;
        public float ArcherSpotCommitSeconds => archerSpotCommitSeconds;
        public int MaxArcherRepositionSearchesPerFrame => Mathf.Max(0, maxArcherRepositionSearchesPerFrame);
        public float ReachabilityCacheTtl => Mathf.Max(0f, reachabilityCacheTtl);
        public ArcherCoverPlan CreateArcherCoverPlan() =>
            new ArcherCoverPlan(archerRepositionDelay, archerSearchCooldown, archerSpotCommitSeconds);
        public float ArcherRepositionDelay => archerRepositionDelay;
        public float ArcherSideStep => archerSideStep;
        public int ArcherSideStepCount => archerSideStepCount;
        // 후퇴하는 적이 판 바깥 끝에서 멈출 거리와, 길이 막혀도 이 시간이 지나면 이탈로 보는 시간.
        [SerializeField, Min(0f)] float retreatEdgeMargin = 1.5f;
        [SerializeField, Min(0.1f)] float retreatTimeout = 20f;
        public float RetreatEdgeMargin => retreatEdgeMargin;
        public float RetreatTimeout => retreatTimeout;

        [Header("키")]
        [SerializeField] InputAction pushLadder = new InputAction("PushLadder", InputActionType.Button, "<Keyboard>/f");
        [SerializeField] InputAction switchView = new InputAction("SwitchView", InputActionType.Button, "<Keyboard>/tab");
        [SerializeField] InputAction restart = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");

        public int StartingBudget => startingBudget;
        public float LadderRaiseSeconds => ladderRaiseSeconds;
        public float LadderPushSeconds => ladderPushSeconds;
        public float LadderPushHoldSeconds => ladderPushHoldSeconds;
        public float LadderOccupiedPushMultiplier => ladderOccupiedPushMultiplier;
        public float LadderTwoPusherBonus => ladderTwoPusherBonus;
        public float LadderThreePlusPusherBonus => ladderThreePlusPusherBonus;
        public LadderPushTuning CreateLadderPushTuning() => new LadderPushTuning(ladderPushHoldSeconds,
            ladderOccupiedPushMultiplier, ladderTwoPusherBonus, ladderThreePlusPusherBonus);
        public float LadderClimbSeconds => ladderClimbSeconds;
        public float LadderTapSeconds => ladderTapSeconds;
        public float LadderUseRadius => ladderUseRadius;
        public float LadderPushRadius => ladderPushRadius;
        public float LadderBlockRadius => ladderBlockRadius;
        public float LadderHeightTolerance => ladderHeightTolerance;
        public float LadderBaseOffset => ladderBaseOffset;
        public float LadderTopInset => ladderTopInset;
        public float LadderCrowdPenalty => ladderCrowdPenalty;
        public float LadderFallClearance => ladderFallClearance;
        public float LadderFallOutwardSpeed => ladderFallOutwardSpeed;
        public float LadderPushPoise => ladderPushPoise;
        public float LadderPushInterruptCooldown => ladderPushInterruptCooldown;
        public float AttackerDecisionInterval => attackerDecisionInterval;
        public float GateStandOffset => gateStandOffset;
        public float GateSpread => gateSpread;
        public float ArcherApproachRatio => archerApproachRatio;
        public InputAction PushLadder => pushLadder;
        public InputAction SwitchView => switchView;
        public InputAction Restart => restart;

        public int CostOf(BuildPieceDefinition piece)
        {
            foreach (PieceCost entry in pieceCosts) if (entry.piece == piece) return Mathf.Max(0, entry.cost);
            return 0;
        }

        public int CostOf(SiegeWeaponDefinition weapon)
        {
            foreach (WeaponCost entry in weaponCosts) if (entry.weapon == weapon) return Mathf.Max(0, entry.cost);
            return 0;
        }

        public static SiegeSettings Create(int startingBudget = 40, PieceCost[] pieceCosts = null,
            WeaponCost[] weaponCosts = null)
        {
            var value = CreateInstance<SiegeSettings>();
            value.startingBudget = Mathf.Max(0, startingBudget);
            value.pieceCosts = pieceCosts ?? new PieceCost[0];
            value.weaponCosts = weaponCosts ?? new WeaponCost[0];
            return value;
        }
    }
}
