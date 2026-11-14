using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>PREP-01 강화 데이터만 연결한다. 기존 사용자 설정과 씬은 바꾸지 않는다.</summary>
    public static class EquipmentUpgradeSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/EquipmentUpgrades.asset";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        const string SmithyPath = "Assets/_Project/Data/GameFlow/Facilities/Facility_Smithy.asset";

        [MenuItem("ProjectNeo/Setup/Configure Equipment Upgrades")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring equipment upgrades.");
            RunConfig start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("Missing RunStart: " + StartPath);
            FacilityDefinition smithy = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(SmithyPath)
                ?? throw new InvalidOperationException("Missing Smithy: " + SmithyPath);
            var settings = AssetDatabase.LoadAssetAtPath<EquipmentUpgradeSettings>(SettingsPath);
            if (settings == null)
            {
                settings = EquipmentUpgradeSettings.Create(0.1f, 0.5f, 1, 3, 5);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var startData = new SerializedObject(start);
            SerializedProperty reference = startData.FindProperty("equipmentUpgrades");
            if (reference.objectReferenceValue == null)
            {
                reference.objectReferenceValue = settings;
                startData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(start);
            }
            else settings = (EquipmentUpgradeSettings)reference.objectReferenceValue;

            int maxLevel = 0;
            for (int i = 0; i < smithy.Levels.Count; i++) maxLevel = Math.Max(maxLevel, settings.GetMaxLevel(i));
            var visited = new HashSet<ItemDefinition>();
            foreach (FacilityLevel level in smithy.Levels)
            {
                if (level.recipes == null) continue;
                foreach (CraftingRecipe recipe in level.recipes)
                {
                    ItemDefinition item = recipe.item;
                    if (item == null || item.Kind != ItemKind.Equipment || !visited.Add(item) || item.UpgradeMaterials.Count > 0) continue;
                    var data = new SerializedObject(item);
                    SerializedProperty steps = data.FindProperty("upgradeMaterials");
                    steps.arraySize = maxLevel;
                    for (int target = 1; target <= maxLevel; target++)
                    {
                        SerializedProperty materials = steps.GetArrayElementAtIndex(target - 1).FindPropertyRelative("materials");
                        materials.ClearArray();
                        if (recipe.cost.materials == null) continue;
                        foreach (MaterialAmount amount in recipe.cost.materials)
                        {
                            if (amount.material == null || amount.amount <= 0) continue;
                            materials.InsertArrayElementAtIndex(materials.arraySize);
                            SerializedProperty entry = materials.GetArrayElementAtIndex(materials.arraySize - 1);
                            entry.FindPropertyRelative("material").objectReferenceValue = amount.material;
                            entry.FindPropertyRelative("amount").intValue = checked(amount.amount * target);
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
