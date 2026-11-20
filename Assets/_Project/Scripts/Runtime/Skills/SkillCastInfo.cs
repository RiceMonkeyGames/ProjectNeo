using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>시전 신호(COMBAT-06). 외형·소리·HUD가 듣는다. 규칙은 애니메이션을 직접 부르지 않는다.</summary>
    public readonly struct SkillCastInfo
    {
        public readonly CharacterActor Caster;
        public readonly SkillDefinition Skill;
        public readonly int Slot;
        public readonly Vector3 Center;
        /// <summary>효과가 걸린 유닛 수(편 구분 없이, 중복 제외).</summary>
        public readonly int AffectedCount;

        public SkillCastInfo(CharacterActor caster, SkillDefinition skill, int slot, Vector3 center, int affectedCount)
        {
            Caster = caster;
            Skill = skill;
            Slot = slot;
            Center = center;
            AffectedCount = affectedCount;
        }
    }
}
