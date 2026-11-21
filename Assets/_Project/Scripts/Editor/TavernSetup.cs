using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-3 주점 프로토타입 데이터. 고용 영웅 4명(등급 1·1·2·3, 고용 60·60·120·200골드, Q-48)을 만들고, 잡졸 프리팹을 몸체로 연결하며,
    /// 주점 시설 정의의 영웅 풀이 모두 비어 있을 때만 레벨별 풀을 채운다(인스펙터에서 바꾼 값은 덮어쓰지 않는다). 씬은 건드리지 않는다.
    /// </summary>
    public static class TavernSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Heroes";
        const string TavernPath = "Assets/_Project/Data/GameFlow/Facilities/Facility_Tavern.asset";
        const string MeleeBody = "Assets/_Project/Prefabs/UnitBattlePrototype/allied_melee.prefab";
        const string RangedBody = "Assets/_Project/Prefabs/UnitBattlePrototype/allied_ranged.prefab";

        [MenuItem("ProjectNeo/Setup/Create Tavern Heroes")]
        public static void Apply()
        {
            CreateData();
            AssetDatabase.SaveAssets();
        }

        public static void CreateData()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data/GameFlow", "Heroes");
            // 프로토타입 임의 값(2026-11-13). 같은 등급 잡졸보다 체력·공격·방어가 높고, 등급이 오를수록 강하다.
            HeroDefinition swordsman = Hero("hero_swordsman", "용병 검사", 1, 60, MeleeBody, 180f, 30f, 6f, 1.5f);
            HeroDefinition archer = Hero("hero_archer", "용병 궁수", 1, 60, RangedBody, 130f, 26f, 3f,
                ArcherRangeMigration.HeroArcherRange, ArcherRangeMigration.DefaultSpread, ArcherRangeMigration.ArcherAttackSpeed);
            HeroDefinition knight = Hero("hero_knight", "기사", 2, 120, MeleeBody, 260f, 38f, 9f, 1.6f);
            HeroDefinition paladin = Hero("hero_paladin", "성기사", 3, 200, MeleeBody, 360f, 48f, 12f, 1.6f);

            var tavern = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(TavernPath);
            if (tavern == null) return;
            var data = new SerializedObject(tavern);
            SerializedProperty levels = data.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("heroPool").arraySize > 0) return;
            var pools = new[]
            {
                new[] { (swordsman, 1f), (archer, 1f) },
                new[] { (swordsman, 1f), (archer, 1f), (knight, 1f) },
                new[] { (swordsman, 0.5f), (archer, 0.5f), (knight, 1f), (paladin, 1f) },
            };
            for (int i = 0; i < levels.arraySize && i < pools.Length; i++)
            {
                SerializedProperty pool = levels.GetArrayElementAtIndex(i).FindPropertyRelative("heroPool");
                foreach ((HeroDefinition hero, float weight) in pools[i])
                {
                    pool.InsertArrayElementAtIndex(pool.arraySize);
                    SerializedProperty entry = pool.GetArrayElementAtIndex(pool.arraySize - 1);
                    entry.FindPropertyRelative("item").objectReferenceValue = hero;
                    entry.FindPropertyRelative("weight").floatValue = weight;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tavern);
        }

        // 이미 있으면 그대로 둔다(인스펙터에서 바꾼 값 보존). 없으면 만들고 이름·스탯·몸체를 넣는다.
        static HeroDefinition Hero(string id, string name, int tier, int gold, string bodyPath, float health, float attack,
            float defense, float range, float spread = 0f, float attackSpeed = 1f)
        {
            string path = $"{Folder}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<HeroDefinition>(path);
            if (existing != null) return existing;
            HeroDefinition hero = HeroDefinition.Create(id, tier, new Cost(gold));
            hero.SetUnitPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath));
            AssetDatabase.CreateAsset(hero, path);
            var data = new SerializedObject(hero);
            data.FindProperty("displayName").stringValue = name;
            SerializedProperty stats = data.FindProperty("baseStats");
            stats.ClearArray();
            var values = new List<StatValue>
            {
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
                new StatValue(StatType.Defense, defense), new StatValue(StatType.AttackRange, range),
                new StatValue(StatType.AttackSpeed, attackSpeed), new StatValue(StatType.MoveSpeed, 3.6f),
                new StatValue(StatType.AimSpread, spread),
            };
            foreach (StatValue value in values)
            {
                stats.InsertArrayElementAtIndex(stats.arraySize);
                SerializedProperty entry = stats.GetArrayElementAtIndex(stats.arraySize - 1);
                entry.FindPropertyRelative("stat").enumValueIndex = (int)value.stat;
                entry.FindPropertyRelative("value").floatValue = value.value;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hero);
            return hero;
        }
    }
}
