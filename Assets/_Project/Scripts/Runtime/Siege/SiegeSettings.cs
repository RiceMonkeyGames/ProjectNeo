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
        // 한 줄이 적 한 무리다. 적 프리팹의 SiegeAttackerBrain이 종류(사다리병·성문 파괴병·궁수)를 정한다.
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
        [SerializeField, Min(0.01f)] float ladderPushSeconds = 2f;
        [SerializeField, Min(0.01f)] float ladderClimbSeconds = 2.5f;
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

        [Header("적 판단")]
        [SerializeField, Min(0.05f)] float attackerDecisionInterval = 0.5f;
        // 성문 바깥면에서 서는 자리까지의 거리. 음수면 문기둥 사이 안쪽으로 들어가 문에 닿는다.
        [SerializeField] float gateStandOffset = -0.3f;
        [SerializeField, Min(0f)] float gateSpread = 3f;
        [SerializeField, Range(0.1f, 1f)] float archerApproachRatio = 0.7f;
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
        public float LadderClimbSeconds => ladderClimbSeconds;
        public float LadderUseRadius => ladderUseRadius;
        public float LadderPushRadius => ladderPushRadius;
        public float LadderBlockRadius => ladderBlockRadius;
        public float LadderHeightTolerance => ladderHeightTolerance;
        public float LadderBaseOffset => ladderBaseOffset;
        public float LadderTopInset => ladderTopInset;
        public float LadderCrowdPenalty => ladderCrowdPenalty;
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
