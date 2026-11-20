using System.Collections.Generic;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// META-09 퍽 트리(사용자 승인 2026-11-20)를 에셋으로 만들고 RunStart 설정과 런 저장 목록에 연결한다.
    /// META-02 샘플 퍽은 지운다. 다시 실행하면 같은 값으로 덮어쓴다(에셋 참조는 유지된다).
    /// </summary>
    public static class PerkTreeSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Perks";
        public const string CatalogPath = Folder + "/PerkTrees.asset";
        const string RunStartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";

        static readonly string[] OldSamples =
        {
            "perk_sample_player_attack", "perk_sample_hero_health", "perk_sample_soldier_attack", "perk_sample_reward_gold",
            "perk_sample_shop_discount", "perk_sample_prep_hours", "perk_sample_battle_morale", "PerkEffect_SampleMoraleAtBattleStart",
        };

        [MenuItem("ProjectNeo/Setup/Create Perk Trees (META-09)")]
        public static void Apply()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);
            foreach (string name in OldSamples) AssetDatabase.DeleteAsset(Folder + "/" + name + ".asset");

            // 전투(플레이어)
            PerkDefinition c1 = Perk("perk_combat_sharpened_blade", "벼린 칼날", PerkCategory.Combat, null,
                Stat(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.15f)));
            PerkDefinition c2 = Perk("perk_combat_toughness", "강인함", PerkCategory.Combat, new[] { c1 },
                Stat(PerkTarget.Player, StatModifier.Percent(StatType.MaxHealth, 0.2f)));
            PerkDefinition c3 = Perk("perk_combat_swift_strikes", "빠른 일격", PerkCategory.Combat, new[] { c1 },
                Stat(PerkTarget.Player, StatModifier.Percent(StatType.AttackSpeed, 0.1f)));
            PerkDefinition c4 = Perk("perk_combat_iron_skin", "강철 피부", PerkCategory.Combat, new[] { c2 },
                Stat(PerkTarget.Player, StatModifier.Flat(StatType.Defense, 5f)));
            PerkDefinition c5 = Perk("perk_combat_champion", "투사", PerkCategory.Combat, new[] { c3, c4 },
                Stat(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.2f)));

            // 내실
            PerkDefinition e1 = Perk("perk_economy_bounty", "현상금", PerkCategory.Economy, null,
                run: Run(PerkRunEffectKind.RewardGoldPercent, 15f));
            PerkDefinition e2 = Perk("perk_economy_haggler", "흥정꾼", PerkCategory.Economy, new[] { e1 },
                run: Run(PerkRunEffectKind.ShopPricePercent, -10f));
            PerkDefinition e3 = Perk("perk_economy_long_days", "긴 하루", PerkCategory.Economy, new[] { e1 },
                run: Run(PerkRunEffectKind.PrepHours, 1f));
            PerkDefinition e4 = Perk("perk_economy_master_trader", "거상", PerkCategory.Economy, new[] { e2 },
                run: Run(PerkRunEffectKind.ShopPricePercent, -10f));
            PerkDefinition e5 = Perk("perk_economy_war_chest", "군자금", PerkCategory.Economy, new[] { e3, e4 },
                run: Run(PerkRunEffectKind.RewardGoldPercent, 20f));

            // 유닛 강화
            MoraleBoostPerkEffect rally = SaveOver(MoraleBoostPerkEffect.Create(PerkTrigger.BattleStart, 10f),
                Folder + "/PerkEffect_RallyMorale.asset");
            MoraleBoostPerkEffect avenge = SaveOver(MoraleBoostPerkEffect.Create(PerkTrigger.AllySquadMemberDied, 3f),
                Folder + "/PerkEffect_AvengeMorale.asset");
            PerkDefinition u1 = Perk("perk_units_drilled_ranks", "훈련된 대열", PerkCategory.UnitBoost, null,
                Stat(PerkTarget.Soldiers, StatModifier.Percent(StatType.Attack, 0.1f)));
            PerkDefinition u2 = Perk("perk_units_shield_wall", "방패벽", PerkCategory.UnitBoost, new[] { u1 },
                Stat(PerkTarget.Soldiers, StatModifier.Percent(StatType.MaxHealth, 0.15f)));
            PerkDefinition u3 = Perk("perk_units_veteran_captains", "노련한 대장", PerkCategory.UnitBoost, new[] { u1 },
                Stat(PerkTarget.Heroes, StatModifier.Percent(StatType.MaxHealth, 0.2f)));
            PerkDefinition u4 = Perk("perk_units_rally", "재집결", PerkCategory.UnitBoost, new[] { u2 }, special: rally);
            PerkDefinition u5 = Perk("perk_units_avenge_them", "복수", PerkCategory.UnitBoost, new[] { u3 }, special: avenge);

            PerkTreeDefinition combat = SaveOver(PerkTreeDefinition.Create(PerkCategory.Combat, "전투", new[] { c1, c2, c3, c4, c5 }),
                Folder + "/PerkTree_Combat.asset");
            PerkTreeDefinition economy = SaveOver(PerkTreeDefinition.Create(PerkCategory.Economy, "경제", new[] { e1, e2, e3, e4, e5 }),
                Folder + "/PerkTree_Economy.asset");
            PerkTreeDefinition units = SaveOver(PerkTreeDefinition.Create(PerkCategory.UnitBoost, "유닛 강화", new[] { u1, u2, u3, u4, u5 }),
                Folder + "/PerkTree_UnitEnhancement.asset");
            PerkTreeCatalog catalog = SaveOver(PerkTreeCatalog.Create(combat, economy, units), CatalogPath);
            foreach (string problem in catalog.Validate()) Debug.LogError("Perk trees: " + problem, catalog);

            var runStart = AssetDatabase.LoadAssetAtPath<RunConfig>(RunStartPath)
                ?? throw new System.InvalidOperationException("Run start config is missing: " + RunStartPath);
            var data = new SerializedObject(runStart);
            SerializedProperty field = data.FindProperty("perkTrees");
            if (field.objectReferenceValue != catalog)
            {
                field.objectReferenceValue = catalog;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(runStart);
            }
            AssetDatabase.SaveAssets();
            // 런 저장 목록의 퍽을 새 퍽으로 바꾼다.
            RunSaveSetup.Apply();
        }

        static PerkStatEffect[] Stat(PerkTarget target, StatModifier modifier) => new[] { new PerkStatEffect(target, modifier) };
        static PerkRunEffect[] Run(PerkRunEffectKind kind, float value) => new[] { new PerkRunEffect(kind, value) };

        static PerkDefinition Perk(string id, string name, PerkCategory category, PerkDefinition[] prerequisites,
            PerkStatEffect[] stats = null, PerkRunEffect[] run = null, PerkSpecialEffect special = null)
        {
            PerkSpecialEffect[] specials = special != null ? new[] { special } : null;
            return SaveOver(PerkDefinition.Create(id, category, stats, specials, 1, true, run, name, prerequisites),
                Folder + "/" + id + ".asset");
        }

        // 이미 있으면 내용을 덮어써서 다른 에셋의 참조(GUID)를 유지한다.
        static T SaveOver<T>(T value, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
