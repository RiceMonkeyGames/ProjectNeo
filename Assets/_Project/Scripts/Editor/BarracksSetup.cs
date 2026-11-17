using System;
using ProjectNeo.Characters;
using ProjectNeo.Facilities;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-4 병영 데이터(Q-37, 2026-11-13 결정). 훈련장 시설의 모든 레벨에서 근접·원거리 잡졸을 모집할 수 있게 하고,
    /// 레벨 2·3에 체력·공격력 +10%·+20% 보너스를 넣으며, 잡졸 모집 비용(근접 15·원거리 20골드)과 표시 이름을 정한다.
    /// 여러 번 실행해도 같은 값으로 덮어쓴다. 값은 시작점이며 이후에는 에셋 인스펙터에서 조정한다.
    /// </summary>
    public static class BarracksSetup
    {
        const string FacilityPath = "Assets/_Project/Data/GameFlow/Facilities/Facility_TrainingGround.asset";
        const string MeleePath = "Assets/_Project/Data/UnitBattlePrototype/allied_melee.asset";
        const string RangedPath = "Assets/_Project/Data/UnitBattlePrototype/allied_ranged.asset";
        static readonly float[] BonusPerLevel = { 0f, 0.1f, 0.2f };

        [MenuItem("ProjectNeo/Setup/Configure Barracks Data")]
        public static void Apply()
        {
            var melee = Load<SoldierDefinition>(MeleePath);
            var ranged = Load<SoldierDefinition>(RangedPath);
            SetSoldier(melee, "Melee soldier", 15);
            SetSoldier(ranged, "Ranged soldier", 20);

            var facility = Load<FacilityDefinition>(FacilityPath);
            var data = new SerializedObject(facility);
            SerializedProperty levels = data.FindProperty("levels");
            if (levels.arraySize != BonusPerLevel.Length)
                throw new InvalidOperationException($"훈련장 레벨 수가 {BonusPerLevel.Length}이어야 한다: {levels.arraySize}");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty recruits = level.FindPropertyRelative("recruitableSoldiers");
                recruits.ClearArray();
                foreach (SoldierDefinition soldier in new[] { melee, ranged })
                {
                    recruits.InsertArrayElementAtIndex(recruits.arraySize);
                    recruits.GetArrayElementAtIndex(recruits.arraySize - 1).objectReferenceValue = soldier;
                }
                SerializedProperty modifiers = level.FindPropertyRelative("soldierModifiers");
                modifiers.ClearArray();
                if (BonusPerLevel[i] <= 0f) continue;
                foreach (StatType stat in new[] { StatType.MaxHealth, StatType.Attack })
                {
                    modifiers.InsertArrayElementAtIndex(modifiers.arraySize);
                    SerializedProperty modifier = modifiers.GetArrayElementAtIndex(modifiers.arraySize - 1);
                    modifier.FindPropertyRelative("stat").intValue = (int)stat;
                    modifier.FindPropertyRelative("kind").intValue = (int)ModifierKind.Percent;
                    modifier.FindPropertyRelative("value").floatValue = BonusPerLevel[i];
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(facility);
            AssetDatabase.SaveAssets();
        }

        static void SetSoldier(SoldierDefinition soldier, string displayName, int gold)
        {
            var data = new SerializedObject(soldier);
            data.FindProperty("displayName").stringValue = displayName;
            SerializedProperty cost = data.FindProperty("recruitCost");
            cost.FindPropertyRelative("gold").intValue = gold;
            cost.FindPropertyRelative("materials").ClearArray();
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soldier);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("에셋이 없다: " + path);
    }
}
