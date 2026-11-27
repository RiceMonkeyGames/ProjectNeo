using System;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// CONTENT-03 helmet and accessory prototype set (user decision 2026-11-27): two helmets and four accessories, added to the
    /// shop pools, smithy recipes and the victory drop pool. Entries that are already there stay (inspector edits are kept),
    /// so running it again changes nothing. Unlock tracks, icons, upgrade materials and the run save list are refreshed by
    /// their own setups, which <see cref="Apply"/> runs afterwards.
    /// </summary>
    public static class HelmetAccessorySetup
    {
        const string ShopPath = "Assets/_Project/Data/GameFlow/Facilities/Facility_Shop.asset";
        const string SmithyPath = "Assets/_Project/Data/GameFlow/Facilities/Facility_Smithy.asset";
        const string RewardsPath = "Assets/_Project/Data/GameFlow/Rewards/StageRewards.asset";
        const string MaterialFolder = "Assets/_Project/Data/GameFlow/Rewards";

        public const string LeatherCap = "item_leather_cap";
        public const string IronHelm = "item_iron_helm";
        public const string TravelerCharm = "item_traveler_charm";
        public const string IronRing = "item_iron_ring";
        public const string MarksmanClasp = "item_marksman_clasp";
        public const string WarBannerCharm = "item_war_banner_charm";

        [MenuItem("ProjectNeo/Setup/Create Helmet And Accessory Set (CONTENT-03)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the helmet and accessory set.");
            CreateData();
            AssetDatabase.SaveAssets();
            UnlockCatalogSetup.Apply();
            EquipmentWindowLayout.AssignItemIcons();
            EquipmentUpgradeSetup.Apply();
            RunSaveSetup.Apply();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Creates the six items and adds them to the shop, smithy and drop pools. Scene files are not touched.</summary>
        public static void CreateData()
        {
            Items items = CreateItems();
            AddToShop(items);
            AddToSmithy(items);
            AddToDrops(items);
        }

        internal struct Items
        {
            public ItemDefinition cap, helm, charm, ring, clasp, banner;
        }

        /// <summary>Creates the item assets (or loads them) and fills empty slots and modifiers.</summary>
        internal static Items CreateItems()
        {
            // Prototype values (user 2026-11-27). Max HP is flat; a full-health unit refills to the new maximum on equip.
            var items = new Items
            {
                cap = Equipment(LeatherCap, "가죽 모자", 30, EquipmentSlot.Helmet,
                    StatModifier.Flat(StatType.MaxHealth, 15f), StatModifier.Flat(StatType.Defense, 1f)),
                helm = Equipment(IronHelm, "철투구", 65, EquipmentSlot.Helmet,
                    StatModifier.Flat(StatType.MaxHealth, 30f), StatModifier.Flat(StatType.Defense, 3f)),
                charm = Equipment(TravelerCharm, "여행자의 부적", 30, EquipmentSlot.Accessory,
                    StatModifier.Percent(StatType.MoveSpeed, 0.1f)),
                ring = Equipment(IronRing, "철반지", 30, EquipmentSlot.Accessory,
                    StatModifier.Flat(StatType.Attack, 2f)),
                clasp = Equipment(MarksmanClasp, "명사수의 걸쇠", 70, EquipmentSlot.Accessory,
                    StatModifier.Flat(StatType.AimSpread, -1f), StatModifier.Flat(StatType.Attack, 2f)),
                banner = Equipment(WarBannerCharm, "전투 깃발 부적", 75, EquipmentSlot.Accessory,
                    StatModifier.Flat(StatType.Attack, 4f), StatModifier.Flat(StatType.Defense, 3f)),
            };
            return items;
        }

        static ItemDefinition Equipment(string id, string name, int gold, EquipmentSlot slot, params StatModifier[] modifiers)
        {
            ItemDefinition item = GameRewardSetup.Item(id, name, ItemKind.Equipment, gold);
            if (item.Slot != slot)
            {
                var data = new SerializedObject(item);
                data.FindProperty("slot").enumValueIndex = (int)slot;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
            GameFacilitySetup.SetModifiers(item, modifiers);
            return item;
        }

        // Shop pools per shop level (tier 1 from Lv1, tier 2 from Lv2; weights follow the existing armor pattern).
        static void AddToShop(Items items)
        {
            var shop = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(ShopPath);
            if (shop == null) return;
            var pools = new[]
            {
                new[] { (items.cap, 1f), (items.charm, 1f), (items.ring, 1f) },
                new[] { (items.cap, 1f), (items.charm, 1f), (items.ring, 1f), (items.helm, 1f), (items.clasp, 0.5f), (items.banner, 0.5f) },
                new[] { (items.cap, 0.5f), (items.charm, 0.5f), (items.ring, 0.5f), (items.helm, 1.5f), (items.clasp, 1f), (items.banner, 1f) },
            };
            var data = new SerializedObject(shop);
            SerializedProperty levels = data.FindProperty("levels");
            for (int i = 0; i < levels.arraySize && i < pools.Length; i++)
            {
                SerializedProperty pool = levels.GetArrayElementAtIndex(i).FindPropertyRelative("itemPool");
                foreach ((ItemDefinition item, float weight) in pools[i]) AddWeighted(pool, item, weight);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
        }

        // Smithy recipes: tier 1 from Lv1, tier 2 from Lv2. Gold is below the shop price and materials make up the rest,
        // like the existing recipes. Upgrade materials follow these recipe materials (EquipmentUpgradeSetup).
        static void AddToSmithy(Items items)
        {
            var smithy = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(SmithyPath);
            if (smithy == null) return;
            MaterialDefinition hide = Material("mat_hide");
            MaterialDefinition bone = Material("mat_bone");
            MaterialDefinition feather = Material("mat_feather");
            var tier1 = new[]
            {
                new CraftingRecipe(items.cap, new Cost(20, new MaterialAmount(hide, 2))),
                new CraftingRecipe(items.charm, new Cost(20, new MaterialAmount(hide, 1), new MaterialAmount(feather, 1))),
                new CraftingRecipe(items.ring, new Cost(20, new MaterialAmount(bone, 2))),
            };
            var tier2 = new[]
            {
                new CraftingRecipe(items.helm, new Cost(45, new MaterialAmount(hide, 2), new MaterialAmount(bone, 3))),
                new CraftingRecipe(items.clasp, new Cost(50, new MaterialAmount(bone, 2), new MaterialAmount(feather, 3))),
                new CraftingRecipe(items.banner, new Cost(55, new MaterialAmount(hide, 3), new MaterialAmount(bone, 3))),
            };
            var data = new SerializedObject(smithy);
            SerializedProperty levels = data.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty recipes = levels.GetArrayElementAtIndex(i).FindPropertyRelative("recipes");
                foreach (CraftingRecipe recipe in tier1) AddRecipe(recipes, recipe);
                if (i >= 1) foreach (CraftingRecipe recipe in tier2) AddRecipe(recipes, recipe);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(smithy);
        }

        // Victory drops are tier 1 only, like the Iron Sword already there.
        static void AddToDrops(Items items)
        {
            var table = AssetDatabase.LoadAssetAtPath<StageRewardTable>(RewardsPath);
            if (table == null) return;
            var data = new SerializedObject(table);
            SerializedProperty pool = data.FindProperty("itemPool");
            AddWeighted(pool, items.cap, 0.5f);
            AddWeighted(pool, items.charm, 0.5f);
            AddWeighted(pool, items.ring, 0.5f);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
        }

        static MaterialDefinition Material(string id) =>
            AssetDatabase.LoadAssetAtPath<MaterialDefinition>($"{MaterialFolder}/{id}.asset")
            ?? throw new InvalidOperationException("Material is missing (run Connect Rewards To Game Scene first): " + id);

        static void AddWeighted(SerializedProperty pool, ItemDefinition item, float weight)
        {
            for (int i = 0; i < pool.arraySize; i++)
                if (pool.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) return;
            pool.InsertArrayElementAtIndex(pool.arraySize);
            SerializedProperty entry = pool.GetArrayElementAtIndex(pool.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("weight").floatValue = weight;
        }

        static void AddRecipe(SerializedProperty recipes, CraftingRecipe recipe)
        {
            for (int i = 0; i < recipes.arraySize; i++)
                if (recipes.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == recipe.item) return;
            recipes.InsertArrayElementAtIndex(recipes.arraySize);
            SerializedProperty entry = recipes.GetArrayElementAtIndex(recipes.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = recipe.item;
            SerializedProperty cost = entry.FindPropertyRelative("cost");
            cost.FindPropertyRelative("gold").intValue = recipe.cost.gold;
            SerializedProperty materials = cost.FindPropertyRelative("materials");
            materials.ClearArray();
            foreach (MaterialAmount amount in recipe.cost.materials)
            {
                materials.InsertArrayElementAtIndex(materials.arraySize);
                SerializedProperty material = materials.GetArrayElementAtIndex(materials.arraySize - 1);
                material.FindPropertyRelative("material").objectReferenceValue = amount.material;
                material.FindPropertyRelative("amount").intValue = amount.amount;
            }
        }
    }
}
