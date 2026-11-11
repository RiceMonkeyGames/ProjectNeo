using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>발사 때 정한 직선으로 이동한다. 충돌은 이동 구간 전체에서 검사한다.</summary>
    public sealed class CombatProjectile : MonoBehaviour
    {
        CharacterActor owner;
        CombatTeam team;
        CombatProfile profile;
        Vector3 direction;
        float attack;
        float remainingDistance;
        float remainingTime;
        bool launched;

        public void Launch(CharacterActor source, float attackPower, Vector3 flightDirection, float range)
        {
            owner = source;
            team = source.Team;
            profile = source.Profile;
            attack = attackPower;
            direction = flightDirection.normalized;
            remainingDistance = range;
            remainingTime = profile.ProjectileLifetime;
            launched = true;
        }

        void Update()
        {
            if (!launched) return;
            float delta = Mathf.Min(Time.deltaTime, remainingTime);
            float distance = Mathf.Min(profile.ProjectileSpeed * delta, remainingDistance);
            Vector3 origin = transform.position;
            // SphereCast는 시작점에 이미 겹친 충돌체를 놓칠 수 있어 별도로 검사한다.
            foreach (Collider overlap in Physics.OverlapSphere(origin, profile.ProjectileRadius, ~0, QueryTriggerInteraction.Ignore))
                if (TryImpact(overlap)) return;

            RaycastHit? nearest = null;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, profile.ProjectileRadius, direction,
                distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ShouldIgnore(hit.collider)) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (nearest.HasValue)
            {
                transform.position = origin + direction * nearest.Value.distance;
                TryImpact(nearest.Value.collider);
                return;
            }
            transform.position += direction * distance;
            remainingDistance -= distance;
            remainingTime -= delta;
            if (remainingDistance <= 0f || remainingTime <= 0f) Destroy(gameObject);
        }

        bool ShouldIgnore(Collider collider)
        {
            if (collider.gameObject.scene != gameObject.scene) return true;
            var actor = collider.GetComponentInParent<CharacterActor>();
            return actor != null && (actor == owner || actor.Team == team
                || actor.Health == null || actor.Health.IsDead);
        }

        bool TryImpact(Collider collider)
        {
            if (ShouldIgnore(collider)) return false;
            var target = collider.GetComponentInParent<CharacterActor>();
            if (target != null)
                target.ReceiveDamage(DamageRules.Calculate(attack, target.Stats.GetValue(StatType.Defense), profile.MinimumDamage));
            Destroy(gameObject);
            launched = false;
            return true;
        }
    }
}
