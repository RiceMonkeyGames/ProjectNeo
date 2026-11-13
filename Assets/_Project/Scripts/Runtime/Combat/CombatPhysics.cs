using UnityEngine;

namespace ProjectNeo.Combat
{
    public static class CombatPhysics
    {
        /// <summary>
        /// 유닛이 딛고 선 물체(땅·성벽 윗면). 원거리 공격은 이 물체에 막히지 않는다.
        /// 성벽 위 궁수가 성벽 바로 아래를 내려다보고 쏠 때, 선이 자기 발밑 성벽 모서리를 스치는 것을 막는다.
        /// </summary>
        public static Collider GroundUnder(CharacterActor actor)
        {
            Collider ground = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(actor.transform.position + Vector3.up * 0.3f, Vector3.down,
                0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != actor.gameObject.scene
                    || hit.collider.GetComponentInParent<CharacterActor>() != null || hit.distance >= nearest) continue;
                ground = hit.collider;
                nearest = hit.distance;
            }
            return ground;
        }

        public static bool HasClearShot(CharacterActor attacker, CharacterActor target)
        {
            Vector3 origin = attacker.AimPoint;
            Vector3 offset = target.AimPoint - origin;
            Collider footing = attacker.Profile.AttackKind == AttackKind.Projectile ? GroundUnder(attacker) : null;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != attacker.gameObject.scene || hit.collider == footing) continue;
                var actor = hit.collider.GetComponentInParent<CharacterActor>();
                if (actor == target || actor == attacker) continue;
                if (actor != null && (actor.Team == attacker.Team || actor.Health == null || actor.Health.IsDead)) continue;
                return false;
            }
            return true;
        }

        public static bool IsInRange(CharacterActor attacker, CharacterActor target)
        {
            Collider body = target.Body;
            if (body == null || !body.enabled) return false;
            float range = attacker.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange);
            // 공중 대상은 근접이면 바닥 투영점 기준으로 잰다(Q-43). 원거리는 몸통까지 그대로 잰다.
            AirborneBody air = target.Airborne;
            if (air != null && attacker.Profile.AttackKind == AttackKind.Melee)
                return AirborneReach.InMeleeReach(attacker.transform.position, air.GroundPoint, air.IsMeleeExposed,
                    range, target.Profile.Radius);
            return (body.ClosestPoint(attacker.AimPoint) - attacker.AimPoint).sqrMagnitude <= range * range;
        }

        /// <summary>길찾기로 다가갈 지점. 공중 대상은 바닥 투영점이다.</summary>
        public static Vector3 ReachPoint(CharacterActor target)
        {
            AirborneBody air = target.Airborne;
            return air != null && air.HasGround ? air.GroundPoint : target.transform.position;
        }

        /// <summary>
        /// 대상으로 고를 수 있는지. 근접 공격자는 높이 떠 있는 공중 대상을 쫓지 않는다.
        /// 쫓아가 봐야 닿지 않고, 그동안 땅의 적을 놓치기 때문이다.
        /// </summary>
        public static bool CanTarget(CharacterActor attacker, CharacterActor target)
        {
            AirborneBody air = target.Airborne;
            return air == null || attacker.Profile.AttackKind != AttackKind.Melee
                || AirborneReach.CanMeleeTarget(air.HasGround, air.IsMeleeExposed);
        }
    }
}
