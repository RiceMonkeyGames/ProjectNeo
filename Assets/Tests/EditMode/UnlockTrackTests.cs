using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Meta;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>런 사이 해금 트랙 규칙(META-05, Q-76).</summary>
    public class UnlockTrackTests
    {
        readonly List<Object> created = new List<Object>();
        UnlockCatalog catalog;
        MetaProgress progress;

        [SetUp]
        public void SetUp()
        {
            // 영웅 트랙: knight(처음부터 열림) -> archer 1 -> paladin 2. 시작 보너스 트랙: gold 1 -> grunts 3.
            HeroDefinition knight = Track(HeroDefinition.Create("knight"));
            HeroDefinition archer = Track(HeroDefinition.Create("archer", unlockedByDefault: false));
            HeroDefinition paladin = Track(HeroDefinition.Create("paladin", unlockedByDefault: false));
            UnlockTrackDefinition heroes = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "Heroes",
                UnlockEntry.ForContent("hero_knight", 0, UnlockPayloadKind.Hero, knight),
                UnlockEntry.ForContent("hero_archer", 1, UnlockPayloadKind.Hero, archer),
                UnlockEntry.ForContent("hero_paladin", 2, UnlockPayloadKind.Hero, paladin)));
            UnlockTrackDefinition bonuses = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "Starting bonuses",
                UnlockEntry.Create("bonus_gold", 1, UnlockPayload.StartingGold(50)),
                UnlockEntry.Create("bonus_grunts", 3, UnlockPayload.StartingGrunts(2))));
            catalog = Track(UnlockCatalog.Create(heroes, bonuses));
            progress = new MetaProgress();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [Test]
        public void DefaultFlag_OpensEntryWithoutBuying()
        {
            Assert.IsTrue(UnlockTracks.IsOpened(progress, catalog.FindEntry("hero_knight")));
            Assert.IsFalse(UnlockTracks.IsOpened(progress, catalog.FindEntry("hero_archer")));
            Assert.AreEqual(UnlockOpenCheck.AlreadyOpened, UnlockTracks.Check(progress, catalog, "hero_knight"));
        }

        [Test]
        public void TryOpen_SpendsCostAndOpens()
        {
            progress.unlockPoints = 5;

            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, catalog, "hero_archer"));

            Assert.AreEqual(4, progress.unlockPoints);
            CollectionAssert.AreEqual(new[] { "hero_archer" }, progress.openedEntryIds);
            Assert.AreEqual(UnlockOpenCheck.AlreadyOpened, UnlockTracks.TryOpen(progress, catalog, "hero_archer"), "같은 항목을 두 번 살 수 없다.");
            Assert.AreEqual(4, progress.unlockPoints);
        }

        [Test]
        public void StraightTrack_OpensInOrder()
        {
            progress.unlockPoints = 10;

            Assert.AreEqual(UnlockOpenCheck.MissingPrerequisite, UnlockTracks.TryOpen(progress, catalog, "hero_paladin"));
            Assert.AreEqual(10, progress.unlockPoints);

            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, catalog, "hero_archer"));
            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, catalog, "hero_paladin"));
            Assert.AreEqual(7, progress.unlockPoints);
        }

        [Test]
        public void FirstEntry_AfterDefaultEntry_NeedsOnlyTheCost()
        {
            CollectionAssert.AreEqual(new[] { "hero_knight" }, catalog.FindEntry("hero_archer").PrerequisiteIds);
            Assert.IsEmpty(catalog.FindEntry("hero_knight").PrerequisiteIds);
            Assert.IsEmpty(catalog.FindEntry("bonus_gold").PrerequisiteIds);
        }

        [Test]
        public void NotEnoughPoints_ChangesNothing()
        {
            progress.unlockPoints = 2;
            UnlockTracks.TryOpen(progress, catalog, "bonus_gold");

            Assert.AreEqual(UnlockOpenCheck.NotEnoughPoints, UnlockTracks.TryOpen(progress, catalog, "bonus_grunts"));

            Assert.AreEqual(1, progress.unlockPoints);
            CollectionAssert.AreEqual(new[] { "bonus_gold" }, progress.openedEntryIds);
        }

        [Test]
        public void UnknownEntry_IsRefused()
        {
            progress.unlockPoints = 10;

            Assert.AreEqual(UnlockOpenCheck.UnknownEntry, UnlockTracks.TryOpen(progress, catalog, "missing"));
            Assert.AreEqual(UnlockOpenCheck.UnknownEntry, UnlockTracks.TryOpen(progress, catalog, null));
            Assert.AreEqual(10, progress.unlockPoints);
        }

        [Test]
        public void Check_DoesNotChangeProgress()
        {
            progress.unlockPoints = 5;

            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.Check(progress, catalog, "hero_archer"));

            Assert.AreEqual(5, progress.unlockPoints);
            Assert.IsEmpty(progress.openedEntryIds);
        }

        [Test]
        public void BranchingPrerequisites_NeedEveryPrerequisite()
        {
            UnlockTrackDefinition recipes = Track(UnlockTrackDefinition.Create(UnlockTrackCategory.EquipmentRecipes, "Recipes", new[]
            {
                UnlockEntry.Create("recipe_a", 1, UnlockPayload.EquipmentRecipe("sword")),
                UnlockEntry.Create("recipe_b", 1, UnlockPayload.EquipmentRecipe("shield")),
                UnlockEntry.Create("recipe_c", 1, UnlockPayload.EquipmentRecipe("armor"), prerequisiteIds: new[] { "recipe_a", "recipe_b" }),
            }));
            UnlockCatalog branching = Track(UnlockCatalog.Create(recipes));
            progress.unlockPoints = 10;

            UnlockTracks.TryOpen(progress, branching, "recipe_a");
            Assert.AreEqual(UnlockOpenCheck.MissingPrerequisite, UnlockTracks.Check(progress, branching, "recipe_c"));
            UnlockTracks.TryOpen(progress, branching, "recipe_b");
            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, branching, "recipe_c"));
        }

        [Test]
        public void SameItem_CanSitOnTwoTracks()
        {
            ItemDefinition sword = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(10), unlockedByDefault: false));
            UnlockCatalog twoTracks = Track(UnlockCatalog.Create(
                Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.EquipmentRecipes, "Recipes",
                    UnlockEntry.ForContent("recipe_sword", 1, UnlockPayloadKind.EquipmentRecipe, sword))),
                Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.ShopItems, "Shop",
                    UnlockEntry.ForContent("shop_sword", 1, UnlockPayloadKind.ShopItem, sword)))));
            progress.unlockPoints = 1;

            Assert.IsEmpty(twoTracks.Validate());
            Assert.AreEqual(UnlockOpenCheck.Ok, UnlockTracks.TryOpen(progress, twoTracks, "shop_sword"));
            Assert.IsFalse(UnlockTracks.IsOpened(progress, twoTracks.FindEntry("recipe_sword")), "상점 항목을 열어도 레시피는 따로다.");
        }

        [Test]
        public void ImportLegacyUnlocks_OpensMatchingContentEntriesForFree()
        {
            progress.unlockedIds.Add("paladin");

            Assert.AreEqual(1, UnlockTracks.ImportLegacyUnlocks(progress, catalog));

            CollectionAssert.AreEqual(new[] { "hero_paladin" }, progress.openedEntryIds);
            Assert.AreEqual(0, progress.unlockPoints);
            Assert.AreEqual(0, UnlockTracks.ImportLegacyUnlocks(progress, catalog), "두 번 옮겨도 늘어나지 않는다.");
        }

        [Test]
        public void ContentAvailability_FollowsEntries_AndAllowsContentOffTrack()
        {
            Assert.IsTrue(UnlockTracks.IsContentAvailable(progress, catalog, UnlockPayloadKind.Hero, "knight"), "처음부터 열린 항목");
            Assert.IsFalse(UnlockTracks.IsContentAvailable(progress, catalog, UnlockPayloadKind.Hero, "archer"), "잠긴 항목");
            Assert.IsTrue(UnlockTracks.IsContentAvailable(progress, catalog, UnlockPayloadKind.Hero, "prototype"), "트랙 밖 콘텐츠는 쓸 수 있다");
            Assert.IsTrue(UnlockTracks.IsContentAvailable(progress, catalog, UnlockPayloadKind.ShopItem, "archer"), "종류가 다르면 상관없다");

            progress.unlockPoints = 1;
            UnlockTracks.TryOpen(progress, catalog, "hero_archer");

            Assert.IsTrue(UnlockTracks.IsContentAvailable(progress, catalog, UnlockPayloadKind.Hero, "archer"));
        }

        [Test]
        public void StartingBonus_SumsOnlyOpenedEntries()
        {
            UnlockTrackDefinition bonuses = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.StartingBonuses, "Bonuses",
                UnlockEntry.Create("gold_a", 0, UnlockPayload.StartingGold(50), openedByDefault: true),
                UnlockEntry.Create("potions", 1, UnlockPayload.StartingItem("potion", 2)),
                UnlockEntry.Create("more_potions", 1, UnlockPayload.StartingItem("potion", 1)),
                UnlockEntry.Create("grunts", 1, UnlockPayload.StartingGrunts(2)),
                UnlockEntry.Create("gold_b", 1, UnlockPayload.StartingGold(100))));
            UnlockCatalog withBonuses = Track(UnlockCatalog.Create(bonuses));
            progress.unlockPoints = 3;
            UnlockTracks.TryOpen(progress, withBonuses, "potions");
            UnlockTracks.TryOpen(progress, withBonuses, "more_potions");
            UnlockTracks.TryOpen(progress, withBonuses, "grunts");

            UnlockStartingBonus bonus = UnlockTracks.StartingBonus(progress, withBonuses);

            Assert.AreEqual(50, bonus.Gold, "열지 않은 gold_b는 빠진다");
            Assert.AreEqual(2, bonus.Grunts);
            Assert.AreEqual(3, bonus.Items["potion"]);
            Assert.IsTrue(UnlockTracks.StartingBonus(new MetaProgress(), Track(UnlockCatalog.Create())).IsEmpty);
        }

        [Test]
        public void Validate_ReportsDataMistakes()
        {
            Assert.IsEmpty(catalog.Validate());

            UnlockCatalog broken = Track(UnlockCatalog.Create(
                Track(UnlockTrackDefinition.Create(UnlockTrackCategory.Heroes, "A", new[]
                {
                    UnlockEntry.Create("dup", 1, UnlockPayload.Hero("a")),
                    UnlockEntry.Create("self", 1, UnlockPayload.Hero("b"), prerequisiteIds: new[] { "self" }),
                    UnlockEntry.Create("orphan", 1, UnlockPayload.Hero("c"), prerequisiteIds: new[] { "nowhere" }),
                })),
                Track(UnlockTrackDefinition.Create(UnlockTrackCategory.Heroes, "B", new[]
                {
                    UnlockEntry.Create("dup", 1, UnlockPayload.Hero("d")),
                }))));

            List<string> problems = broken.Validate();

            Assert.AreEqual(4, problems.Count, string.Join("\n", problems));
        }
    }
}
