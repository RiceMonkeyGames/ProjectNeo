using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 액티브 스킬 한 종류의 데이터(Q-62~Q-65, COMBAT-06). 비용은 쿨다운뿐이고 전투 시작 시 모두 준비 상태다.
    /// 대상 방식과 효과 목록이 데이터라서 새 스킬은 에셋만 추가하면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Skill", fileName = "Skill_")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, TextArea] string description;
        [Tooltip("다시 쓸 수 있을 때까지 걸리는 시간(초).")]
        [SerializeField, Min(0f)] float cooldown = 10f;
        [SerializeField] SkillTargetMode targetMode = SkillTargetMode.Self;
        [Tooltip("조준 지점·대상 유닛까지의 최대 거리(m). Self/OwnSquad는 쓰지 않는다.")]
        [SerializeField, Min(0f)] float range = 15f;
        [Tooltip("효과 반경(m). Self/AimPoint에서 쓴다. 0이면 중심 유닛 하나만.")]
        [SerializeField, Min(0f)] float radius = 4f;
        [SerializeField] List<SkillEffect> effects = new List<SkillEffect>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description ?? string.Empty;
        public float Cooldown => cooldown;
        public SkillTargetMode TargetMode => targetMode;
        public float Range => range;
        public float Radius => radius;
        public IReadOnlyList<SkillEffect> Effects => effects ??= new List<SkillEffect>();

        /// <summary>코드에서 스킬을 만든다(테스트·에디터 설정용).</summary>
        public static SkillDefinition Create(string id, float cooldown, SkillTargetMode targetMode,
            float range, float radius, params SkillEffect[] effects)
        {
            var skill = CreateInstance<SkillDefinition>();
            skill.name = id;
            skill.id = id;
            skill.displayName = id;
            skill.cooldown = Mathf.Max(0f, cooldown);
            skill.targetMode = targetMode;
            skill.range = Mathf.Max(0f, range);
            skill.radius = Mathf.Max(0f, radius);
            skill.effects = new List<SkillEffect>(effects ?? new SkillEffect[0]);
            return skill;
        }

        /// <summary>표시 이름과 설명을 바꾼다(에디터 설정용).</summary>
        public void SetText(string newDisplayName, string newDescription)
        {
            displayName = newDisplayName;
            description = newDescription;
        }
    }
}
