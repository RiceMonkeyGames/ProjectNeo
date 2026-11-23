using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>FLOW-02 런 저장: 기록 만들기·되살리기, 검사, 저장 파일 규칙, 세션의 이어 하기, ID 목록 에셋.</summary>
    public class RunSaveTests
    {
        readonly List<Object> created = new List<Object>();
        string directory;
        SquadConfig squadConfig;
        HeroDefinition playerHero, knight, archer;
        SoldierDefinition melee, ranged;
        ItemDefinition sword, potion, shield;
        MaterialDefinition hide;
        FacilityDefinition shop, tavern, smithy;
        BuildPieceDefinition wall, gate;
        SiegeWeaponDefinition cannon;
        EconomyConfig economy;
        EquipmentUpgradeSettings upgrades;
        RunContentCatalog content;
        RunRestoreOptions options;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ProjectNeoRunSaveTests_" + Guid.NewGuid().ToString("N"));
            GameSession.Reset();
            GameSession.UseSaveStore(new MetaSaveStore(directory));

            squadConfig = Track(SquadConfig.Create(4, 10));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            knight = Track(HeroDefinition.Create("knight", 2, new Cost(120)));
            archer = Track(HeroDefinition.Create("archer", 1, new Cost(60)));
            melee = Track(SoldierDefinition.Create("melee", new Cost(10)));
            ranged = Track(SoldierDefinition.Create("ranged", new Cost(15)));
            hide = Track(MaterialDefinition.Create("hide"));
            var materials = new ItemUpgradeMaterials[3];
            for (int i = 0; i < materials.Length; i++) materials[i] = new ItemUpgradeMaterials(new MaterialAmount(hide, 1));
            sword = Track(ItemDefinition.CreateUpgradable("sword", new Cost(40), materials, true, StatModifier.Flat(StatType.Attack, 10f)));
            shield = Track(ItemDefinition.Create("shield", ItemKind.Equipment, new Cost(30)));
            potion = Track(ItemDefinition.CreateConsumable("potion", new Cost(10), 0.35f));
            economy = Track(EconomyConfig.Create(0.5f));
            upgrades = Track(EquipmentUpgradeSettings.Create(0.1f, 0.5f, 1, 3, 5));
            shop = Track(FacilityDefinition.Create(FacilityType.Shop,
                ShopLevel(0, 2, sword, shield, potion), ShopLevel(50, 3, sword, shield, potion)));
            tavern = Track(FacilityDefinition.Create(FacilityType.Tavern, TavernLevel(0, 2, knight, archer)));
            smithy = Track(FacilityDefinition.Create(FacilityType.Smithy,
                new FacilityLevel { upgradeCost = new Cost(0) }, new FacilityLevel { upgradeCost = new Cost(10) }));
            wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "Wall", Vector2Int.one, 3f, 100f, null,
                new BuildMountPoint("top", Vector3.up)));
            wall.name = "Wall";
            gate = Track(BuildPieceDefinition.Create(BuildPieceKind.Gate, "Gate", new Vector2Int(2, 1), 3f, 200f));
            gate.name = "Gate";
            cannon = Track(SiegeWeaponDefinition.Create("cannon", "Cannon", Vector3.one));

            content = Track(RunContentCatalog.Create(new[] { sword, shield, potion }, new[] { hide },
                new[] { playerHero, knight, archer }, new[] { melee, ranged }, new[] { shop, tavern, smithy },
                new[] { wall, gate }, new[] { cannon }));
            options = new RunRestoreOptions { SquadConfig = squadConfig, Economy = economy, EquipmentUpgrades = upgrades };
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static FacilityLevel ShopLevel(int gold, int offers, params ItemDefinition[] items) => new FacilityLevel
        {
            upgradeCost = new Cost(gold), offerCount = offers,
            itemPool = items.Select(item => new WeightedEntry<ItemDefinition>(item)).ToList(),
        };

        static FacilityLevel TavernLevel(int gold, int offers, params HeroDefinition[] heroes) => new FacilityLevel
        {
            upgradeCost = new Cost(gold), offerCount = offers,
            heroPool = heroes.Select(hero => new WeightedEntry<HeroDefinition>(hero)).ToList(),
        };

        // 저장할 것이 골고루 들어 있는 런: 재화, 강화한 착용 장비, 고용한 대장, 두 부대, 사기, 시설 레벨, 상점·주점 매물, 성.
        RunState RichRun()
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, 1000,
                new[] { new MaterialAmount(hide, 50) }, new[] { shop, tavern, smithy }))
                .WithEconomy(economy).WithEquipmentUpgrades(upgrades);
            RunState run = RunState.CreateNew(config);
            Squad<RunUnit> first = run.Army.TryAddSquad(), second = run.Army.TryAddSquad();
            run.TryAssign(run.AddSoldier(melee), first);
            run.TryAssign(run.AddSoldier(ranged), first);
            run.TryAssign(run.AddSoldier(melee), second);
            first.Morale = new SquadMorale(200f, 95f);

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            ItemInstance blade = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryUpgradeEquipment(blade));
            Assert.IsTrue(run.TryEquip(run.Player, blade));
            run.Inventory.Add(potion);
            run.Inventory.Add(shield);

            Assert.IsTrue(run.RestockShopIfNeeded());
            Assert.IsTrue(run.TryToggleShopLock(0));
            Assert.IsTrue(run.RestockTavernIfNeeded());
            HeroDefinition offered = run.TavernOffers[0];
            Assert.IsNotNull(run.TryHire(offered, second));

            run.SetCastle(CastleState.FromRecords(new[]
            {
                new CastlePieceState(gate, new Vector2Int(4, 9), 0, 0, true, 0f),
                new CastlePieceState(wall, new Vector2Int(2, 9), 1, 0, false, 64f),
                new CastlePieceState(wall, new Vector2Int(2, 9), 1, 1, false, 100f),
            }, new[] { new CastleWeaponState(cannon, 2, "top", 3, 40f) }));
            return run;
        }

        static string Json(RunSnapshot snapshot) => JsonUtility.ToJson(snapshot, true);

        [Test]
        public void CaptureThenRestore_KeepsTheAscensionLevel()
        {
            // FLOW-03: 고른 승천 단계는 런 저장에 남고, 이어 하면 같은 규칙이 다시 붙는다.
            AscensionTable table = Track(AscensionTable.Create(0.1f,
                new AscensionLevel("hp", new AscensionRule(AscensionRuleKind.EnemyHealthPercent, 0.15f)),
                new AscensionLevel("gold", new AscensionRule(AscensionRuleKind.StartingGoldPercent, -0.25f))));
            RunState run = RichRun();
            run.SetupAscension(table, 2);
            RunSnapshot saved = run.Capture(3, content);
            Assert.AreEqual(2, saved.ascensionLevel);

            options.Ascension = table;
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(2, restored.AscensionLevel);
            Assert.AreEqual(0.15f, restored.Ascension.Total(AscensionRuleKind.EnemyHealthPercent), 0.0001f);

            // 예전 저장(필드 없음)은 기본 게임으로 읽힌다.
            string withoutLevel = System.Text.RegularExpressions.Regex.Replace(Json(saved), @",\s*""ascensionLevel"":\s*2", string.Empty);
            StringAssert.DoesNotContain("ascensionLevel", withoutLevel);
            RunSnapshot old = JsonUtility.FromJson<RunSnapshot>(withoutLevel);
            Assert.AreEqual(0, old.ascensionLevel);
        }

        [Test]
        public void CaptureThenRestore_ReproducesTheWholeRun()
        {
            RunState run = RichRun();
            RunSnapshot saved = run.Capture(3, content);

            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(Json(saved), Json(restored.Capture(3, content)), "a restored run must save to the same record");

            Assert.AreEqual(run.Seed, restored.Seed);
            Assert.AreEqual(run.Wallet.Gold, restored.Wallet.Gold);
            Assert.AreEqual(run.Wallet.GetMaterial(hide), restored.Wallet.GetMaterial(hide));
            Assert.AreEqual(2, restored.Inventory.Items.Count, "equipped items are not in the stash");
            Assert.AreEqual(1, restored.Player.Character.Equipment.Count);
            Assert.AreEqual(1, restored.Player.Character.Equipment[0].UpgradeLevel);
            Assert.AreEqual(run.Player.Character.Stats.GetValue(StatType.Attack),
                restored.Player.Character.Stats.GetValue(StatType.Attack), 0.0001f, "upgraded equipment stats come back");
            Assert.AreEqual(run.Player.Id, restored.Player.Id);
            Assert.AreEqual(2, restored.Heroes.Count);
            Assert.AreEqual(2, restored.Army.Squads.Count);
            Assert.AreEqual(2, restored.Army.Squads[0].Members.Count);
            Assert.AreEqual(95f, restored.Army.Squads[0].Morale.Value, 0.0001f);
            Assert.IsNull(restored.Army.Squads[1].Morale);
            Assert.AreSame(restored.Heroes.First(hero => !hero.IsPlayer), restored.Army.Squads[1].Leader);
            Assert.AreEqual(1, restored.Smithy.Level);
            Assert.AreEqual(run.Shop.Stock.Count, restored.Shop.Stock.Count);
            Assert.IsTrue(restored.Shop.IsLocked(0));
            Assert.AreEqual(run.TavernOffers.Count, restored.TavernOffers.Count);
            Assert.AreEqual(3, restored.Castle.Pieces.Count);
            Assert.AreEqual(64f, restored.Castle.Pieces[1].Health, 0.0001f);
            Assert.AreEqual(0f, restored.Castle.Pieces[0].Health, 0.0001f, "a breached gate stays breached");
            Assert.AreEqual(3, restored.Castle.Weapons[0].Rotation);
            Assert.IsFalse(restored.HasPendingBattle);
        }

        [Test]
        public void CaptureThenRestore_KeepsEachHerosSkillAuto_AndOldSavesStayAuto()
        {
            RunState run = RichRun();
            RunHero hired = run.Heroes.First(hero => !hero.IsPlayer);
            Assert.IsTrue(hired.SkillAuto, "새 영웅은 자동 사용이 켜져 있다");
            hired.SkillAuto = false;

            string json = Json(run.Capture(1, content));
            RunState restored = RunState.Restore(JsonUtility.FromJson<RunSnapshot>(json), content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.IsFalse(((RunHero)restored.Find(hired.Id)).SkillAuto);
            Assert.IsTrue(restored.Player.SkillAuto);

            RunSnapshot old = JsonUtility.FromJson<RunSnapshot>(json.Replace("\"skillManual\": true", "\"unused\": true"));
            RunState fromOld = RunState.Restore(old, content, options, out error);
            Assert.IsNotNull(fromOld, error);
            Assert.IsTrue(((RunHero)fromOld.Find(hired.Id)).SkillAuto, "필드가 없는 예전 저장은 자동 켜짐");
        }

        [Test]
        public void OldSaveWithEquippedFallenHero_RestoresTheGearToTheStash()
        {
            // PREP-08 이전 저장: 죽은 영웅이 강화한 장비를 낀 채 기록돼 있다(Q-31 이전 동작).
            RunState run = RichRun();
            RunSnapshot saved = run.Capture(1, content);
            HeroRecord player = saved.heroes.First(hero => hero.isPlayer);
            saved.fallenHeroes.Add(new HeroRecord
            {
                unitId = 999, id = player.id, isPlayer = false, equipment = new List<ItemRecord>(player.equipment),
            });

            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(1, restored.FallenHeroes.Count);
            CollectionAssert.IsEmpty(restored.FallenHeroes[0].Character.Equipment, "the fallen hero no longer holds gear");
            Assert.AreEqual(3, restored.Inventory.Items.Count, "the fallen hero's gear joins the two stash items");
            Assert.IsTrue(restored.Inventory.Items.Any(item => item.Definition == sword && item.UpgradeLevel == 1),
                "returned gear keeps its upgrade level");
            Assert.AreEqual(1, restored.Player.Character.Equipment.Count, "living heroes keep their gear");
            CollectionAssert.IsEmpty(restored.Capture(1, content).fallenHeroes[0].equipment, "the next save records the move");
        }

        [Test]
        public void RestoredRun_ContinuesUnitNumbers_AndKeepsThisPreparationsShop()
        {
            RunState run = RichRun();
            int highest = run.Soldiers.Max(soldier => soldier.Id);
            RunState restored = RunState.Restore(run.Capture(1, content), content, options, out _);
            Assert.Greater(restored.AddSoldier(melee).Id, highest, "new units never reuse a saved number");
            Assert.IsFalse(restored.RestockShopIfNeeded(), "the shop already stocked for this preparation is not re-rolled");
            Assert.IsFalse(restored.RestockTavernIfNeeded());
        }

        [Test]
        public void RunSavedBeforeStocking_RollsTheSameShopAfterRestore()
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, 100, null, new[] { shop }))
                .WithEconomy(economy);
            RunState run = RunState.CreateNew(config);
            RunState restored = RunState.Restore(run.Capture(1, content), content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.IsTrue(run.RestockShopIfNeeded());
            Assert.IsTrue(restored.RestockShopIfNeeded());
            CollectionAssert.AreEqual(run.Shop.Stock, restored.Shop.Stock, "same seed and battle count give the same offers");
        }

        [Test]
        public void Restore_UsesTheGivenSceneCharacter_SoEquipmentReachesItsStats()
        {
            RunState run = RichRun();
            var sceneCharacter = new Character(playerHero);
            options.CharacterFor = (record, definition) => record.isPlayer ? sceneCharacter : null;
            RunState restored = RunState.Restore(run.Capture(1, content), content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreSame(sceneCharacter, restored.Player.Character);
            Assert.AreEqual(31f, sceneCharacter.Stats.GetValue(StatType.Attack), 0.0001f);

            options.CharacterFor = (record, definition) => new Character(knight);
            Assert.IsNull(RunState.Restore(run.Capture(1, content), content, options, out error));
            StringAssert.Contains("character does not match", error);
        }

        [Test]
        public void Capture_KeepsEmptySquadsAndStoredSoldiers_AndDoesNotChangeTheRun()
        {
            RunState run = RichRun();
            run.Army.TryAddSquad();
            RunSoldier stored = run.Soldiers[0];
            Assert.IsTrue(run.Unassign(stored));
            int changes = 0;
            run.Changed += () => changes++;
            RunSnapshot saved = run.Capture(2, content);
            Assert.AreEqual(3, saved.squads.Count, "empty squads are kept (UI-07)");
            Assert.AreEqual(3, run.Army.Squads.Count);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(2, saved.stageNumber);

            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(3, restored.Army.Squads.Count);
            Assert.AreEqual(1, restored.StoredSoldiers.Count);
            Assert.AreEqual(stored.Id, restored.StoredSoldiers[0].Id);
        }

        [Test]
        public void Validate_RejectsRecordsThatCannotBeRestored()
        {
            RunState run = RichRun();
            Func<RunSnapshot> fresh = () => run.Capture(1, content);
            Assert.IsTrue(RunState.TryValidate(fresh(), content, squadConfig, out _));

            RunSnapshot unknownItem = fresh();
            unknownItem.stash[0] = new ItemRecord { id = "removed_item" };
            Assert.IsFalse(RunState.TryValidate(unknownItem, content, squadConfig, out string error));
            StringAssert.Contains("removed_item", error);
            Assert.IsNull(RunState.Restore(unknownItem, content, options, out _));

            RunSnapshot twoPlayers = fresh();
            twoPlayers.heroes[1].isPlayer = true;
            Assert.IsFalse(RunState.TryValidate(twoPlayers, content, squadConfig, out _));

            RunSnapshot duplicateUnit = fresh();
            duplicateUnit.squads[1].soldiers[0] = duplicateUnit.squads[0].soldiers[0];
            Assert.IsFalse(RunState.TryValidate(duplicateUnit, content, squadConfig, out _));

            RunSnapshot tooManySquads = fresh();
            for (int i = 0; i < 5; i++) tooManySquads.squads.Add(new SquadRecord());
            Assert.IsFalse(RunState.TryValidate(tooManySquads, content, squadConfig, out _));

            RunSnapshot unknownPiece = fresh();
            unknownPiece.castle.pieces.Add(new CastlePieceRecord { id = "Tower" });
            Assert.IsFalse(RunState.TryValidate(unknownPiece, content, squadConfig, out _));

            RunSnapshot badSupport = fresh();
            badSupport.castle.weapons[0] = new CastleWeaponRecord { id = "cannon", supportIndex = 9 };
            Assert.IsFalse(RunState.TryValidate(badSupport, content, squadConfig, out _));

            Assert.IsFalse(RunState.TryValidate(null, content, squadConfig, out _));
        }

        [Test]
        public void Store_RoundTripsThroughAFile_AndKeepsABackup()
        {
            var store = new RunSaveStore(directory);
            Assert.AreEqual(RunSaveStatus.Empty, store.Inspect(0).Status);
            Assert.IsFalse(store.Inspect(0).HasRun);

            RunSnapshot saved = RichRun().Capture(2, content);
            Assert.IsTrue(store.Save(0, saved), store.LastError);
            RunLoadResult loaded = store.Load(0);
            Assert.AreEqual(RunSaveStatus.Loaded, loaded.Status);
            Assert.AreEqual(Json(saved), Json(loaded.Snapshot));
            Assert.IsNotNull(RunState.Restore(loaded.Snapshot, content, options, out string error), error);
            Assert.IsFalse(File.Exists(store.BackupPath(0)));
            Assert.AreEqual(RunSaveStatus.Empty, store.Inspect(1).Status, "slots are independent");

            saved.stageNumber = 3;
            Assert.IsTrue(store.Save(0, saved));
            Assert.IsTrue(File.Exists(store.BackupPath(0)));
            Assert.AreEqual(3, store.Inspect(0).Snapshot.stageNumber);
        }

        [Test]
        public void Store_RecoversFromBackup_AndSetsDamagedFilesAside()
        {
            var store = new RunSaveStore(directory);
            RunSnapshot saved = RichRun().Capture(2, content);
            store.Save(0, saved);
            saved.stageNumber = 3;
            store.Save(0, saved);

            File.WriteAllText(store.MainPath(0), "{ broken");
            Assert.AreEqual(RunSaveStatus.RestoredFromBackup, store.Inspect(0).Status);
            Assert.AreEqual("{ broken", File.ReadAllText(store.MainPath(0)), "Inspect has no side effects");
            RunLoadResult loaded = store.Load(0);
            Assert.AreEqual(2, loaded.Snapshot.stageNumber, "the backup is the previous save");
            Assert.AreEqual(RunSaveStatus.Loaded, store.Inspect(0).Status, "the main file was rebuilt from the backup");
            Assert.AreEqual(1, Directory.GetFiles(directory, "run_slot1.json.corrupt-*").Length);

            File.WriteAllText(store.MainPath(0), "x");
            File.WriteAllText(store.BackupPath(0), "y");
            loaded = store.Load(0);
            Assert.AreEqual(RunSaveStatus.Unreadable, loaded.Status);
            Assert.IsFalse(loaded.HasRun);
            Assert.AreEqual(RunSaveStatus.Empty, store.Inspect(0).Status);
        }

        [Test]
        public void Store_TreatsNewerVersionsAndEmptyRecordsAsUnreadable_AndCanDeleteOrDiscard()
        {
            var store = new RunSaveStore(directory);
            RunSnapshot saved = RichRun().Capture(1, content);
            Directory.CreateDirectory(directory);

            File.WriteAllText(store.MainPath(0), JsonUtility.ToJson(new RunSaveFile { version = RunSaveStore.CurrentVersion + 1, snapshot = saved }));
            Assert.AreEqual(RunSaveStatus.Unreadable, store.Inspect(0).Status);
            File.WriteAllText(store.MainPath(1), "{}");
            Assert.AreEqual(RunSaveStatus.Unreadable, store.Inspect(1).Status);

            Assert.IsTrue(store.Save(2, saved));
            Assert.IsTrue(store.Delete(2));
            Assert.AreEqual(RunSaveStatus.Empty, store.Inspect(2).Status);

            Assert.IsTrue(store.Save(2, saved));
            Assert.IsTrue(store.Discard(2));
            Assert.AreEqual(RunSaveStatus.Empty, store.Inspect(2).Status);
            Assert.AreEqual(1, Directory.GetFiles(directory, "run_slot3.json.corrupt-*").Length, "a discarded save is kept aside, not deleted");
        }

        [Test]
        public void Session_SavesOnlyWithASlot_AndContinueResumesTheSavedStage()
        {
            RunSnapshot saved = RichRun().Capture(2, content);
            Assert.IsFalse(GameSession.SaveRun(saved), "no slot selected: nothing is written");
            Assert.IsFalse(Directory.Exists(directory) && Directory.GetFiles(directory, "run_*").Length > 0);

            GameSession.SelectSlot(1);
            Assert.IsTrue(GameSession.SaveRun(saved));
            Assert.IsTrue(GameSession.HasRunSave(1));
            Assert.IsFalse(GameSession.HasRunSave(0));
            Assert.IsFalse(GameSession.HasRunSave(-1));

            GameSession.Reset();
            Assert.IsFalse(GameSession.ContinueRun(0), "an empty slot cannot be continued");
            Assert.AreEqual(GamePhase.Title, GameSession.Flow.Phase);
            Assert.IsTrue(GameSession.ContinueRun(1));
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(2, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(1, GameSession.ActiveSlot);
            Assert.IsNull(GameSession.Flow.Run.State, "the game scene attaches the restored run data");
            Assert.IsNotNull(GameSession.TakePendingRun());
            Assert.IsNull(GameSession.TakePendingRun(), "the pending record is handed over once");
            Assert.IsFalse(GameSession.ContinueRun(1), "only from the title");

            Assert.IsTrue(GameSession.DeleteRunSave());
            Assert.IsFalse(GameSession.HasRunSave(1));
        }

        [Test]
        public void Flow_ResumeFallsBackToANewRunAtStageOne()
        {
            var flow = new GameFlow();
            Assert.IsTrue(flow.ResumeRun(4));
            Assert.AreEqual(4, flow.Run.StageNumber);
            Assert.IsFalse(flow.ResumeRun(2));
            Assert.IsTrue(flow.RestartRunAtFirstStage());
            Assert.AreEqual(1, flow.Run.StageNumber);
            Assert.AreEqual(GamePhase.Preparation, flow.Phase);
            flow.AttachRunState(new RunState(squadConfig));
            Assert.IsFalse(flow.RestartRunAtFirstStage(), "a run that already has data is not replaced");
        }

        [Test]
        public void Policy_SwitchesEachSaveTrigger()
        {
            var policy = new RunSavePolicy();
            foreach (RunSaveTrigger trigger in Enum.GetValues(typeof(RunSaveTrigger))) Assert.IsTrue(policy.Allows(trigger), trigger.ToString());
            Assert.IsTrue(policy.deleteOnDefeat);
            policy.onBattleStart = false;
            Assert.IsFalse(policy.Allows(RunSaveTrigger.BattleStart));
            Assert.IsTrue(policy.Allows(RunSaveTrigger.PreparationStart));
        }

        [Test]
        public void Catalog_UsesExplicitPieceIds_AndReportsIdProblems()
        {
            Assert.AreEqual("Wall", content.PieceId(wall));
            Assert.AreSame(wall, content.Piece("Wall"));
            content.SetPieceIds(new[] { "guid-wall", "guid-gate" });
            Assert.AreEqual("guid-gate", content.PieceId(gate));
            Assert.AreSame(gate, content.Piece("guid-gate"));
            Assert.IsNull(content.Piece("Gate"));
            Assert.AreEqual(0, content.FindIdProblems().Count);

            ItemDefinition copy = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(1)));
            RunContentCatalog broken = Track(RunContentCatalog.Create(new[] { sword, copy }));
            Assert.AreEqual(1, broken.FindIdProblems().Count);
        }

        [Test]
        public void ProjectCatalogAsset_ListsEveryDefinition_SoSavesCanAlwaysBeRestored()
        {
            const string path = "Assets/_Project/Data/GameFlow/RunContent.asset";
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RunContentCatalog>(path);
            Assert.IsNotNull(catalog, "Run ProjectNeo/Setup/Refresh Run Save Content (FLOW-02).");
            CollectionAssert.IsEmpty(catalog.FindIdProblems());
            AssertListed(catalog.Items, "ItemDefinition");
            AssertListed(catalog.Materials, "MaterialDefinition");
            AssertListed(catalog.Heroes, "HeroDefinition");
            AssertListed(catalog.Soldiers, "SoldierDefinition");
            AssertListed(catalog.Pieces, "BuildPieceDefinition");
            AssertListed(catalog.Weapons, "SiegeWeaponDefinition");
            foreach (BuildPieceDefinition piece in catalog.Pieces) Assert.AreSame(piece, catalog.Piece(catalog.PieceId(piece)));
            var battle = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBattleSettings>("Assets/_Project/Data/GameFlow/Battle.asset");
            Assert.AreSame(catalog, battle.RunContent, "the Game scene battle settings must point at the catalog");
        }

        static void AssertListed<T>(IReadOnlyList<T> listed, string type) where T : Object
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + type, new[] { "Assets/_Project" }))
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                    Assert.IsTrue(listed.Contains(asset), assetPath + " is missing from the run content catalog. Re-run ProjectNeo/Setup/Refresh Run Save Content (FLOW-02).");
            }
        }
    }
}
