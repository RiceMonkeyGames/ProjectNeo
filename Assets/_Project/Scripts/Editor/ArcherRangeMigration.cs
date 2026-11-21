using System;
using ProjectNeo.Characters;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// COMBAT-09(2026-11-21 사용자 결정): 이미 만든 궁병·영웅 궁수·명사수 에셋에 늘어난 사거리와 탄퍼짐을 넣고,
    /// 같은 값이면 건드리지 않으므로 여러 번 실행해도 같다.
    /// </summary>
    public static class ArcherRangeMigration
    {
        public const float SoldierRange = 16f, HeroArcherRange = 18f, MarksmanRange = 30f;
        public const float DefaultSpread = 3f, MarksmanSpread = 1f;
        /// <summary>원거리 잡졸·영웅 궁수 공격 속도(초당 0.4발 = 2.5초에 한 발, COMBAT-10 사용자 결정 2026-11-21).</summary>
        public const float ArcherAttackSpeed = 0.4f;

        static readonly (string path, float range)[] Soldiers =
        {
            ("Assets/_Project/Data/UnitBattlePrototype/allied_ranged.asset", SoldierRange),
            ("Assets/_Project/Data/UnitBattlePrototype/hostile_ranged.asset", SoldierRange),
        };
        const string HeroArcher = "Assets/_Project/Data/GameFlow/Heroes/hero_archer.asset";
        const string Marksman = BossSetup.Folder + "/boss_marksman.asset";

        [MenuItem("ProjectNeo/Setup/Apply Archer Range And Spread (COMBAT-09)")]
        public static void MigrateFromMenu() => Debug.Log(Migrate());

        public static string Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            int changed = 0;
            foreach ((string path, float range) in Soldiers)
                changed += SetStats(Load<SoldierDefinition>(path), range, DefaultSpread);
            changed += SetStats(Load<HeroDefinition>(HeroArcher), HeroArcherRange, DefaultSpread);

            var marksman = new SerializedObject(Load<BossDefinition>(Marksman));
            changed += SetFloat(marksman, "attackRange", MarksmanRange) + SetFloat(marksman, "aimSpread", MarksmanSpread);
            Apply(marksman);
            // 원거리 프로필의 화살 중력(projectileGravity)은 새 필드라 기존 에셋도 기본값 9.81로 읽힌다. 따로 쓰지 않는다.
            AssetDatabase.SaveAssets();
            return $"COMBAT-09 archer migration: {changed} values changed.";
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }

        // baseStats 목록에서 사거리와 탄퍼짐을 맞춘다. 탄퍼짐 항목이 없으면 끝에 더한다.
        static int SetStats(UnityEngine.Object definition, float range, float spread)
        {
            var data = new SerializedObject(definition);
            SerializedProperty stats = data.FindProperty("baseStats");
            int changed = SetStat(stats, StatType.AttackRange, range) + SetStat(stats, StatType.AimSpread, spread);
            Apply(data);
            return changed;
        }

        static int SetStat(SerializedProperty stats, StatType stat, float value)
        {
            for (int i = 0; i < stats.arraySize; i++)
            {
                SerializedProperty entry = stats.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("stat").intValue != (int)stat) continue;
                SerializedProperty field = entry.FindPropertyRelative("value");
                if (Mathf.Approximately(field.floatValue, value)) return 0;
                field.floatValue = value;
                return 1;
            }
            stats.InsertArrayElementAtIndex(stats.arraySize);
            SerializedProperty added = stats.GetArrayElementAtIndex(stats.arraySize - 1);
            added.FindPropertyRelative("stat").intValue = (int)stat;
            added.FindPropertyRelative("value").floatValue = value;
            return 1;
        }

        static int SetFloat(SerializedObject data, string name, float value)
        {
            SerializedProperty field = data.FindProperty(name);
            if (field == null) throw new InvalidOperationException($"Missing field {name} on {data.targetObject.name}");
            if (Mathf.Approximately(field.floatValue, value)) return 0;
            field.floatValue = value;
            return 1;
        }

        static void Apply(SerializedObject data)
        {
            if (!data.hasModifiedProperties) return;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data.targetObject);
        }
    }
}
