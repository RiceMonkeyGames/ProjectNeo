using System.Collections.Generic;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬 칸 수 규칙(Q-62, Q-73, COMBAT-06): 최대 3칸. 플레이어는 처음부터 3칸,
    /// 영웅은 등급만큼(1 -> 2 -> 3) 열리고 영웅 데이터의 고정 스킬을 순서대로 쓴다.
    /// </summary>
    public static class SkillSlotRules
    {
        public const int MaxSlots = 3;
        public const int PlayerSlots = MaxSlots;

        /// <summary>등급에 따른 영웅의 열린 칸 수(1~3).</summary>
        public static int HeroSlots(int grade)
        {
            if (grade < 1) return 1;
            return grade > MaxSlots ? MaxSlots : grade;
        }

        /// <summary>영웅의 고정 스킬 목록에서 등급으로 열린 스킬만 순서대로 돌려준다(빈 칸은 건너뛴다).</summary>
        public static List<SkillDefinition> OpenHeroSkills(IReadOnlyList<SkillDefinition> fixedSkills, int grade)
        {
            var open = new List<SkillDefinition>();
            if (fixedSkills == null) return open;
            int slots = HeroSlots(grade);
            for (int i = 0; i < fixedSkills.Count && i < slots; i++)
                if (fixedSkills[i] != null) open.Add(fixedSkills[i]);
            return open;
        }
    }
}
