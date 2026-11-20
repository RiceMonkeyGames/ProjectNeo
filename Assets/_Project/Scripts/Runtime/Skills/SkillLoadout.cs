using System.Collections.Generic;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 플레이어가 장착한 스킬 3칸(Q-72, COMBAT-06). 배운 스킬은 전투 트리 퍽에서 오고,
    /// 배운 수가 3개를 넘을 때만 준비 단계에서 고른다. 칸에는 스킬 id만 저장해 런 저장에 그대로 담을 수 있다.
    /// </summary>
    public sealed class SkillLoadout
    {
        readonly string[] equipped = new string[SkillSlotRules.PlayerSlots];

        public int SlotCount => equipped.Length;

        /// <summary>칸의 스킬 id. 비어 있으면 null.</summary>
        public string EquippedId(int slot) => slot >= 0 && slot < equipped.Length ? equipped[slot] : null;

        public bool IsEquipped(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            foreach (string id in equipped)
                if (id == skillId) return true;
            return false;
        }

        /// <summary>
        /// 배운 스킬을 칸에 넣는다. 이미 다른 칸에 있으면 그 칸과 맞바꾼다.
        /// 배우지 않은 스킬이거나 칸 번호가 틀리면 false.
        /// </summary>
        public bool TryEquip(int slot, string skillId, IReadOnlyList<SkillDefinition> learned)
        {
            if (slot < 0 || slot >= equipped.Length || !Contains(learned, skillId)) return false;
            for (int i = 0; i < equipped.Length; i++)
                if (i != slot && equipped[i] == skillId) equipped[i] = equipped[slot];
            equipped[slot] = skillId;
            return true;
        }

        public bool Unequip(int slot)
        {
            if (slot < 0 || slot >= equipped.Length || equipped[slot] == null) return false;
            equipped[slot] = null;
            return true;
        }

        /// <summary>
        /// 배운 목록에 맞춘다: 배우지 않은(퍽이 초기화된) 스킬은 빼고, 빈 칸은 아직 장착하지 않은 배운 스킬로 배운 순서대로 채운다.
        /// 그래서 배운 스킬이 3개 이하이면 고르지 않아도 모두 장착된다.
        /// </summary>
        public void Refresh(IReadOnlyList<SkillDefinition> learned)
        {
            for (int i = 0; i < equipped.Length; i++)
                if (equipped[i] != null && !Contains(learned, equipped[i])) equipped[i] = null;
            if (learned == null) return;
            foreach (SkillDefinition skill in learned)
            {
                if (skill == null || IsEquipped(skill.Id)) continue;
                int empty = System.Array.IndexOf(equipped, null);
                if (empty < 0) return;
                equipped[empty] = skill.Id;
            }
        }

        /// <summary>장착 칸 순서대로 스킬을 돌려준다(빈 칸은 건너뛴다). 전투용 <see cref="SkillSlots"/>를 만들 때 쓴다.</summary>
        public List<SkillDefinition> EquippedSkills(IReadOnlyList<SkillDefinition> learned)
        {
            var result = new List<SkillDefinition>();
            foreach (string id in equipped)
            {
                SkillDefinition skill = Find(learned, id);
                if (skill != null) result.Add(skill);
            }
            return result;
        }

        /// <summary>저장된 id로 칸을 되살린다(배우지 않은 id는 다음 <see cref="Refresh"/>에서 빠진다).</summary>
        public void Restore(IReadOnlyList<string> ids)
        {
            for (int i = 0; i < equipped.Length; i++)
                equipped[i] = ids != null && i < ids.Count && !string.IsNullOrEmpty(ids[i]) ? ids[i] : null;
        }

        /// <summary>저장용 id 목록(빈 칸은 빈 문자열).</summary>
        public List<string> Capture()
        {
            var ids = new List<string>(equipped.Length);
            foreach (string id in equipped) ids.Add(id ?? string.Empty);
            return ids;
        }

        static bool Contains(IReadOnlyList<SkillDefinition> learned, string skillId) => Find(learned, skillId) != null;

        static SkillDefinition Find(IReadOnlyList<SkillDefinition> learned, string skillId)
        {
            if (learned == null || string.IsNullOrEmpty(skillId)) return null;
            foreach (SkillDefinition skill in learned)
                if (skill != null && skill.Id == skillId) return skill;
            return null;
        }
    }
}
