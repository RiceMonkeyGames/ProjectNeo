using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬 대상 고르기(Q-63, COMBAT-06). 효과마다 적·아군 편이 다르므로 편을 받아 그 편의 대상만 모은다.
    /// <list type="bullet">
    /// <item>Self: 시전자 중심 반경. 반경 0이면 시전자 자신(아군 편)만.</item>
    /// <item>AimPoint: 조준 지점(사거리로 당김) 중심 반경.</item>
    /// <item>TargetUnit: 조준한 유닛 하나. 편이 맞을 때만.</item>
    /// <item>OwnSquad: 시전자 분대의 살아 있는 구성원(아군 편만). 분대가 없으면 시전자 중심 반경, 반경 0이면 시전자 자신.</item>
    /// </list>
    /// </summary>
    public static class SkillTargeting
    {
        /// <summary>조준 지점을 시전자에서 사거리 안으로 당긴다(높이는 그대로).</summary>
        public static Vector3 ClampAim(Vector3 origin, Vector3 aim, float range)
        {
            Vector3 flat = aim - origin;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance <= range || distance <= 0f) return aim;
            Vector3 clamped = origin + flat * (range / distance);
            clamped.y = aim.y;
            return clamped;
        }

        /// <summary>효과 중심(시각 효과·AI 판단용).</summary>
        public static Vector3 Center(CharacterActor caster, SkillDefinition skill, SkillAim aim)
        {
            switch (skill.TargetMode)
            {
                case SkillTargetMode.AimPoint:
                    return ClampAim(caster.transform.position, aim.Point, skill.Range);
                case SkillTargetMode.TargetUnit:
                    return aim.Unit != null ? aim.Unit.transform.position : caster.transform.position;
                default:
                    return caster.transform.position;
            }
        }

        /// <summary>TargetUnit 스킬의 대상이 사거리 안인지(몸 반경 포함).</summary>
        public static bool InUnitRange(CharacterActor caster, CharacterActor target, float range)
        {
            if (caster == null || target == null) return false;
            float reach = range + (target.Profile != null ? target.Profile.Radius : 0f);
            return (target.transform.position - caster.transform.position).sqrMagnitude <= reach * reach;
        }

        public static bool IsAlly(CharacterActor caster, CharacterActor other) =>
            caster != null && other != null && other.isActiveAndEnabled && other.gameObject.scene == caster.gameObject.scene
            && other.Health != null && !other.Health.IsDead && other.Team == caster.Team;

        public static bool IsSide(CharacterActor caster, CharacterActor other, bool enemies) =>
            enemies ? caster.IsEnemy(other) : IsAlly(caster, other);

        /// <summary>편(<paramref name="enemies"/>)에 맞는 대상을 <paramref name="into"/>에 모은다.</summary>
        public static void Collect(CharacterActor caster, SkillDefinition skill, SkillAim aim, bool enemies,
            ISkillContext context, List<CharacterActor> into)
        {
            into.Clear();
            if (caster == null || skill == null) return;
            switch (skill.TargetMode)
            {
                case SkillTargetMode.TargetUnit:
                    if (IsSide(caster, aim.Unit, enemies)) into.Add(aim.Unit);
                    return;
                case SkillTargetMode.OwnSquad:
                    if (enemies) return;
                    int squad = context != null ? context.SquadOf(caster) : -1;
                    if (squad >= 0)
                    {
                        context.CollectSquad(squad, into);
                        into.RemoveAll(unit => !IsAlly(caster, unit));
                        if (!into.Contains(caster)) into.Add(caster);
                        return;
                    }
                    CollectArea(caster, caster.transform.position, skill.Radius, false, into);
                    return;
                default:
                    CollectArea(caster, Center(caster, skill, aim), skill.Radius, enemies, into);
                    return;
            }
        }

        static void CollectArea(CharacterActor caster, Vector3 center, float radius, bool enemies, List<CharacterActor> into)
        {
            if (radius <= 0f)
            {
                if (!enemies && IsAlly(caster, caster)) into.Add(caster);
                return;
            }
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (!IsSide(caster, actor, enemies)) continue;
                float reach = radius + (actor.Profile != null ? actor.Profile.Radius : 0f);
                if ((actor.transform.position - center).sqrMagnitude <= reach * reach) into.Add(actor);
            }
        }
    }
}
