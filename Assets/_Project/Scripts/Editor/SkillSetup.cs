using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Skills;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// COMBAT-06 샘플 스킬 6종(효과 종류마다 하나, 사용자 결정 2026-11-20)과 영웅 고정 스킬 3개씩, 자동 시전 수치를 에셋으로 만든다.
    /// 플레이어 스킬 퍽은 <see cref="PerkTreeSetup"/>이 이 스킬로 만든다. 다시 실행하면 같은 값으로 덮어쓴다(에셋 참조는 유지된다).
    /// </summary>
    public static class SkillSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Skills";
        const string HeroFolder = "Assets/_Project/Data/GameFlow/Heroes";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";
        public const string Whirlwind = "skill_whirlwind";
        public const string WarCry = "skill_war_cry";
        public const string Hobble = "skill_hobble";
        public const string HealingLight = "skill_healing_light";
        public const string Rally = "skill_rally";
        public const string ShieldBash = "skill_shield_bash";

        /// <summary>영웅마다 등급 1·2·3에 열리는 고정 스킬 순서.</summary>
        static readonly (string hero, string[] skills)[] HeroSkills =
        {
            ("hero_swordsman", new[] { Whirlwind, WarCry, ShieldBash }),
            ("hero_archer", new[] { Hobble, Rally, HealingLight }),
            ("hero_knight", new[] { ShieldBash, WarCry, Rally }),
            ("hero_paladin", new[] { HealingLight, ShieldBash, Rally }),
        };

        [MenuItem("ProjectNeo/Setup/Create Skills (COMBAT-06)")]
        public static void Apply()
        {
            Dictionary<string, SkillDefinition> skills = CreateSkills();
            AssignHeroSkills(skills);
            LinkAutoSettings();
            AssetDatabase.SaveAssets();
            // 플레이어 스킬 퍽을 전투 트리에 넣고 런 저장 목록을 갱신한다.
            PerkTreeSetup.Apply();
        }

        /// <summary>샘플 스킬 6종을 만들거나 덮어쓰고 id로 돌려준다.</summary>
        public static Dictionary<string, SkillDefinition> CreateSkills()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);
            var skills = new Dictionary<string, SkillDefinition>();
            Add(skills, Whirlwind, "회오리 베기", "주변 3m 안의 적을 모두 벤다(공격력 100% + 10).",
                12f, SkillTargetMode.Self, 0f, 3f, SkillEffect.Damage(10f, 1f));
            Add(skills, WarCry, "전투 함성", "자기 분대(분대가 없으면 주변 8m 아군)의 공격력을 8초 동안 25% 올린다.",
                25f, SkillTargetMode.OwnSquad, 0f, 8f, SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.25f), 8f));
            Add(skills, Hobble, "발 묶기", "12m 안의 적 하나를 6초 동안 이동 속도 50%, 공격 속도 25% 느리게 한다.",
                15f, SkillTargetMode.TargetUnit, 12f, 0f,
                SkillEffect.Debuff(StatModifier.Percent(StatType.MoveSpeed, -0.5f), 6f),
                SkillEffect.Debuff(StatModifier.Percent(StatType.AttackSpeed, -0.25f), 6f));
            Add(skills, HealingLight, "치유의 빛", "15m 안의 조준 지점 4m 안 아군의 체력을 회복한다(40 + 공격력 50%).",
                20f, SkillTargetMode.AimPoint, 15f, 4f, SkillEffect.Heal(40f, 0.5f));
            Add(skills, Rally, "결집", "자기 분대(분대가 없으면 주변 8m 아군이 속한 분대)의 사기를 15 올린다.",
                30f, SkillTargetMode.OwnSquad, 0f, 8f, SkillEffect.MoraleGain(15f));
            Add(skills, ShieldBash, "방패 강타", "바로 앞의 적 하나를 강하게 쳐서 행동을 끊는다(5 + 공격력 50%, 경직 100).",
                10f, SkillTargetMode.TargetUnit, 2.5f, 0f, SkillEffect.Damage(5f, 0.5f), SkillEffect.Stagger(100f));
            return skills;
        }

        static void Add(Dictionary<string, SkillDefinition> skills, string id, string name, string description,
            float cooldown, SkillTargetMode mode, float range, float radius, params SkillEffect[] effects)
        {
            SkillDefinition skill = SkillDefinition.Create(id, cooldown, mode, range, radius, effects);
            skill.SetText(name, description);
            skills[id] = SaveOver(skill, $"{Folder}/{id}.asset");
        }

        static void AssignHeroSkills(Dictionary<string, SkillDefinition> skills)
        {
            foreach ((string heroId, string[] ids) in HeroSkills)
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroDefinition>($"{HeroFolder}/{heroId}.asset");
                if (hero == null) { Debug.LogError("Skill setup: hero asset is missing: " + heroId); continue; }
                var data = new SerializedObject(hero);
                SerializedProperty list = data.FindProperty("skills");
                list.ClearArray();
                foreach (string id in ids)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = skills[id];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hero);
            }
        }

        static void LinkAutoSettings()
        {
            SkillAutoSettings auto = SaveOver(SkillAutoSettings.Create(), $"{Folder}/SkillAuto.asset");
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle == null) { Debug.LogError("Skill setup: battle settings are missing: " + BattlePath); return; }
            if (battle.SkillAuto == auto) return;
            battle.ConfigureSkillAuto(auto);
            EditorUtility.SetDirty(battle);
        }

        // 이미 있으면 내용을 덮어써서 다른 에셋의 참조(GUID)를 유지한다.
        static T SaveOver<T>(T value, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            string keepName = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.CopySerialized(value, existing);
            existing.name = keepName;
            Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
