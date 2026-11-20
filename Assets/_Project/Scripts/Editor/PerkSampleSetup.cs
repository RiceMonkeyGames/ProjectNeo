using ProjectNeo.Meta;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// META-02 샘플 퍽 에셋을 만들고 런 콘텐츠 목록(RunContent.asset)에 넣는다. 실제 퍽 트리 내용은 META-09에서 정한다.
    /// 에디터 개발 단축키(F11)가 이 목록의 순서대로 찍는다. 다시 실행하면 같은 값으로 덮어쓴다.
    /// </summary>
    public static class PerkSampleSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Perks";

        [MenuItem("ProjectNeo/Setup/Create Sample Perks (META-02)")]
        public static void Apply()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);
            MoraleBoostPerkEffect morale = SaveOver(MoraleBoostPerkEffect.Create(PerkTrigger.BattleStart, 15f),
                Folder + "/PerkEffect_SampleMoraleAtBattleStart.asset");

            Perk("perk_sample_player_attack", "Sample: Player attack +25%", PerkCategory.Combat,
                stats: new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.25f)) });
            Perk("perk_sample_hero_health", "Sample: Hero health +30%", PerkCategory.UnitBoost,
                stats: new[] { new PerkStatEffect(PerkTarget.Heroes, StatModifier.Percent(StatType.MaxHealth, 0.3f)) });
            Perk("perk_sample_soldier_attack", "Sample: Soldier attack +20%", PerkCategory.UnitBoost,
                stats: new[] { new PerkStatEffect(PerkTarget.Soldiers, StatModifier.Percent(StatType.Attack, 0.2f)) });
            Perk("perk_sample_reward_gold", "Sample: Victory gold +25%", PerkCategory.Economy,
                run: new[] { new PerkRunEffect(PerkRunEffectKind.RewardGoldPercent, 25f) });
            Perk("perk_sample_shop_discount", "Sample: Shop prices -20%", PerkCategory.Economy,
                run: new[] { new PerkRunEffect(PerkRunEffectKind.ShopPricePercent, -20f) });
            Perk("perk_sample_prep_hours", "Sample: Prep time +2h", PerkCategory.Economy,
                run: new[] { new PerkRunEffect(PerkRunEffectKind.PrepHours, 2f) });
            Perk("perk_sample_battle_morale", "Sample: Squad morale +15 at battle start", PerkCategory.UnitBoost,
                special: new PerkSpecialEffect[] { morale });

            AssetDatabase.SaveAssets();
            // 런 저장 목록에 새 퍽을 넣는다(퍽 id를 저장에서 되살리고 F11이 목록을 쓴다).
            RunSaveSetup.Apply();
        }

        static void Perk(string id, string name, PerkCategory category, PerkStatEffect[] stats = null,
            PerkRunEffect[] run = null, PerkSpecialEffect[] special = null)
        {
            SaveOver(PerkDefinition.Create(id, category, stats, special, 1, true, run, name), Folder + "/" + id + ".asset");
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
