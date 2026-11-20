using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>시전할 때 겨눈 곳(COMBAT-06). 지점은 AimPoint, 유닛은 TargetUnit 스킬이 쓴다.</summary>
    public readonly struct SkillAim
    {
        public readonly Vector3 Point;
        public readonly CharacterActor Unit;

        public SkillAim(Vector3 point, CharacterActor unit = null)
        {
            Point = point;
            Unit = unit;
        }

        public static SkillAim At(Vector3 point) => new SkillAim(point);
        public static SkillAim AtUnit(CharacterActor unit) =>
            new SkillAim(unit != null ? unit.transform.position : Vector3.zero, unit);
    }
}
