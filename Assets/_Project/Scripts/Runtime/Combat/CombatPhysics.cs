using UnityEngine;

namespace ProjectNeo.Combat
{
    public static class CombatPhysics
    {
        public static bool HasClearShot(CharacterActor attacker, CharacterActor target)
        {
            Vector3 origin = attacker.AimPoint;
            Vector3 offset = target.AimPoint - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != attacker.gameObject.scene) continue;
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
            return (body.ClosestPoint(attacker.AimPoint) - attacker.AimPoint).sqrMagnitude <= range * range;
        }
    }
}
