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
    /// 플레이어 영웅 고정 스킬, 퍽 트리(스킬 퍽 없음, UI-07), 자동 시전 수치 연결.
    /// </summary>
    public class SkillDataTests
    {
        const string SkillFolder = "Assets/_Project/Data/GameFlow/Skills";
        static readonly string[] SkillIds =
            { "skill_whirlwind", "skill_war_cry", "skill_hobble", "skill_healing_light", "skill_rally", "skill_shield_bash" };
        static readonly string[] HeroPaths =
        {
            "Assets/_Project/Data/GameFlow/Heroes/hero_swordsman.asset", "Assets/_Project/Data/GameFlow/Heroes/hero_archer.asset",
            "Assets/_Project/Data/GameFlow/Heroes/hero_knight.asset", "Assets/_Project/Data/GameFlow/Heroes/hero_paladin.asset",
            // 게임 씬 플레이어의 영웅 데이터(UI-07: 플레이어도 고정 스킬 3개).
            "Assets/_Project/Data/CombatPrototype/Player.asset",
        };

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
            foreach (string id in HeroPaths)
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroDefinition>(id);
                Assert.IsNotNull(hero, id);
                Assert.AreEqual(SkillSlotRules.MaxSlots, hero.Skills.Count, id);
                Assert.IsTrue(hero.Skills.All(skill => skill != null && SkillIds.Contains(skill.Id)), id);
                Assert.AreEqual(hero.Skills.Count, hero.Skills.Distinct().Count(), id + " has no duplicate skill");
            }
        }

        [Test]
        public void PerkTrees_AreValid_AndOldSkillPerksAreGone()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PerkTreeCatalog>("Assets/_Project/Data/GameFlow/Perks/PerkTrees.asset");
            Assert.IsEmpty(catalog.Validate());
            PerkTreeDefinition combat = catalog.Trees.Single(tree => tree.Category == PerkCategory.Combat);
            Assert.AreEqual(9, combat.Perks.Count, "the 4 former skill perks became stat perks");
            Assert.IsTrue(combat.Perks.All(perk => !perk.Id.StartsWith("perk_combat_skill_")), "no skill perk left");
            Assert.IsTrue(combat.Perks.All(perk => perk.StatEffects.Count > 0), "every Combat perk improves player stats");

            var content = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/_Project/Data/GameFlow/RunContent.asset");
            foreach (PerkDefinition perk in combat.Perks) Assert.AreSame(perk, content.Perk(perk.Id), perk.Id + " is saved");
        }

        [Test]
        public void BattleSettings_LinkAutoCastAsset()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>("Assets/_Project/Data/GameFlow/Battle.asset");
            Assert.IsNotNull(battle.SkillAuto);
            Assert.AreEqual($"{SkillFolder}/SkillAuto.asset", AssetDatabase.GetAssetPath(battle.SkillAuto));
        }

    }
}
