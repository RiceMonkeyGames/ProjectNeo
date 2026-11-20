using System.Collections.Generic;
using ProjectNeo.Skills;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 이번 런의 플레이어 스킬(COMBAT-06, Q-72). 배운 스킬은 찍은 전투 트리 퍽에서 오므로 런이 끝나면 함께 사라진다.
    /// 배운 스킬이 3개 이하이면 모두 장착되고, 넘으면 준비 단계에서 3개를 고른다(<see cref="SkillLoadout"/>).
    /// </summary>
    public sealed partial class RunState
    {
        /// <summary>플레이어가 장착한 스킬 칸(스킬 id).</summary>
        public SkillLoadout PlayerSkillLoadout { get; } = new SkillLoadout();

        /// <summary>찍은 퍽으로 배운 플레이어 스킬(찍은 순서). 퍽 묶음이 없으면 빈 목록.</summary>
        public List<SkillDefinition> LearnedPlayerSkills() =>
            Perks != null ? Perks.LearnedSkills() : new List<SkillDefinition>();

        /// <summary>배운 스킬에 맞춰 장착 칸을 정리하고 장착한 스킬을 칸 순서대로 돌려준다(전투 시작 때 쓴다).</summary>
        public List<SkillDefinition> EquippedPlayerSkills()
        {
            List<SkillDefinition> learned = LearnedPlayerSkills();
            PlayerSkillLoadout.Refresh(learned);
            return PlayerSkillLoadout.EquippedSkills(learned);
        }

        /// <summary>배운 스킬을 첫 빈 칸에 장착한다. 이미 장착했거나, 배우지 않았거나, 빈 칸이 없으면 false.</summary>
        public bool TryEquipPlayerSkill(string skillId)
        {
            if (PlayerSkillLoadout.IsEquipped(skillId)) return false;
            for (int slot = 0; slot < PlayerSkillLoadout.SlotCount; slot++)
                if (PlayerSkillLoadout.EquippedId(slot) == null)
                    return PlayerSkillLoadout.TryEquip(slot, skillId, LearnedPlayerSkills());
            return false;
        }

        /// <summary>칸의 스킬을 뺀다. 빈 칸은 다음 전투 시작 때 남은 배운 스킬로 채워진다(<see cref="EquippedPlayerSkills"/>).</summary>
        public bool UnequipPlayerSkill(int slot) => PlayerSkillLoadout.Unequip(slot);
    }
}
