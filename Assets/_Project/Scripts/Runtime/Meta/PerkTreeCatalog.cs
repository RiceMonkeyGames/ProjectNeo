using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>퍽 트리 전체(META-09). 전투·내실·유닛 강화 트리를 창에 보일 순서대로 둔다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Perk Tree Catalog", fileName = "PerkTrees")]
    public sealed class PerkTreeCatalog : ScriptableObject
    {
        [SerializeField] List<PerkTreeDefinition> trees = new List<PerkTreeDefinition>();

        public IReadOnlyList<PerkTreeDefinition> Trees => trees;

        public static PerkTreeCatalog Create(params PerkTreeDefinition[] trees)
        {
            var catalog = CreateInstance<PerkTreeCatalog>();
            if (trees != null) foreach (PerkTreeDefinition tree in trees) if (tree != null) catalog.trees.Add(tree);
            return catalog;
        }

        public IEnumerable<PerkDefinition> AllPerks()
        {
            foreach (PerkTreeDefinition tree in trees)
            {
                if (tree == null) continue;
                foreach (PerkDefinition perk in tree.Perks) if (perk != null) yield return perk;
            }
        }

        /// <summary>
        /// 데이터 실수를 찾는다: 빈 id, 겹치는 id(트리를 통틀어), 트리 종류와 다른 분류의 퍽, 트리에 없는 선행 퍽, 자기 자신을 선행으로 둔 퍽.
        /// 문제가 없으면 빈 목록.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var perks = new HashSet<PerkDefinition>();
            var ids = new HashSet<string>();
            foreach (PerkTreeDefinition tree in trees)
            {
                if (tree == null) { problems.Add("Empty tree slot."); continue; }
                foreach (PerkDefinition perk in tree.Perks)
                {
                    if (perk == null) { problems.Add($"Empty perk slot in {tree.DisplayName}."); continue; }
                    perks.Add(perk);
                    if (string.IsNullOrEmpty(perk.Id)) problems.Add($"Perk without id in {tree.DisplayName}.");
                    else if (!ids.Add(perk.Id)) problems.Add($"Duplicate perk id '{perk.Id}'.");
                    if (perk.Category != tree.Category) problems.Add($"Perk '{perk.Id}' is {perk.Category} but sits in the {tree.Category} tree.");
                }
            }
            foreach (PerkDefinition perk in perks)
                foreach (PerkDefinition required in perk.Prerequisites)
                {
                    if (required == perk) problems.Add($"Perk '{perk.Id}' requires itself.");
                    else if (required == null || !perks.Contains(required)) problems.Add($"Perk '{perk.Id}' requires a perk outside the trees.");
                }
            return problems;
        }
    }
}
