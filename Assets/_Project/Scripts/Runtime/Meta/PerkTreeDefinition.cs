using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>퍽 트리 하나(META-09, Q-72). 퍽은 창에 보일 순서대로 두고, 순서 제약은 각 퍽의 선행 퍽이 맡는다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Perk Tree", fileName = "PerkTree_")]
    public sealed class PerkTreeDefinition : ScriptableObject
    {
        [SerializeField] PerkCategory category;
        [SerializeField] string displayName;
        [SerializeField] List<PerkDefinition> perks = new List<PerkDefinition>();

        public PerkCategory Category => category;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? category.ToString() : displayName;
        public IReadOnlyList<PerkDefinition> Perks => perks;

        public static PerkTreeDefinition Create(PerkCategory category, string displayName, IEnumerable<PerkDefinition> perks)
        {
            var tree = CreateInstance<PerkTreeDefinition>();
            tree.category = category;
            tree.displayName = displayName ?? string.Empty;
            if (perks != null) foreach (PerkDefinition perk in perks) if (perk != null) tree.perks.Add(perk);
            return tree;
        }
    }
}
