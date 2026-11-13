using UnityEngine;

namespace ProjectNeo.Morale
{
    /// <summary>
    /// 사기 규칙 수치(Q-04·Q-38·Q-39). 기본값은 기획의 시작값이며 플레이하며 에셋에서 조정한다.
    /// 규칙 설명은 Docs/design/MoraleAndWave.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Morale Settings", fileName = "MoraleSettings")]
    public sealed class MoraleSettings : ScriptableObject
    {
        [Header("범위")]
        [SerializeField, Min(1f)] float maxMorale = 200f;
        [SerializeField, Min(0f)] float startingMorale = 150f;
        [Tooltip("사기가 이 값까지 떨어지면 전투당 한 번 한계 판정. 이 값 미만으로 전투를 시작하면 약한 붕괴 효과를 안고 시작한다.")]
        [SerializeField, Min(0f)] float breakingPoint = 100f;

        [Header("한계 판정")]
        [SerializeField, Range(0f, 1f)] float awakenChance = 0.25f;
        [Tooltip("100 미만으로 시작한 부대의 붕괴 효과 세기(1 = 판정으로 걸린 붕괴와 같음).")]
        [SerializeField, Range(0f, 1f)] float startingCollapseStrength = 0.5f;

        [Header("능력 보정")]
        [SerializeField, Min(0f)] float highBandThreshold = 150f;
        [SerializeField, Min(0f)] float highBandAttackBonus = 0.1f;
        [Tooltip("breakingPoint 미만일 때 공격력 감소. 적에게도 적용된다.")]
        [SerializeField, Min(0f)] float lowBandAttackPenalty = 0.1f;

        [Header("사기 감소")]
        [Tooltip("처음 인원이 모두 죽었을 때의 총 감소량. 한 명이 죽으면 이 값 / 처음 인원만큼 줄어든다.")]
        [SerializeField, Min(0f)] float fullSquadLoss = 100f;
        [SerializeField, Min(0f)] float captainDeathLoss = 40f;
        [SerializeField, Min(0f)] float gateDestroyedLoss = 25f;
        [SerializeField, Min(0f)] float nearbyWallCollapseLoss = 15f;
        [SerializeField, Min(0f)] float nearbySquadRoutLoss = 20f;
        [Tooltip("근처 부대·근처 성벽 붕괴를 판단하는 거리(m). 씬 연결에서 쓴다.")]
        [SerializeField, Min(0f)] float nearbyRadius = 15f;

        [Header("각성 효과")]
        [Tooltip("사기 상승: 자기 부대와 근처 부대에 더하는 사기.")]
        [SerializeField, Min(0f)] float rallyAmount = 40f;
        [Tooltip("결사항전: 처음 인원을 모두 잃었을 때의 공격력 증가(잃은 비율에 비례).")]
        [SerializeField, Min(0f)] float lastStandMaxAttackBonus = 0.5f;

        [Header("붕괴 효과")]
        [SerializeField, Range(0f, 1f)] float fearIgnoreChance = 0.5f;
        [SerializeField, Min(0.1f)] float panicInterval = 8f;
        [SerializeField, Min(0f)] float contagionAmount = 5f;
        [SerializeField, Min(0.1f)] float contagionInterval = 5f;

        [Header("전투 후 회복")]
        [SerializeField, Min(0f)] float postBattleRecovery = 20f;
        [Tooltip("전투 후 사기의 최소값. 전투 후 사기 = max(현재 + 회복, 이 값), 최대 maxMorale.")]
        [SerializeField, Min(0f)] float postBattleFloor = 80f;

        public float MaxMorale => maxMorale;
        public float StartingMorale => startingMorale;
        public float BreakingPoint => breakingPoint;
        public float AwakenChance => awakenChance;
        public float StartingCollapseStrength => startingCollapseStrength;
        public float HighBandThreshold => highBandThreshold;
        public float HighBandAttackBonus => highBandAttackBonus;
        public float LowBandAttackPenalty => lowBandAttackPenalty;
        public float FullSquadLoss => fullSquadLoss;
        public float CaptainDeathLoss => captainDeathLoss;
        public float GateDestroyedLoss => gateDestroyedLoss;
        public float NearbyWallCollapseLoss => nearbyWallCollapseLoss;
        public float NearbySquadRoutLoss => nearbySquadRoutLoss;
        public float NearbyRadius => nearbyRadius;
        public float RallyAmount => rallyAmount;
        public float LastStandMaxAttackBonus => lastStandMaxAttackBonus;
        public float FearIgnoreChance => fearIgnoreChance;
        public float PanicInterval => panicInterval;
        public float ContagionAmount => contagionAmount;
        public float ContagionInterval => contagionInterval;
        public float PostBattleRecovery => postBattleRecovery;
        public float PostBattleFloor => postBattleFloor;

        /// <summary>새 부대의 사기(시작값).</summary>
        public SquadMorale CreateSquadMorale() => new SquadMorale(maxMorale, startingMorale);

        /// <summary>코드에서 기본값 설정을 만든다(테스트용).</summary>
        public static MoraleSettings Create() => CreateInstance<MoraleSettings>();

        /// <summary>코드에서 주요 수치를 바꾼 설정을 만든다(테스트용).</summary>
        public static MoraleSettings Create(float awakenChance, float startingCollapseStrength = 0.5f)
        {
            MoraleSettings settings = Create();
            settings.awakenChance = awakenChance;
            settings.startingCollapseStrength = startingCollapseStrength;
            return settings;
        }
    }
}
