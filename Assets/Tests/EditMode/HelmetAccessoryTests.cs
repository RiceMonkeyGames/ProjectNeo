using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// CONTENT-03 helmet and accessory set: the project data (items, shop, smithy, drops, unlocks, save list) and the run rules
    /// that use it (slots, swap in place, stats, crafting, upgrading, shop rolls, save/restore).
    /// </summary>
    public class HelmetAccessoryTests
    {
        const string Rewards = "Assets/_Project/Data/GameFlow/Rewards/";
        const string Facilities = "Assets/_Project/Data/GameFlow/Facilities/";
        const string Cap = "item_leather_cap", Helm = "item_iron_helm", Charm = "item_traveler_charm",
            Ring = "item_iron_ring", Clasp = "item_marksman_clasp", Banner = "item_war_banner_charm";
        static readonly string[] Tier1 = { Cap, Charm, Ring };
        static readonly string[] Tier2 = { Helm, Clasp, Banner };

        readonly List<Object> created = new List<Object>();
        HeroDefinition hero;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            hero = Track(HeroDefinition.Create("player", equipmentSlots: 1, baseStats: new[]
            {
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 10f),
                new StatValue(StatType.Defense, 2f), new StatValue(StatType.MoveSpeed, 5f), new StatValue(StatType.AimSpread, 3f),
            }));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        static ItemDefinition Item(string id)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Rewards + id + ".asset");
            Assert.IsNotNull(item, "Run ProjectNeo/Setup/Create Helmet And Accessory Set (CONTENT-03): " + id);
            return item;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, path);
            return asset;
        }

        static float Value(ItemDefinition item, StatType stat, ModifierKind kind) =>
            item.EquipModifiers.Where(m => m.stat == stat && m.kind == kind).Sum(m => m.value);

        // ----- Project data -----

        [TestCase(Cap, EquipmentSlot.Helmet, 30)]
        [TestCase(Helm, EquipmentSlot.Helmet, 65)]
        [TestCase(Charm, EquipmentSlot.Accessory, 30)]
        [TestCase(Ring, EquipmentSlot.Accessory, 30)]
        [TestCase(Clasp, EquipmentSlot.Accessory, 70)]
        [TestCase(Banner, EquipmentSlot.Accessory, 75)]
        public void ProjectItems_AreEquipmentInTheDecidedSlotAndPrice_WithAnIcon(string id, EquipmentSlot slot, int gold)
        {
            ItemDefinition item = Item(id);
            Assert.AreEqual(id, item.Id);
            Assert.AreEqual(ItemKind.Equipment, item.Kind);
            Assert.AreEqual(slot, item.Slot);
            Assert.AreEqual(gold, item.Price.gold);
            Assert.IsNotEmpty(item.DisplayName);
            Assert.IsNotNull(item.Icon, id + " needs an icon");
        }

        [Test]
        public void ProjectItems_CarryTheDecidedModifiers()
        {
            Assert.AreEqual(15f, Value(Item(Cap), StatType.MaxHealth, ModifierKind.Flat));
            Assert.AreEqual(1f, Value(Item(Cap), StatType.Defense, ModifierKind.Flat));
            Assert.AreEqual(30f, Value(Item(Helm), StatType.MaxHealth, ModifierKind.Flat));
            Assert.AreEqual(3f, Value(Item(Helm), StatType.Defense, ModifierKind.Flat));
            Assert.AreEqual(0.1f, Value(Item(Charm), StatType.MoveSpeed, ModifierKind.Percent), 0.0001f);
            Assert.AreEqual(2f, Value(Item(Ring), StatType.Attack, ModifierKind.Flat));
            Assert.AreEqual(-1f, Value(Item(Clasp), StatType.AimSpread, ModifierKind.Flat));
            Assert.AreEqual(2f, Value(Item(Clasp), StatType.Attack, ModifierKind.Flat));
            Assert.AreEqual(4f, Value(Item(Banner), StatType.Attack, ModifierKind.Flat));
            Assert.AreEqual(3f, Value(Item(Banner), StatType.Defense, ModifierKind.Flat));
        }

        [Test]
        public void ProjectShop_OffersTier1FromLevel1_AndTier2FromLevel2()
        {
            var shop = Load<FacilityDefinition>(Facilities + "Facility_Shop.asset");
            Assert.GreaterOrEqual(shop.Levels.Count, 2);
            for (int level = 0; level < shop.Levels.Count; level++)
            {
                List<string> pool = shop.Levels[level].itemPool.Select(e => e.item.Id).ToList();
                foreach (string id in Tier1) CollectionAssert.Contains(pool, id, $"shop Lv{level + 1}");
                foreach (string id in Tier2)
                    if (level == 0) CollectionAssert.DoesNotContain(pool, id, "tier 2 waits for shop Lv2");
                    else CollectionAssert.Contains(pool, id, $"shop Lv{level + 1}");
            }
        }

        [Test]
        public void ProjectSmithy_HasTier1RecipesFromLevel1_Tier2FromLevel2_AndEveryItemUpgradesToTheTop()
        {
            var smithy = Load<FacilityDefinition>(Facilities + "Facility_Smithy.asset");
            var upgrades = Load<EquipmentUpgradeSettings>("Assets/_Project/Data/GameFlow/EquipmentUpgrades.asset");
            for (int level = 0; level < smithy.Levels.Count; level++)
            {
                List<string> recipes = smithy.Levels[level].recipes.Select(r => r.item.Id).ToList();
                foreach (string id in Tier1) CollectionAssert.Contains(recipes, id, $"smithy Lv{level + 1}");
                foreach (string id in Tier2)
                    if (level == 0) CollectionAssert.DoesNotContain(recipes, id, "tier 2 waits for smithy Lv2");
                    else CollectionAssert.Contains(recipes, id, $"smithy Lv{level + 1}");
            }
            int top = upgrades.GetMaxLevel(smithy.Levels.Count - 1);
            foreach (string id in Tier1.Concat(Tier2))
                Assert.GreaterOrEqual(Item(id).UpgradeMaterials.Count, top, id + " must list materials up to +" + top);
        }

        [Test]
        public void ProjectDrops_IncludeTier1Only()
        {
            var table = Load<StageRewardTable>(Rewards + "StageRewards.asset");
            List<string> pool = table.ItemPool.Select(e => e.item.Id).ToList();
            foreach (string id in Tier1) CollectionAssert.Contains(pool, id);
            foreach (string id in Tier2) CollectionAssert.DoesNotContain(pool, id);
        }

        [TestCase(UnlockPayloadKind.ShopItem)]
        [TestCase(UnlockPayloadKind.EquipmentRecipe)]
        public void ProjectUnlocks_OpenTier1ByDefault_AndLockTier2OnAFreshProfile(UnlockPayloadKind kind)
        {
            var catalog = Load<UnlockCatalog>("Assets/_Project/Data/GameFlow/Unlocks/UnlockCatalog.asset");
            CollectionAssert.IsEmpty(catalog.Validate());
            var fresh = new MetaProgress();
            foreach (string id in Tier1) Assert.IsTrue(UnlockTracks.IsContentAvailable(fresh, catalog, kind, id), id);
            foreach (string id in Tier2) Assert.IsFalse(UnlockTracks.IsContentAvailable(fresh, catalog, kind, id), id);
        }

        [Test]
        public void ProjectRunContent_FindsEveryNewItemById()
        {
            var content = Load<RunContentCatalog>("Assets/_Project/Data/GameFlow/RunContent.asset");
            CollectionAssert.IsEmpty(content.FindIdProblems());
            foreach (string id in Tier1.Concat(Tier2)) Assert.AreSame(Item(id), content.Item(id), id);
        }

        // ----- Run rules with the project items -----

        RunState NewRun(out RunHero player)
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            player = run.AddHero(hero, true);
            return run;
        }

        [Test]
        public void HelmetAndAccessory_GoIntoTheirOwnSlots_AndChangeStats()
        {
            RunState run = NewRun(out RunHero player);
            StatSheet stats = player.Character.Stats;
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Cap))));
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Ring))));
            Assert.AreEqual(2, player.Character.Equipment.Count, "helmet and accessory do not share a slot");
            Assert.AreEqual(Cap, player.Character.EquippedIn(EquipmentSlot.Helmet).Definition.Id);
            Assert.AreEqual(Ring, player.Character.EquippedIn(EquipmentSlot.Accessory).Definition.Id);
            Assert.AreEqual(115f, stats.GetValue(StatType.MaxHealth));
            Assert.AreEqual(3f, stats.GetValue(StatType.Defense));
            Assert.AreEqual(12f, stats.GetValue(StatType.Attack));
        }

        [Test]
        public void EquippingAnotherHelmet_SwapsInPlace_AndReplacesTheStats()
        {
            RunState run = NewRun(out RunHero player);
            ItemInstance cap = run.Inventory.Add(Item(Cap));
            Assert.IsTrue(run.TryEquip(player, cap));
            ItemInstance before = run.Inventory.Add(Item(Ring));
            ItemInstance helm = run.Inventory.Add(Item(Helm));
            ItemInstance after = run.Inventory.Add(Item(Charm));

            Assert.IsTrue(run.TryEquip(player, helm));
            CollectionAssert.AreEqual(new[] { before, cap, after }, run.Inventory.Items, "the old helmet takes the new one's place");
            Assert.AreEqual(130f, player.Character.Stats.GetValue(StatType.MaxHealth));
            Assert.AreEqual(5f, player.Character.Stats.GetValue(StatType.Defense));
        }

        [Test]
        public void Accessories_ChangeMoveSpeedAndSpread()
        {
            RunState run = NewRun(out RunHero player);
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Charm))));
            Assert.AreEqual(5.5f, player.Character.Stats.GetValue(StatType.MoveSpeed), 0.0001f);
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Clasp))));
            Assert.AreEqual(2f, player.Character.Stats.GetValue(StatType.AimSpread), 0.0001f, "the clasp swaps out the charm");
            Assert.AreEqual(5f, player.Character.Stats.GetValue(StatType.MoveSpeed), 0.0001f);
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Banner))));
            Assert.AreEqual(14f, player.Character.Stats.GetValue(StatType.Attack));
            Assert.AreEqual(5f, player.Character.Stats.GetValue(StatType.Defense));
        }

        [Test]
        public void ProjectShopRolls_NeverOfferLockedTier2_AndDoOfferTier1()
        {
            var shop = Load<FacilityDefinition>(Facilities + "Facility_Shop.asset");
            var catalog = Load<UnlockCatalog>("Assets/_Project/Data/GameFlow/Unlocks/UnlockCatalog.asset");
            var fresh = new MetaProgress();
            System.Func<ItemDefinition, bool> available =
                item => UnlockTracks.IsContentAvailable(fresh, catalog, UnlockPayloadKind.ShopItem, item.Id);
            var seen = new HashSet<string>();
            var itemShop = new ItemShop(Track(EconomyConfig.Create(0.5f)));
            for (int seed = 0; seed < 200; seed++)
            {
                FacilityLevel level = shop.Levels[1];
                itemShop.Restock(level.itemPool, level.offerCount, new SeededRandom(seed), available);
                foreach (ItemDefinition item in itemShop.Stock) seen.Add(item.Id);
            }
            foreach (string id in Tier1) CollectionAssert.Contains(seen, id);
            foreach (string id in Tier2) CollectionAssert.DoesNotContain(seen, id, "locked on a fresh profile");
        }

        [Test]
        public void ProjectSmithy_CraftsTier1AtLevel1_Tier2AfterUpgrading_AndUpgradesTheHelmet()
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            run.AddFacility(Load<FacilityDefinition>(Facilities + "Facility_Smithy.asset"));
            run.SetupShop(Track(EconomyConfig.Create(0.5f)));
            run.SetupEquipmentUpgrades(Load<EquipmentUpgradeSettings>("Assets/_Project/Data/GameFlow/EquipmentUpgrades.asset"));
            run.Wallet.AddGold(2000);
            foreach (string material in new[] { "mat_hide", "mat_bone", "mat_feather" })
                run.Wallet.AddMaterial(Load<MaterialDefinition>(Rewards + material + ".asset"), 100);

            Assert.AreEqual(CraftCheck.NotCraftable, run.CheckCraft(Item(Helm)), "tier 2 waits for smithy Lv2");
            ItemInstance cap = run.TryCraft(Item(Cap));
            Assert.IsNotNull(cap);
            Assert.IsNotNull(run.TryCraft(Item(Ring)));
            Assert.IsNotNull(run.TryCraft(Item(Charm)));

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            Assert.IsNotNull(run.TryCraft(Item(Helm)));
            Assert.IsNotNull(run.TryCraft(Item(Clasp)));
            Assert.IsNotNull(run.TryCraft(Item(Banner)));

            Assert.IsTrue(run.TryUpgradeEquipment(cap));
            Assert.AreEqual(1, cap.UpgradeLevel);
            float health = cap.EquipModifiers.Where(m => m.stat == StatType.MaxHealth).Sum(m => m.value);
            Assert.Greater(health, 15f, "an upgraded helmet gives more max HP");
        }

        [Test]
        public void CaptureThenRestore_KeepsEquippedHelmetAndAccessory()
        {
            SquadConfig squads = Track(SquadConfig.Create(4, 10));
            ItemDefinition[] items = Tier1.Concat(Tier2).Select(Item).ToArray();
            var content = Track(RunContentCatalog.Create(items, new MaterialDefinition[0], new[] { hero },
                new SoldierDefinition[0], new FacilityDefinition[0], new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            var run = new RunState(squads);
            RunHero player = run.AddHero(hero, true);
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Helm))));
            Assert.IsTrue(run.TryEquip(player, run.Inventory.Add(Item(Clasp))));
            run.Inventory.Add(Item(Cap));

            RunSnapshot saved = run.Capture(1, content);
            RunState restored = RunState.Restore(saved, content, new RunRestoreOptions { SquadConfig = squads }, out string error);
            Assert.IsNotNull(restored, error);
            Character character = restored.Player.Character;
            Assert.AreEqual(Helm, character.EquippedIn(EquipmentSlot.Helmet).Definition.Id);
            Assert.AreEqual(Clasp, character.EquippedIn(EquipmentSlot.Accessory).Definition.Id);
            Assert.AreEqual(130f, character.Stats.GetValue(StatType.MaxHealth));
            Assert.AreEqual(2f, character.Stats.GetValue(StatType.AimSpread), 0.0001f);
            Assert.AreEqual(Cap, restored.Inventory.Items.Single().Definition.Id);
        }
    }
}
