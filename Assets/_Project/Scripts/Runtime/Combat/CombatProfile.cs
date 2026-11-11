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
        public float ImpactDelay => impactDelay;
        public float AttackHalfAngle => attackHalfAngle;
        public float Radius => radius;
        public float Height => height;

        public static CombatProfile Create(float impactDelay = 0.18f, float attackHalfAngle = 55f,
            float radius = 0.4f, float height = 2f)
        {
            var profile = CreateInstance<CombatProfile>();
            profile.impactDelay = Mathf.Max(0f, impactDelay);
            profile.attackHalfAngle = Mathf.Clamp(attackHalfAngle, 1f, 180f);
            profile.radius = Mathf.Max(0.01f, radius);
            profile.height = Mathf.Max(0.1f, height);
            return profile;
        }
    }
}
