using System;
using ProjectNeo.Characters;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>플레이어와 영웅이 공유하는 전투 몸통. 입력과 외형은 별도 컴포넌트다.</summary>
    public sealed class CharacterActor : MonoBehaviour
    {
        [SerializeField] HeroDefinition definition;
        [SerializeField] CombatProfile profile;
        float cooldown;
        float impactRemaining;
        bool pendingImpact;
        public Character Character { get; private set; }
        public Health Health { get; private set; }
        public CombatProfile Profile => profile;
        public event Action AttackStarted;
        public event Action HitReceived;
        public event Action Died;

        public void Configure(HeroDefinition hero, CombatProfile combatProfile)
        {
            definition = hero;
            profile = combatProfile;
        }

        void Awake()
        {
            if (definition == null || profile == null)
            {
                Debug.LogError("CharacterActor needs a hero definition and combat profile.", this);
                enabled = false;
                return;
            }
            Character = new Character(definition);
            Health = new Health(Character.Stats);
            Health.Died += OnDied;
        }

        void Update()
        {
            if (Health == null || Health.IsDead) return;
            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
            if (!pendingImpact) return;
            impactRemaining -= Time.deltaTime;
            if (impactRemaining > 0f) return;
            pendingImpact = false;
            ResolveImpact();
        }

        public bool TryAttack()
        {
            if (Health == null || Health.IsDead || pendingImpact || cooldown > 0f) return false;
            float speed = Character.Stats.GetValue(StatType.AttackSpeed);
            if (speed <= 0f) return false;
            cooldown = Mathf.Max(1f / speed, profile.ImpactDelay);
            impactRemaining = profile.ImpactDelay;
            pendingImpact = true;
            AttackStarted?.Invoke();
            return true;
        }

        void ResolveImpact()
        {
            float range = Character.Stats.GetValue(StatType.AttackRange);
            Vector3 origin = transform.position + Vector3.up * (profile.Height * 0.5f);
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider candidate in Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = candidate.GetComponentInParent<CharacterActor>();
                if (target == null || target == this || target.Health == null || target.Health.IsDead) continue;
                Vector3 offset = candidate.ClosestPoint(origin) - origin;
                Vector3 direction = target.transform.position - transform.position;
                direction.y = 0f;
                if (Vector3.Angle(transform.forward, direction) > profile.AttackHalfAngle) continue;
                float distance = offset.sqrMagnitude;
                if (distance >= nearestDistance) continue;
                // 벽 너머의 대상은 때리지 않는다.
                if (Physics.Linecast(origin, candidate.bounds.center, out RaycastHit hit, ~0,
                    QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<CharacterActor>() != target) continue;
                nearest = target;
                nearestDistance = distance;
            }
            if (nearest != null) nearest.ReceiveDamage(Character.Stats.GetValue(StatType.Attack));
        }

        public void ReceiveDamage(float damage)
        {
            if (Health != null && Health.TakeDamage(damage) > 0f) HitReceived?.Invoke();
        }

        void OnDied()
        {
            pendingImpact = false;
            foreach (Collider body in GetComponents<Collider>()) body.enabled = false;
            Died?.Invoke();
        }

        void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.Dispose();
        }
    }
}
