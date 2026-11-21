using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 발사 속도와 중력으로 포물선을 그리며 난다(COMBAT-09). 충돌은 매 프레임 이동 구간 전체에서 검사하고,
    /// 땅·성벽 등 무엇에든 닿으면 그 자리에서 멈춘다. 수명(ProjectileLifetime)이 지나면 사라진다.
    /// StuckArrow가 붙은 화살은 맞은 자리에 박혀 남는다(ART-05).
    /// </summary>
    public sealed class CombatProjectile : MonoBehaviour
    {
        CharacterActor owner;
        CombatTeam team;
        CombatProfile profile;
        Vector3 start;
        Vector3 velocity;
        float gravity;
        float attack;
        float elapsed;
        bool launched;
        float launchHeight;
        Collider footing;

        public void Launch(CharacterActor source, float attackPower, Vector3 launchVelocity)
        {
            owner = source;
            launchHeight = source.transform.position.y;
            // 쏜 유닛이 딛고 선 물체는 통과한다. 사선 검사(CombatPhysics.HasClearShot)와 같은 기준이다.
            footing = CombatPhysics.GroundUnder(source);
            team = source.Team;
            profile = source.Profile;
            attack = attackPower;
            start = transform.position;
            velocity = launchVelocity;
            gravity = profile.ProjectileGravity;
            elapsed = 0f;
            launched = true;
            Face(launchVelocity);
        }

        void Update()
        {
            if (!launched) return;
            float delta = Mathf.Min(Time.deltaTime, profile.ProjectileLifetime - elapsed);
            Vector3 origin = transform.position;
            // SphereCast는 시작점에 이미 겹친 충돌체를 놓칠 수 있어 별도로 검사한다.
            foreach (Collider overlap in Physics.OverlapSphere(origin, profile.ProjectileRadius, ~0, QueryTriggerInteraction.Ignore))
                if (TryImpact(overlap)) return;

            elapsed += delta;
            Vector3 next = ArrowBallistics.Evaluate(start, velocity, gravity, elapsed);
            Vector3 step = next - origin;
            float distance = step.magnitude;
            if (distance > 0f)
            {
                Vector3 direction = step / distance;
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
            }
            transform.position = next;
            Face(ArrowBallistics.VelocityAt(velocity, gravity, elapsed));
            if (elapsed >= profile.ProjectileLifetime) Destroy(gameObject);
        }

        void Face(Vector3 heading)
        {
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);
        }

        bool ShouldIgnore(Collider collider)
        {
            if (collider.gameObject.scene != gameObject.scene || collider == footing || ShotCover.Passes(collider, team)) return true;
            var actor = collider.GetComponentInParent<CharacterActor>();
            return actor != null && (actor == owner || actor.Team == team
                || actor.Health == null || actor.Health.IsDead);
        }

        bool TryImpact(Collider collider)
        {
            if (ShouldIgnore(collider)) return false;
            var target = collider.GetComponentInParent<CharacterActor>();
            // 위로 쏠 때의 감소는 성벽 위를 노릴 때만이다. 공중 대상은 언제나 온전한 피해를 받는다(Q-43).
            if (target != null)
                target.ReceiveDamage(DamageRules.Calculate(attack, target.Stats.GetValue(StatType.Defense), profile.MinimumDamage)
                    * (target.Airborne != null ? 1f : DamageRules.ElevationMultiplier(target.transform.position.y - launchHeight,
                        profile.UpwardShotMinimumRise, profile.UpwardShotDamageRatio)), owner);
            // 화살 프리팹(StuckArrow)은 맞은 자리에 박혀 남고, 그렇지 않은 발사체는 사라진다(ART-05).
            if (!TryGetComponent(out StuckArrow arrow) || !arrow.Stick(collider, profile.ProjectileRadius)) Destroy(gameObject);
            launched = false;
            return true;
        }
    }
}
