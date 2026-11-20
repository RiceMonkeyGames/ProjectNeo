using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 화면 가운데 조준선으로 스킬 조준(지점과 유닛)을 고른다(COMBAT-06).
    /// 처음 맞은 면이 지점이고, 그 앞에 유닛이 맞았거나 조준선 가까이(반경) 있으면 그 유닛을 대상으로 잡는다.
    /// </summary>
    public static class SkillAimFinder
    {
        static readonly RaycastHit[] hits = new RaycastHit[64];

        public static SkillAim FromRay(Ray ray, float maxDistance, float unitRadius, CharacterActor caster)
        {
            Vector3 point = ray.origin + ray.direction * maxDistance;
            float surface = maxDistance;
            CharacterActor unit = null;
            float unitDistance = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!SameScene(hits[i].collider, caster)) continue;
                CharacterActor actor = hits[i].collider.GetComponentInParent<CharacterActor>();
                if (actor == caster && actor != null) continue;
                if (actor != null)
                {
                    if (hits[i].distance < unitDistance && IsLiving(actor)) { unit = actor; unitDistance = hits[i].distance; }
                    continue;
                }
                if (hits[i].distance < surface) { surface = hits[i].distance; point = hits[i].point; }
            }
            if (unit != null && unitDistance > surface) unit = null;
            if (unit == null && unitRadius > 0f)
            {
                count = Physics.SphereCastNonAlloc(ray, unitRadius, hits, surface, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (!SameScene(hits[i].collider, caster)) continue;
                    CharacterActor actor = hits[i].collider.GetComponentInParent<CharacterActor>();
                    if (actor == null || actor == caster || !IsLiving(actor) || hits[i].distance >= unitDistance) continue;
                    unit = actor;
                    unitDistance = hits[i].distance;
                }
            }
            return new SkillAim(point, unit);
        }

        static bool SameScene(Collider collider, CharacterActor caster) =>
            caster == null || collider.gameObject.scene == caster.gameObject.scene;

        static bool IsLiving(CharacterActor actor) => actor.Health != null && !actor.Health.IsDead && actor.isActiveAndEnabled;
    }
}
