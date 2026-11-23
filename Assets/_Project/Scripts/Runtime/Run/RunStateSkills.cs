using System.Collections.Generic;
using ProjectNeo.Skills;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 런 영웅의 스킬(COMBAT-06, UI-07 2026-11-23). 스킬은 퍽과 따로이고, 플레이어를 포함한 모든 영웅이
    /// 영웅 데이터의 고정 스킬 3개를 등급 순서대로 연다(<see cref="SkillSlotRules.OpenHeroSkills"/>). 고르는 칸은 없다.
    /// 플레이어는 처음부터 최고 등급이라 스킬이 모두 열려 있다(<see cref="RunState.PlayerGrade"/>).
    /// </summary>
    public sealed partial class RunState
    {
        /// <summary>영웅이 지금 등급으로 쓸 수 있는 스킬(칸 순서). 영웅이 없으면 빈 목록.</summary>
        public static List<SkillDefinition> OpenSkills(RunHero hero) =>
            hero != null && hero.Definition != null
                ? SkillSlotRules.OpenHeroSkills(hero.Definition.Skills, hero.Grade)
                : new List<SkillDefinition>();

        /// <summary>플레이어가 전투에서 쓸 스킬(Z/X/C 칸 순서).</summary>
        public List<SkillDefinition> PlayerSkills() => OpenSkills(Player);
    }
}
