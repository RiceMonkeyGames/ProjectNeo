using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>META-09 퍽 트리: 선행 퍽, 거절 이유, 데이터 검사, 퍽 창 규칙(두 번 눌러 찍기)과 글자.</summary>
    public class PerkTreeTests
    {
        readonly List<Object> created = new List<Object>();
        PerkDefinition blade, toughness, swift, champion;
        PerkTreeCatalog catalog;
        RunState run;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            blade = Track(PerkDefinition.Create("blade", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.15f)) }, displayName: "Sharpened Blade"));
            toughness = Track(PerkDefinition.Create("toughness", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.MaxHealth, 0.2f)) },
                displayName: "Toughness", prerequisites: new[] { blade }));
            swift = Track(PerkDefinition.Create("swift", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.AttackSpeed, 0.1f)) },
                displayName: "Swift Strikes", prerequisites: new[] { blade }));
            champion = Track(PerkDefinition.Create("champion", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Flat(StatType.Defense, 5f)) },
                displayName: "Champion", prerequisites: new[] { toughness, swift }));
            catalog = Track(PerkTreeCatalog.Create(Track(PerkTreeDefinition.Create(PerkCategory.Combat, "Combat",
                new[] { blade, toughness, swift, champion }))));
            HeroDefinition player = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            run = RunState.CreateNew(Track(RunConfig.Create(Track(SquadConfig.Create(4, 10)), player, 0)));
            run.SetupPerks(new MetaProgress { perkPoints = 3 });
        }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        [Test]
        public void Prerequisites_MustAllBeTaken()
        {
            Assert.AreEqual(PerkAllocateCheck.MissingPrerequisite, run.CheckPerk(toughness));
            Assert.IsTrue(run.TryAllocatePerk(blade));
            Assert.IsTrue(run.TryAllocatePerk(toughness));
            Assert.AreEqual(PerkAllocateCheck.MissingPrerequisite, run.CheckPerk(champion), "선행 퍽 두 개가 모두 필요하다");
            CollectionAssert.AreEqual(new[] { swift }, run.Perks.MissingPrerequisites(champion));
            Assert.IsTrue(run.TryAllocatePerk(swift));
            Assert.AreEqual(PerkAllocateCheck.NotEnoughPoints, run.CheckPerk(champion), "포인트 3개를 다 썼다");
        }

        [Test]
        public void Check_ReportsEachReason()
        {
            Assert.AreEqual(PerkAllocateCheck.UnknownPerk, run.CheckPerk(null));
            run.TryAllocatePerk(blade);
            Assert.AreEqual(PerkAllocateCheck.AlreadyAllocated, run.CheckPerk(blade));
            PerkDefinition locked = Track(PerkDefinition.Create("locked", PerkCategory.Combat, unlockedByDefault: false));
            Assert.AreEqual(PerkAllocateCheck.Locked, run.CheckPerk(locked));
            run.CreateBattleInput(1);
            Assert.AreEqual(PerkAllocateCheck.BattlePending, run.CheckPerk(toughness));
        }

        [Test]
        public void Window_FirstClickArms_SecondClickTakes()
        {
            var window = new PerkWindowModel(run, catalog);

            Assert.AreEqual(UnlockClickResult.Armed, window.Click(blade));
            Assert.AreEqual(0, run.Perks.Allocated.Count, "첫 클릭은 찍지 않는다");
            StringAssert.Contains("Click again to take (1 pt)", window.EntryText(blade));

            Assert.AreEqual(UnlockClickResult.Opened, window.Click(blade));

            CollectionAssert.AreEqual(new[] { blade }, run.Perks.Allocated);
            Assert.AreEqual("Took Sharpened Blade.", window.Notice);
            StringAssert.Contains("Taken", window.EntryText(blade));
            Assert.AreEqual("Perk points: 2 left (spent 1)", window.PointsText);
            Assert.IsNull(window.ArmedPerk);
        }

        [Test]
        public void Window_RefusesWithTheReason()
        {
            var window = new PerkWindowModel(run, catalog);
            StringAssert.Contains("Needs Toughness, Swift Strikes", window.EntryText(champion));

            Assert.AreEqual(UnlockClickResult.Refused, window.Click(toughness));
            Assert.AreEqual("Take Sharpened Blade first.", window.Notice);

            window.Click(blade); window.Click(blade);
            Assert.AreEqual(UnlockClickResult.Refused, window.Click(blade));
            Assert.AreEqual("Sharpened Blade is already taken.", window.Notice);

            run.CreateBattleInput(1);
            Assert.AreEqual(UnlockClickResult.Refused, window.Click(toughness));
            Assert.AreEqual("Perks cannot be taken during battle.", window.Notice);
        }

        [Test]
        public void Describe_BuildsATextFromTheEffects()
        {
            Assert.AreEqual("Player attack +15%", PerkWindowModel.Describe(blade));
            Assert.AreEqual("Player defense +5", PerkWindowModel.Describe(champion));
            PerkDefinition shop = Track(PerkDefinition.Create("shop", PerkCategory.Economy,
                runEffects: new[] { new PerkRunEffect(PerkRunEffectKind.ShopPricePercent, -10f), new PerkRunEffect(PerkRunEffectKind.PrepHours, 1f) }));
            Assert.AreEqual("Shop prices -10%, Prep time +1h", PerkWindowModel.Describe(shop));
            MoraleBoostPerkEffect avenge = Track(MoraleBoostPerkEffect.Create(PerkTrigger.AllySquadMemberDied, 3f));
            PerkDefinition morale = Track(PerkDefinition.Create("morale", PerkCategory.UnitBoost, specialEffects: new PerkSpecialEffect[] { avenge }));
            Assert.AreEqual("Squad morale +3 when an ally squad member dies", PerkWindowModel.Describe(morale));
        }

        [Test]
        public void CatalogValidate_FindsDataMistakes()
        {
            Assert.IsEmpty(catalog.Validate());

            PerkDefinition outside = Track(PerkDefinition.Create("outside", PerkCategory.Combat));
            PerkDefinition needsOutside = Track(PerkDefinition.Create("needs", PerkCategory.Combat, prerequisites: new[] { outside }));
            PerkDefinition wrongTree = Track(PerkDefinition.Create("economy", PerkCategory.Economy));
            PerkDefinition duplicate = Track(PerkDefinition.Create("blade", PerkCategory.Combat));
            PerkTreeCatalog broken = Track(PerkTreeCatalog.Create(Track(PerkTreeDefinition.Create(PerkCategory.Combat, "Combat",
                new[] { blade, needsOutside, wrongTree, duplicate }))));

            Assert.AreEqual(3, broken.Validate().Count, string.Join("\n", broken.Validate()));
        }
    }
}
