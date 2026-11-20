using System;
using System.Collections.Generic;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 전투 중 한 유닛의 스킬 칸과 쿨다운(Q-64, COMBAT-06). 비용은 쿨다운뿐이고 만들 때 모든 칸이 준비 상태다.
    /// 시간은 <see cref="Tick"/>으로만 흐르므로 EditMode에서 그대로 검증할 수 있다.
    /// </summary>
    public sealed class SkillSlots
    {
        readonly SkillDefinition[] skills;
        readonly float[] remaining;

        /// <summary>칸 상태가 바뀌었을 때(사용, 준비 완료). 인자는 칸 번호.</summary>
        public event Action<int> Changed;

        public SkillSlots(IEnumerable<SkillDefinition> slotSkills)
        {
            var list = new List<SkillDefinition>();
            if (slotSkills != null)
                foreach (SkillDefinition skill in slotSkills)
                    if (skill != null && list.Count < SkillSlotRules.MaxSlots) list.Add(skill);
            skills = list.ToArray();
            remaining = new float[skills.Length];
        }

        public int Count => skills.Length;

        public SkillDefinition Get(int slot) => IsValid(slot) ? skills[slot] : null;

        /// <summary>남은 쿨다운(초). 준비 상태면 0.</summary>
        public float Remaining(int slot) => IsValid(slot) ? remaining[slot] : 0f;

        public bool IsReady(int slot) => IsValid(slot) && remaining[slot] <= 0f;

        /// <summary>남은 쿨다운 비율(1 = 막 사용, 0 = 준비). HUD 표시용.</summary>
        public float RemainingFraction(int slot)
        {
            if (!IsValid(slot)) return 0f;
            float cooldown = skills[slot].Cooldown;
            return cooldown > 0f ? Math.Min(1f, remaining[slot] / cooldown) : 0f;
        }

        /// <summary>준비된 칸이면 쿨다운을 시작하고 true. 효과 적용은 호출한 쪽이 한다.</summary>
        public bool TryUse(int slot)
        {
            if (!IsReady(slot)) return false;
            remaining[slot] = skills[slot].Cooldown;
            Changed?.Invoke(slot);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0f) continue;
                remaining[i] = Math.Max(0f, remaining[i] - deltaTime);
                if (remaining[i] <= 0f) Changed?.Invoke(i);
            }
        }

        /// <summary>모든 칸을 준비 상태로 되돌린다(전투 시작).</summary>
        public void ResetAll()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0f) continue;
                remaining[i] = 0f;
                Changed?.Invoke(i);
            }
        }

        bool IsValid(int slot) => slot >= 0 && slot < skills.Length;
    }
}
