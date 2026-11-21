using UnityEngine;

namespace ProjectNeo.Combat
{
    [CreateAssetMenu(menuName = "ProjectNeo/Combat Profile")]
    public sealed class CombatProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] float impactDelay = 0.18f;
        [SerializeField, Range(1f, 180f)] float attackHalfAngle = 55f;
        [SerializeField, Min(0.01f)] float radius = 0.4f;
        [SerializeField, Min(0.1f)] float height = 2f;
        [SerializeField] AttackKind attackKind = AttackKind.Melee;
        [SerializeField, Min(0f)] float minimumDamage = 1f;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField, Min(0.01f)] float projectileSpeed = 18f;
        [SerializeField, Min(0.01f)] float projectileRadius = 0.08f;
        [SerializeField, Min(0.01f)] float projectileLifetime = 5f;
        // 화살 포물선 중력(COMBAT-09). 발사 속도(projectileSpeed)와 함께 낮은 포물선을 정한다. 0이면 곧게 난다.
        [SerializeField, Min(0f)] float projectileGravity = 9.81f;
        // 아래에서 위로 쏠 때의 피해 비율. 대상이 이 높이 이상 위에 있으면 적용한다. 1이면 변화 없음.
        [SerializeField, Min(0f)] float upwardShotMinimumRise = 1.5f;
        [SerializeField, Range(0f, 1f)] float upwardShotDamageRatio = 1f;
        // 높은 곳에서 아래로 쏠 때의 사거리 보너스. 쏘는 쪽이 이 높이 이상 위에 있으면 사거리에 (1 + 보너스)를 곱한다. 0이면 변화 없음.
        [SerializeField, Min(0f)] float highGroundMinimumDrop = 1.5f;
        [SerializeField, Min(0f)] float highGroundRangeBonus = 0.25f;
        [SerializeField, Min(0f)] float staggerPower = 10f;
        // 몸 가운데가 가려졌을 때 대신 노리는 위쪽 몸통 높이(키 대비, SIEGE-10). 낮은 난간 뒤에서도 상체는 맞는다.
        [SerializeField, Range(0.5f, 1f)] float upperAimRatio = 0.85f;
        // 성가퀴 뒤에서 웅크렸을 때의 키 비율(COMBAT-10). 판정 몸통과 조준점이 이만큼 낮아진다. 2m × 0.55 = 1.1m(받침 높이).
        [SerializeField, Range(0.3f, 1f)] float crouchHeightRatio = 0.55f;
        // 다음 발사 가능 시점보다 이만큼(초) 먼저 일어나 겨눈다(COMBAT-10).
        [SerializeField, Min(0f)] float coverRiseLead = 0.5f;
        // 이 거리(m, 수평) 안에 자기 편 성가퀴가 있으면 그 뒤에 선 것으로 본다(COMBAT-10). 0이면 웅크리지 않는다.
        [SerializeField, Min(0f)] float coverSearchRadius = 1.2f;
        // 원거리 공격이 쏘는 유닛의 발밑 물체(성벽 조각)를 스쳐도 되는 깊이(m, 발 높이 아래).
        // 모서리를 스치는 정도만 허용한다. 3m 성문 위 한가운데 궁수가 성문 앞 적의 상체를 맞히지 못하는 값이다. 이보다 깊이 벽 몸통을 지나면 막힌다(BUG-wall-archer-shoots-through-footing).
        [SerializeField, Min(0f)] float footingClearance = 0.3f;
        public float FootingClearance => footingClearance;
        public float CrouchHeightRatio => crouchHeightRatio;
        public float CoverRiseLead => coverRiseLead;
        public float CoverSearchRadius => coverSearchRadius;
        public float HighGroundMinimumDrop => highGroundMinimumDrop;
        public float UpperAimRatio => upperAimRatio;
        public float HighGroundRangeBonus => highGroundRangeBonus;
        public float UpwardShotMinimumRise => upwardShotMinimumRise;
        public float UpwardShotDamageRatio => upwardShotDamageRatio;
        public float StaggerPower => staggerPower;
        public float ImpactDelay => impactDelay;
        public float AttackHalfAngle => attackHalfAngle;
        public float Radius => radius;
        public float Height => height;
        public AttackKind AttackKind => attackKind;
        public float MinimumDamage => minimumDamage;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileGravity => projectileGravity;

        public static CombatProfile Create(float impactDelay = 0.18f, float attackHalfAngle = 55f,
            float radius = 0.4f, float height = 2f, AttackKind attackKind = AttackKind.Melee,
            GameObject projectilePrefab = null, float projectileSpeed = 18f,
            float projectileRadius = 0.08f, float projectileLifetime = 5f, float minimumDamage = 1f,
            float upwardShotDamageRatio = 1f, float upwardShotMinimumRise = 1.5f,
            float highGroundRangeBonus = 0.25f, float highGroundMinimumDrop = 1.5f, float staggerPower = -1f, float projectileGravity = 9.81f,
            float crouchHeightRatio = 0.55f, float coverRiseLead = 0.5f, float coverSearchRadius = 1.2f, float footingClearance = 0.3f)
        {
            var profile = CreateInstance<CombatProfile>();
            profile.impactDelay = Mathf.Max(0f, impactDelay);
            profile.attackHalfAngle = Mathf.Clamp(attackHalfAngle, 1f, 180f);
            profile.radius = Mathf.Max(0.01f, radius);
            profile.height = Mathf.Max(0.1f, height);
            profile.attackKind = attackKind;
            profile.minimumDamage = Mathf.Max(0f, minimumDamage);
            profile.projectilePrefab = projectilePrefab;
            profile.projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
            profile.projectileRadius = Mathf.Max(0.01f, projectileRadius);
            profile.projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
            profile.projectileGravity = Mathf.Max(0f, projectileGravity);
            profile.upwardShotDamageRatio = Mathf.Clamp01(upwardShotDamageRatio);
            profile.upwardShotMinimumRise = Mathf.Max(0f, upwardShotMinimumRise);
            profile.highGroundRangeBonus = Mathf.Max(0f, highGroundRangeBonus);
            profile.highGroundMinimumDrop = Mathf.Max(0f, highGroundMinimumDrop);
            profile.staggerPower = staggerPower >= 0f ? staggerPower : (attackKind == AttackKind.Projectile ? 10f : 25f);
            profile.crouchHeightRatio = Mathf.Clamp(crouchHeightRatio, 0.3f, 1f);
            profile.coverRiseLead = Mathf.Max(0f, coverRiseLead);
            profile.coverSearchRadius = Mathf.Max(0f, coverSearchRadius);
            profile.footingClearance = Mathf.Max(0f, footingClearance);
            return profile;
        }
    }
}
