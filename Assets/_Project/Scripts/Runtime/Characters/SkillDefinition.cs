using UnityEngine;

namespace ProjectNeo.Characters
{
    /// <summary>스킬 한 종류의 데이터. 지금은 식별 정보만 있고, 효과는 전투 프로토타입에서 붙인다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Skill", fileName = "Skill_")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;

        public string Id => id;
        public string DisplayName => displayName;
    }
}
