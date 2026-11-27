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
            // CONTENT-03 items must exist before the tracks name them.
            HelmetAccessorySetup.CreateItems();

            UnlockTrackDefinition heroes = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "영웅",
                HeroEntry("hero_swordsman", 0, true),
                HeroEntry("hero_archer", 0, true),
                HeroEntry("hero_knight", 2, false),
                HeroEntry("hero_paladin", 3, false)), Folder + "/Track_Heroes.asset");

            UnlockTrackDefinition recipes = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.EquipmentRecipes, "장비 제작법",
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_wood_shield", 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_iron_sword", 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_leather_armor", 0, true),
                // CONTENT-03 (user 2026-11-27): tier 1 open by default, tier 2 appended after Steel Sword.
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.LeatherCap, 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.TravelerCharm, 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.IronRing, 0, true),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_chain_armor", 1, false),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, "item_steel_sword", 2, false),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.IronHelm, 2, false),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.MarksmanClasp, 3, false),
                ItemEntry("recipe_", UnlockPayloadKind.EquipmentRecipe, HelmetAccessorySetup.WarBannerCharm, 3, false)), Folder + "/Track_EquipmentRecipes.asset");

            UnlockTrackDefinition shop = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.ShopItems, "상점 물품",
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_potion", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_wood_shield", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_iron_sword", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_leather_armor", 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.LeatherCap, 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.TravelerCharm, 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.IronRing, 0, true),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_chain_armor", 1, false),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, "item_steel_sword", 2, false),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.IronHelm, 2, false),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.MarksmanClasp, 3, false),
                ItemEntry("shop_", UnlockPayloadKind.ShopItem, HelmetAccessorySetup.WarBannerCharm, 3, false)), Folder + "/Track_ShopItems.asset");

            UnlockTrackDefinition bonuses = SaveOver(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "시작 보너스",
                UnlockEntry.Create("bonus_gold_50", 1, UnlockPayload.StartingGold(50), displayName: "시작 골드 +50"),
                UnlockEntry.Create("bonus_potions_2", 2, UnlockPayload.StartingItem("item_potion", 2), displayName: "시작 회복약 x2"),
                UnlockEntry.Create("bonus_grunts_2", 3, UnlockPayload.StartingGrunts(2), displayName: "시작 잡졸 +2"),
                UnlockEntry.Create("bonus_gold_100", 5, UnlockPayload.StartingGold(100), displayName: "시작 골드 +100")), Folder + "/Track_StartingBonuses.asset");

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
            // 목록을 덮어쓰면 이름 바꾸기 표 연결이 지워지므로 다시 잇는다(META-10).
            ContentRenameSetup.Link();
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
            // CopySerialized also copies the new instance's empty name; name the asset after its file again (CONTENT-03).
            EditorUtility.CopySerialized(value, existing);
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
