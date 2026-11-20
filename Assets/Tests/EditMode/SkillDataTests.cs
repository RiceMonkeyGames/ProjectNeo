using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Skills;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// COMBAT-06 5단계 데이터(Editor/SkillSetup, PerkTreeSetup): 샘플 스킬 6종, 영웅 고정 스킬 3개씩,
    /// 전투 트리 스킬 퍽, 런 저장 목록, 자동 시전 수치 연결. 배운 스킬 규칙(퍽 → 스킬)도 함께 본다.
    /// </summary>
    public class SkillDataTests
    {
        const string SkillFolder = "Assets/_Project/Data/GameFlow/Skills";
        static readonly string[] SkillIds =
            { "skill_whirlwind", "skill_war_cry", "skill_hobble", "skill_healing_light", "skill_rally", "skill_shield_bash" };
        static readonly string[] HeroIds = { "hero_swordsman", "hero_archer", "hero_knight", "hero_paladin" };

        static SkillDefinition Skill(string id) =>
            AssetDatabase.LoadAssetAtPath<SkillDefinition>($"{SkillFolder}/{id}.asset");

        [Test]
        public void SampleSkills_CoverEveryEffectKind_WithNamesAndCooldowns()
        {
            var kinds = new HashSet<SkillEffectKind>();
            foreach (string id in SkillIds)
            {
                SkillDefinition skill = Skill(id);
                Assert.IsNotNull(skill, id);
                Assert.AreEqual(id, skill.Id);
                Assert.AreEqual(id, skill.name, "asset name matches file");
                Assert.IsFalse(string.IsNullOrEmpty(skill.DisplayName), id);
                Assert.IsFalse(string.IsNullOrEmpty(skill.Description), id);
                Assert.Greater(skill.Cooldown, 0f, id);
                Assert.Greater(skill.Effects.Count, 0, id);
                foreach (SkillEffect effect in skill.Effects) kinds.Add(effect.kind);
            }
            CollectionAssert.AreEquivalent(System.Enum.GetValues(typeof(SkillEffectKind)), kinds.ToArray(),
                "one sample per effect family");
        }

        [Test]
        public void Heroes_HaveThreeFixedSampleSkills()
        {
            foreach (string id in HeroIds)
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroDefinition>($"Assets/_Project/Data/GameFlow/Heroes/{id}.asset");
                Assert.IsNotNull(hero, id);
                Assert.AreEqual(SkillSlotRules.MaxSlots, hero.Skills.Count, id);
                Assert.IsTrue(hero.Skills.All(skill => skill != null && SkillIds.Contains(skill.Id)), id);
                Assert.AreEqual(hero.Skills.Count, hero.Skills.Distinct().Count(), id + " has no duplicate skill");
            }
        }

        [Test]
        public void CombatTree_HasSkillPerks_InRunContent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PerkTreeCatalog>("Assets/_Project/Data/GameFlow/Perks/PerkTrees.asset");
            PerkTreeDefinition combat = catalog.Trees.Single(tree => tree.Category == PerkCategory.Combat);
            List<PerkDefinition> skillPerks = combat.Perks.Where(perk => perk.GrantedSkill != null).ToList();
            Assert.GreaterOrEqual(skillPerks.Count, 4, "more skills than slots so the equip choice matters");
            Assert.AreEqual(skillPerks.Count, skillPerks.Select(perk => perk.GrantedSkill).Distinct().Count());
            Assert.IsEmpty(catalog.Validate());
            foreach (PerkTreeDefinition tree in catalog.Trees.Where(tree => tree.Category != PerkCategory.Combat))
                Assert.IsTrue(tree.Perks.All(perk => perk.GrantedSkill == null), "only the Combat tree grants skills");

            var content = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/_Project/Data/GameFlow/RunContent.asset");
            foreach (PerkDefinition perk in skillPerks) Assert.AreSame(perk, content.Perk(perk.Id), perk.Id + " is saved");
        }

        [Test]
        public void BattleSettings_LinkAutoCastAsset()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>("Assets/_Project/Data/GameFlow/Battle.asset");
            Assert.IsNotNull(battle.SkillAuto);
            Assert.AreEqual($"{SkillFolder}/SkillAuto.asset", AssetDatabase.GetAssetPath(battle.SkillAuto));
        }

        [Test]
        public void LearnedSkills_FollowAllocationOrder_WithoutDuplicates()
        {
            var created = new List<Object>();
            try
            {
                SkillDefinition a = SkillDefinition.Create("a", 1f, SkillTargetMode.Self, 0f, 0f);
                SkillDefinition b = SkillDefinition.Create("b", 1f, SkillTargetMode.Self, 0f, 0f);
                created.Add(a); created.Add(b);
                PerkDefinition stat = PerkDefinition.Create("stat", PerkCategory.Combat);
                PerkDefinition grantB = PerkDefinition.Create("grant_b", PerkCategory.Combat, grantedSkill: b);
                PerkDefinition grantA = PerkDefinition.Create("grant_a", PerkCategory.Combat, grantedSkill: a);
                PerkDefinition grantAAgain = PerkDefinition.Create("grant_a2", PerkCategory.Combat, grantedSkill: a);
                created.AddRange(new Object[] { stat, grantB, grantA, grantAAgain });

                var loadout = new PerkLoadout(new MetaProgress { perkPoints = 10 });
                Assert.IsTrue(loadout.TryAllocate(grantB));
                Assert.IsTrue(loadout.TryAllocate(stat));
                Assert.IsTrue(loadout.TryAllocate(grantA));
                Assert.IsTrue(loadout.TryAllocate(grantAAgain));
                CollectionAssert.AreEqual(new[] { b, a }, loadout.LearnedSkills());

                loadout.Reset();
                Assert.IsEmpty(loadout.LearnedSkills(), "skills reset with the perk allocation");
            }
            finally { foreach (Object obj in created) Object.DestroyImmediate(obj); }
        }
    }
}
