using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// META-07 첫 해금 트랙(사용자 승인 2026-11-19)을 에셋으로 만들고 RunStart 설정에 연결한다.
    /// 다시 실행하면 트랙 내용을 이 코드의 값으로 덮어쓴다. 씬 파일은 바꾸지 않는다.
    /// </summary>
    public static class UnlockCatalogSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Unlocks";
        const string Heroes = "Assets/_Project/Data/GameFlow/Heroes/";
        const string Items = "Assets/_Project/Data/GameFlow/Rewards/";
        public const string CatalogPath = Folder + "/UnlockCatalog.asset";
        const string RunStartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";

        [MenuItem("ProjectNeo/Setup/Create Unlock Tracks (META-07)")]
        public static void Apply()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);

            UnlockTrackDefinition heroes = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "Heroes",
                HeroEntry("hero_swordsman", 0, true),
                HeroEntry("hero_archer", 0, true),
                HeroEntry("hero_knight", 2, false),
                HeroEntry("hero_paladin", 3, false)), Folder + "/Track_Heroes.asset");

            UnlockTrackDefinition recipes = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.EquipmentRecipes, "Equipment recipes",
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_wood_shield", 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_iron_sword", 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_leather_armor", 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_chain_armor", 1, false),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_steel_sword", 2, false)), Folder + "/Track_EquipmentRecipes.asset");

            UnlockTrackDefinition shop = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.ShopItems, "Shop items",
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_potion", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_wood_shield", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_iron_sword", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_leather_armor", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_chain_armor", 1, false),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_steel_sword", 2, false)), Folder + "/Track_ShopItems.asset");

            UnlockTrackDefinition bonuses = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "Starting bonuses",
                UnlockEntry.Create("bonus_gold_50", 1, UnlockPayload.StartingGold(50), displayName: "Starting gold +50"),
                UnlockEntry.Create("bonus_potions_2", 2, UnlockPayload.StartingItem("item_potion", 2), displayName: "Starting potions x2"),
                UnlockEntry.Create("bonus_grunts_2", 3, UnlockPayload.StartingGrunts(2), displayName: "Starting grunts +2"),
                UnlockEntry.Create("bonus_gold_100", 5, UnlockPayload.StartingGold(100), displayName: "Starting gold +100")), Folder + "/Track_StartingBonuses.asset");

            UnlockCatalog catalog = SaveOver(UnlockCatalog.Create(heroes, recipes, shop, bonuses), CatalogPath);
            foreach (string problem in catalog.Validate()) Debug.LogError("Unlock catalog: " + problem, catalog);

            var runStart = AssetDatabase.LoadAssetAtPath<RunConfig>(RunStartPath);
            if (runStart == null) throw new System.InvalidOperationException("Run start config is missing: " + RunStartPath);
            var data = new SerializedObject(runStart);
            SerializedProperty field = data.FindProperty("unlockCatalog");
            if (field.objectReferenceValue != catalog)
            {
                field.objectReferenceValue = catalog;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(runStart);
            }
            AssetDatabase.SaveAssets();
        }

        static UnlockEntry HeroEntry(string heroId, int cost, bool openedByDefault)
        {
            HeroDefinition hero = Load<HeroDefinition>(Heroes + heroId + ".asset");
            return UnlockEntry.Create(heroId, cost, UnlockPayload.Hero(hero.Id), openedByDefault, displayName: hero.DisplayName);
        }

        static UnlockEntry ItemEntry(string prefix, UnlockPayloadKind kind, string itemId, int cost, bool openedByDefault)
        {
            ItemDefinition item = Load<ItemDefinition>(Items + itemId + ".asset");
            string id = prefix + itemId.Replace("item_", string.Empty);
            return UnlockEntry.Create(id, cost, new UnlockPayload(kind, item.Id, 1), openedByDefault, displayName: item.DisplayName);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException("Asset is missing: " + path);
            return asset;
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
