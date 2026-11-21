using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>
    /// 스테이지 보스 하나(SIEGE-09, Q-68/Q-74). M2의 임시 보스는 기존 적 프리팹을 바탕으로 능력치·크기·역할만 바꾼다.
    /// 능력치는 바탕 적의 값을 덮어쓰는 목표값이다(0이면 바탕 값을 그대로 둔다). 실제 보스는 M4(CONTENT-02)에서 바꾼다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [Tooltip("처치 기록·보상에 쓰는 ID. 적 프리팹 이름과 겹치지 않게 boss_로 시작한다.")]
        [SerializeField] string id = "boss";
        [SerializeField] string displayName = "보스";
        [SerializeField] BossRole role = BossRole.Siege;
        [Tooltip("바탕이 되는 기존 적 프리팹. SiegeAttacker가 있고 비활성 상태여야 한다.")]
        [SerializeField] GameObject basePrefab;
        [Tooltip("겉모습(Visual 자식) 크기 배율. 충돌·길찾기 크기는 바꾸지 않는다.")]
        [SerializeField, Min(0.1f)] float visualScale = 1.5f;

        [Header("능력치 (0 = 바탕 값 유지)")]
        [SerializeField, Min(0f)] float maxHealth;
        [SerializeField, Min(0f)] float attack;
        [SerializeField, Min(0f)] float defense;
        [SerializeField, Min(0f)] float attackRange;
        [Tooltip("원거리 탄퍼짐 각도(도, COMBAT-09). 0이면 바탕 값을 그대로 둔다.")]
        [SerializeField, Min(0f)] float aimSpread;
        [Tooltip("이동 속도 배율. 1이면 그대로.")]
        [SerializeField, Min(0.1f)] float moveSpeedMultiplier = 1f;

        [Header("역할")]
        [Tooltip("성문·성벽·공성 무기에 주는 피해 배율(공성형).")]
        [SerializeField, Min(0f)] float structureDamageMultiplier = 1f;
        [Tooltip("결투형이 플레이어·영웅을 고를 때의 거리 배율. 3이면 3배 먼 영웅도 가까운 병사보다 먼저 고른다.")]
        [SerializeField, Min(1f)] float heroPreference = 3f;
        [Tooltip("지휘형 강화 반경(m).")]
        [SerializeField, Min(0f)] float auraRadius;
        [Tooltip("지휘형 강화: 주변 적 공격력 비율(0.2 = +20%).")]
        [SerializeField] float auraAttackPercent;
        [Tooltip("지휘형 강화: 주변 적 방어력 더하기.")]
        [SerializeField] float auraDefenseFlat;
        [Tooltip("지휘형이 죽으면 같은 웨이브의 남은 적 공격력 비율(0.2 = -20%). 남은 전투 동안 유지된다.")]
        [SerializeField, Min(0f)] float deathAttackPenalty;

        [Header("처치 보상")]
        [SerializeField, Min(0)] int rewardGold;
        [SerializeField] BossMaterialReward[] rewardMaterials = new BossMaterialReward[0];

        public string Id => id;
        public string DisplayName => displayName;
        public BossRole Role => role;
        public GameObject BasePrefab => basePrefab;
        public float VisualScale => visualScale;
        public float MaxHealth => maxHealth;
        public float Attack => attack;
        public float Defense => defense;
        public float AttackRange => attackRange;
        public float AimSpread => aimSpread;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float StructureDamageMultiplier => structureDamageMultiplier;
        public float HeroPreference => heroPreference;
        public float AuraRadius => auraRadius;
        public float AuraAttackPercent => auraAttackPercent;
        public float AuraDefenseFlat => auraDefenseFlat;
        public float DeathAttackPenalty => deathAttackPenalty;
        public int RewardGold => rewardGold;
        public IReadOnlyList<BossMaterialReward> RewardMaterials => rewardMaterials;
        /// <summary>결투형만 플레이어·영웅을 먼저 노린다.</summary>
        public bool PrefersHeroes => role == BossRole.Duelist;
        /// <summary>지휘형이고 반경이 있으면 강화를 건다.</summary>
        public bool HasAura => role == BossRole.Commander && auraRadius > 0f;

        public static BossDefinition Create(string id, string displayName, BossRole role, GameObject basePrefab,
            float visualScale, float maxHealth, float attack, float defense, float attackRange = 0f,
            float moveSpeedMultiplier = 1f, float structureDamageMultiplier = 1f, int rewardGold = 0,
            params BossMaterialReward[] rewardMaterials)
        {
            var boss = CreateInstance<BossDefinition>();
            boss.name = id;
            boss.id = id; boss.displayName = displayName; boss.role = role; boss.basePrefab = basePrefab;
            boss.visualScale = Mathf.Max(0.1f, visualScale);
            boss.maxHealth = Mathf.Max(0f, maxHealth); boss.attack = Mathf.Max(0f, attack);
            boss.defense = Mathf.Max(0f, defense); boss.attackRange = Mathf.Max(0f, attackRange);
            boss.moveSpeedMultiplier = Mathf.Max(0.1f, moveSpeedMultiplier);
            boss.structureDamageMultiplier = Mathf.Max(0f, structureDamageMultiplier);
            boss.rewardGold = Mathf.Max(0, rewardGold);
            boss.rewardMaterials = rewardMaterials ?? new BossMaterialReward[0];
            return boss;
        }

        /// <summary>지휘형 강화와 죽을 때 약화를 정한다(테스트·설정 도구용).</summary>
        public BossDefinition WithAura(float radius, float attackPercent, float defenseFlat, float deathPenalty)
        {
            auraRadius = Mathf.Max(0f, radius); auraAttackPercent = attackPercent;
            auraDefenseFlat = defenseFlat; deathAttackPenalty = Mathf.Max(0f, deathPenalty);
            return this;
        }

        /// <summary>원거리 탄퍼짐 목표값을 정한다(테스트·설정 도구용, COMBAT-09). 0이면 바탕 값 유지.</summary>
        public BossDefinition WithAimSpread(float degrees)
        {
            aimSpread = Mathf.Max(0f, degrees);
            return this;
        }

        /// <summary>결투형의 영웅 선호 배율을 정한다(테스트·설정 도구용).</summary>
        public BossDefinition WithHeroPreference(float preference)
        {
            heroPreference = Mathf.Max(1f, preference);
            return this;
        }
    }
}
