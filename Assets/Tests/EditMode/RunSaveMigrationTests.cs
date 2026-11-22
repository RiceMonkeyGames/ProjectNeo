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
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>META-10: 바뀌거나 없어진 콘텐츠 ID가 든 런·메타 저장을 이름 바꾸기 표와 전액 환불로 고친다.</summary>
    public class RunSaveMigrationTests
    {
        readonly List<Object> created = new List<Object>();
        SquadConfig squadConfig;
        HeroDefinition playerHero, knight;
        SoldierDefinition melee;
        ItemDefinition sword, shield;
        MaterialDefinition hide;
        FacilityDefinition shop, tavern, smithy;
        BuildPieceDefinition wall, gate;
        SiegeWeaponDefinition cannon;
        EconomyConfig economy;
        EquipmentUpgradeSettings upgrades;
        BuildCostTable buildCosts;
        RunRestoreOptions options;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            squadConfig = Track(SquadConfig.Create(4, 10));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            knight = Track(HeroDefinition.Create("knight", 2, new Cost(120)));
            melee = Track(SoldierDefinition.Create("melee", new Cost(10)));
            hide = Track(MaterialDefinition.Create("hide"));
            var materials = new ItemUpgradeMaterials[3];
            for (int i = 0; i < materials.Length; i++) materials[i] = new ItemUpgradeMaterials(new MaterialAmount(hide, 1));
            sword = Track(ItemDefinition.CreateUpgradable("sword", new Cost(40), materials, true, StatModifier.Flat(StatType.Attack, 10f)));
            shield = Track(ItemDefinition.Create("shield", ItemKind.Equipment, new Cost(30, new MaterialAmount(hide, 2))));
            economy = Track(EconomyConfig.Create(0.5f));
            upgrades = Track(EquipmentUpgradeSettings.Create(0.1f, 0.5f, 1, 3, 5));
            shop = Track(FacilityDefinition.Create(FacilityType.Shop, new FacilityLevel
            {
                upgradeCost = new Cost(0), offerCount = 2,
                itemPool = new[] { sword, shield }.Select(item => new WeightedEntry<ItemDefinition>(item)).ToList(),
            }));
            tavern = Track(FacilityDefinition.Create(FacilityType.Tavern, new FacilityLevel
            {
                upgradeCost = new Cost(0), offerCount = 1,
                heroPool = new List<WeightedEntry<HeroDefinition>> { new WeightedEntry<HeroDefinition>(knight) },
            }));
            smithy = Track(FacilityDefinition.Create(FacilityType.Smithy,
                new FacilityLevel { upgradeCost = new Cost(0) }, new FacilityLevel { upgradeCost = new Cost(10) }));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "Wall", Vector2Int.one, 3f, 100f, null,
                new BuildMountPoint("top", Vector3.up)));
            wall.name = "Wall";
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "Gate", new Vector2Int(2, 1), 3f, 200f));
            gate.name = "Gate";
            cannon = Track(SiegeWeaponDefinition.Create("cannon", "Cannon", Vector3.one));
            buildCosts = Track(BuildCostTable.Create(new[] { new PieceGoldCost(wall, 25) }, new[] { new WeaponGoldCost(cannon, 80) }));
            options = new RunRestoreOptions { SquadConfig = squadConfig, Economy = economy, EquipmentUpgrades = upgrades };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        RunContentCatalog Content(IEnumerable<ItemDefinition> items = null, IEnumerable<HeroDefinition> heroes = null,
            IEnumerable<SoldierDefinition> soldiers = null, IEnumerable<BuildPieceDefinition> pieces = null,
            IEnumerable<SiegeWeaponDefinition> weapons = null, IEnumerable<MaterialDefinition> materials = null) =>
            Track(RunContentCatalog.Create(items ?? new[] { sword, shield }, materials ?? new[] { hide },
                heroes ?? new[] { playerHero, knight }, soldiers ?? new[] { melee }, new[] { shop, tavern, smithy },
                pieces ?? new[] { wall, gate }, weapons ?? new[] { cannon }));

        // 영웅 대장 한 명과 잡졸 둘, 강화한 착용 검, 보관함 방패, 상점·주점 매물, 산 성벽과 그 위 대포, 처음부터 있던 성문.
        RunState Run()
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, 1000,
                new[] { new MaterialAmount(hide, 50) }, new[] { shop, tavern, smithy }))
                .WithEconomy(economy).WithEquipmentUpgrades(upgrades);
            RunState run = RunState.CreateNew(config);
            Squad<RunUnit> squad = run.Army.TryAddSquad();
            run.TryAssign(run.AddSoldier(melee), squad);
            run.TryAssign(run.AddSoldier(melee), squad);
            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            RunHero captain = run.AddHero(knight);
            Assert.IsTrue(run.TrySetLeader(squad, captain));
            ItemInstance blade = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryUpgradeEquipment(blade));
            Assert.IsTrue(run.TryEquip(captain, blade));
            run.Inventory.Add(shield);
            Assert.IsTrue(run.RestockShopIfNeeded());
            Assert.IsTrue(run.RestockTavernIfNeeded());
            run.SetCastle(CastleState.FromRecords(new[]
            {
                new CastlePieceState(gate, new Vector2Int(4, 9), 0, 0, true, 0f),
                new CastlePieceState(wall, new Vector2Int(2, 9), 1, 0, false, 64f),
            }, new[] { new CastleWeaponState(cannon, 1, "top", 3, 40f) }));
            return run;
        }

        static RunSnapshot RoundTrip(RunSnapshot snapshot) => JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snapshot));

        [Test]
        public void Capture_RecordsTheCurrentValueOfEverythingBought()
        {
            RunSnapshot saved = RoundTrip(Run().Capture(1, Content(), buildCosts));
            HeroRecord captain = saved.heroes.Single(hero => hero.id == "knight");
            Assert.AreEqual(120, captain.value.gold);
            Assert.IsTrue(saved.heroes.Single(hero => hero.isPlayer).value.IsEmpty, "the player was never hired");
            Assert.AreEqual(40, captain.equipment.Single().value.gold);
            ItemRecord stashed = saved.stash.Single(item => item.id == "shield");
            Assert.AreEqual(30, stashed.value.gold);
            Assert.AreEqual(2, stashed.value.materials.Single(material => material.id == "hide").amount);
            Assert.AreEqual(10, saved.squads[0].soldiers[0].value.gold);
            Assert.AreEqual(0, saved.castle.pieces[0].value.gold, "the starting gate was not bought");
            Assert.AreEqual(25, saved.castle.pieces[1].value.gold);
            Assert.AreEqual(80, saved.castle.weapons[0].value.gold);
        }

        [Test]
        public void Renamed_Content_IsReadUnderItsNewId()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            // 옛 버전에서 저장한 것처럼 ID를 옛 이름으로 바꾼다.
            saved.heroes.Single(hero => hero.id == "knight").id = "old_knight";
            ItemRecord stashed = saved.stash[0];
            stashed.id = "old_" + stashed.id;
            saved.stash[0] = stashed;
            SoldierRecord soldier = saved.squads[0].soldiers[0];
            soldier.id = "grunt";
            saved.squads[0].soldiers[0] = soldier;
            ContentRenameTable renames = Track(ContentRenameTable.Create(
                new ContentRename(ContentKind.Hero, "old_knight", "knight"),
                new ContentRename(ContentKind.Item, "old_shield", "shield"),
                new ContentRename(ContentKind.Soldier, "grunt", "footman"),
                new ContentRename(ContentKind.Soldier, "footman", "melee")));
            int gold = saved.gold;
            RunContentCatalog content = Content();
            var log = new List<string>();

            Assert.Greater(RunSaveMigration.Migrate(saved, content, renames, playerHero, log), 0);
            Assert.AreEqual(gold, saved.gold, "a rename refunds nothing");
            Assert.IsTrue(RunState.TryValidate(saved, content, squadConfig, out string error), error);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out error), error);
            Assert.IsTrue(log.Any(line => line.Contains("'grunt' -> 'melee'")), "renames follow a chain to the end");
        }

        [Test]
        public void RemovedItem_IsDropped_AndRefundsPriceMaterialsAndUpgradeGold()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            long upgradeGold = saved.heroes.Single(hero => hero.id == "knight").equipment.Single().upgradeGoldSpent;
            Assert.Greater(upgradeGold, 0);
            int gold = saved.gold;
            int hides = saved.materials.Single(material => material.id == "hide").amount;
            RunContentCatalog content = Content(items: new ItemDefinition[0]);

            RunSaveMigration.Migrate(saved, content, null, playerHero);

            Assert.AreEqual(gold + 40 + upgradeGold + 30, saved.gold, "sword price + upgrade gold + shield price");
            Assert.AreEqual(hides + 2, saved.materials.Single(material => material.id == "hide").amount, "the shield's hide comes back");
            Assert.IsEmpty(saved.stash);
            Assert.IsEmpty(saved.heroes.Single(hero => hero.id == "knight").equipment);
            Assert.IsEmpty(saved.shop.stock, "shop offers with a removed item are restocked");
            Assert.AreEqual(-1, saved.shop.stockedFor);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void RemovedHero_RefundsHireCost_MovesGearToStash_AndFreesTheSquad()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            int gold = saved.gold;
            RunContentCatalog content = Content(heroes: new[] { playerHero });

            RunSaveMigration.Migrate(saved, content, null, playerHero);

            Assert.AreEqual(gold + 120, saved.gold);
            Assert.IsFalse(saved.heroes.Any(hero => hero.id == "knight"));
            Assert.IsTrue(saved.stash.Any(item => item.id == "sword"), "the captain's sword is kept");
            Assert.AreEqual(0, saved.squads[0].leaderUnitId);
            Assert.IsEmpty(saved.tavern.offers, "tavern offers with a removed hero are restocked");
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.IsTrue(restored.Inventory.Items.Any(item => item.Definition == sword && item.UpgradeLevel > 0), "the upgrade stays on the sword");
        }

        [Test]
        public void RemovedSoldierType_RefundsRecruitCost()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            int gold = saved.gold;
            RunContentCatalog content = Content(soldiers: new SoldierDefinition[0]);

            RunSaveMigration.Migrate(saved, content, null, playerHero);

            Assert.AreEqual(gold + 20, saved.gold);
            Assert.IsEmpty(saved.squads[0].soldiers);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void RemovedCastlePiece_RefundsIt_AndTheWeaponOnIt()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            int gold = saved.gold;
            RunContentCatalog content = Content(pieces: new[] { gate });

            RunSaveMigration.Migrate(saved, content, null, playerHero);

            Assert.AreEqual(gold + 25 + 80, saved.gold);
            Assert.AreEqual(1, saved.castle.pieces.Count);
            Assert.IsEmpty(saved.castle.weapons);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void WeaponOnPieceThatLostItsMountPoint_IsRefunded_AndThePieceKept()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            int gold = saved.gold;
            // SIEGE-11: 성벽 조각에서 무기 설치 자리가 빠졌다.
            var data = new UnityEditor.SerializedObject(wall);
            data.FindProperty("mountPoints").ClearArray();
            data.ApplyModifiedPropertiesWithoutUndo();
            RunContentCatalog content = Content();
            var log = new List<string>();

            Assert.Greater(RunSaveMigration.Migrate(saved, content, null, playerHero, log), 0);

            Assert.AreEqual(gold + 80, saved.gold, "the weapon's recorded value comes back in full");
            Assert.AreEqual(2, saved.castle.pieces.Count, "the wall itself stays");
            Assert.IsEmpty(saved.castle.weapons);
            Assert.IsTrue(log.Any(line => line.Contains("no longer has mount point 'top'")), string.Join("\n", log));
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void RemovedPiece_BeforeAWeaponSupport_ReindexesTheWeapon()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            int gold = saved.gold;
            // 성문(0번)이 없어지면 대포가 얹힌 성벽은 1번에서 0번이 된다.
            RunContentCatalog content = Content(pieces: new[] { wall });

            RunSaveMigration.Migrate(saved, content, null, playerHero);

            Assert.AreEqual(gold, saved.gold, "the starting gate refunds nothing");
            Assert.AreEqual(0, saved.castle.weapons.Single().supportIndex);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void RemovedPlayerHero_IsReplacedByTheDefault_KeepingGrade()
        {
            HeroDefinition newPlayer = Track(HeroDefinition.Create("commander", true, 3, new StatValue(StatType.Attack, 20f)));
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            HeroRecord player = saved.heroes.Single(hero => hero.isPlayer);
            int grade = player.grade, gold = saved.gold;
            RunContentCatalog content = Content(heroes: new[] { newPlayer, knight });

            RunSaveMigration.Migrate(saved, content, null, newPlayer);

            Assert.AreEqual("commander", player.id);
            Assert.AreEqual(grade, player.grade);
            Assert.AreEqual(gold, saved.gold);
            Assert.IsNotNull(RunState.Restore(saved, content, options, out string error), error);
        }

        [Test]
        public void SaveWithoutValues_RemovedContent_RefundsNothing()
        {
            // META-10 전 저장: 값 칸이 없다.
            RunSnapshot saved = RoundTrip(Run().Capture(1, Content()));
            foreach (HeroRecord hero in saved.heroes) hero.value = new ValueRecord();
            for (int i = 0; i < saved.squads[0].soldiers.Count; i++)
            {
                SoldierRecord soldier = saved.squads[0].soldiers[i];
                soldier.value = new ValueRecord();
                saved.squads[0].soldiers[i] = soldier;
            }
            int gold = saved.gold;

            RunSaveMigration.Migrate(saved, Content(soldiers: new SoldierDefinition[0]), null, playerHero);

            Assert.AreEqual(gold, saved.gold);
            Assert.IsEmpty(saved.squads[0].soldiers);
        }

        [Test]
        public void NothingChanged_LeavesTheSaveAlone()
        {
            RunSnapshot saved = Run().Capture(1, Content(), buildCosts);
            string before = JsonUtility.ToJson(saved);
            Assert.AreEqual(0, RunSaveMigration.Migrate(saved, Content(), Track(ContentRenameTable.Create()), playerHero));
            Assert.AreEqual(before, JsonUtility.ToJson(saved));
        }

        [Test]
        public void RenameTable_Validate_FindsDuplicatesAndLoops()
        {
            ContentRenameTable table = Track(ContentRenameTable.Create(
                new ContentRename(ContentKind.Item, "a", "b"),
                new ContentRename(ContentKind.Item, "b", "a"),
                new ContentRename(ContentKind.Hero, "x", "y"),
                new ContentRename(ContentKind.Hero, "x", "z"),
                new ContentRename(ContentKind.Perk, "", "p")));
            List<string> problems = table.Validate();
            Assert.IsTrue(problems.Any(problem => problem.Contains("renamed twice")));
            Assert.IsTrue(problems.Any(problem => problem.Contains("back to itself")));
            Assert.IsTrue(problems.Any(problem => problem.Contains("empty id")));
            Assert.AreEqual("a", table.Resolve(ContentKind.SiegeWeapon, "a"), "other kinds are untouched");
        }

        [Test]
        public void Unlocks_RecordSpentPoints_RenameEntries_AndRefundRemovedOnes()
        {
            UnlockCatalog before = Track(UnlockCatalog.Create(Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "bonus",
                UnlockEntry.Create("gold_a", 2, UnlockPayload.StartingGold(10)),
                UnlockEntry.Create("gold_b", 3, UnlockPayload.StartingGold(20))))));
            var progress = new MetaProgress { unlockPoints = 10 };
            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, before, "gold_a"));
            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, before, "gold_b"));
            Assert.AreEqual(5, progress.unlockPoints);
            Assert.AreEqual(3, progress.unlockSpent.Single(record => record.entryId == "gold_b").points);

            // 업데이트: gold_a는 이름이 바뀌고 gold_b는 없어졌다.
            UnlockCatalog after = Track(UnlockCatalog.Create(Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "bonus",
                UnlockEntry.Create("bonus_gold_a", 2, UnlockPayload.StartingGold(10))))));
            ContentRenameTable renames = Track(ContentRenameTable.Create(new ContentRename(ContentKind.UnlockEntry, "gold_a", "bonus_gold_a")));
            progress = JsonUtility.FromJson<MetaProgress>(JsonUtility.ToJson(progress));

            Assert.Greater(UnlockTracks.Migrate(progress, after, renames), 0);
            CollectionAssert.AreEqual(new[] { "bonus_gold_a" }, progress.openedEntryIds);
            Assert.AreEqual(8, progress.unlockPoints, "gold_b's 3 points come back");
            Assert.IsTrue(UnlockTracks.IsOpened(progress, after.FindEntry("bonus_gold_a")));
            Assert.AreEqual(0, UnlockTracks.Migrate(progress, after, renames), "a second pass changes nothing");
        }

        [Test]
        public void Unlocks_OldSave_BackfillsCurrentCost_ButLegacyImportsCostNothing()
        {
            UnlockCatalog catalog = Track(UnlockCatalog.Create(Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "heroes",
                UnlockEntry.Create("hero_knight", 2, UnlockPayload.Hero("knight")),
                UnlockEntry.Create("hero_paladin", 3, UnlockPayload.Hero("paladin"))))));
            // META-10 전 저장: 쓴 포인트 기록이 없다. 기사는 예전 가챠 해금에서 옮겨 온 것이다.
            var progress = new MetaProgress { openedEntryIds = { "hero_knight", "hero_paladin" }, unlockedIds = { "knight" } };

            UnlockTracks.Migrate(progress, catalog, null);

            Assert.AreEqual(0, progress.unlockSpent.Single(record => record.entryId == "hero_knight").points);
            Assert.AreEqual(3, progress.unlockSpent.Single(record => record.entryId == "hero_paladin").points);
        }
    }
}
