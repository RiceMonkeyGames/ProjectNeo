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
        public void Window_SelectOnlyPicks_AcquireTakes()
        {
            var window = new PerkWindowModel(run, catalog);

            window.Select(blade);
            Assert.AreSame(blade, window.SelectedPerk);
            Assert.AreEqual(0, run.Perks.Allocated.Count, "고르기만 하고 찍지 않는다");
            StringAssert.Contains("Sharpened Blade", window.DetailText(blade));
            StringAssert.DoesNotContain("포인트", window.DetailText(blade), "모든 퍽이 1포인트라 비용은 보이지 않는다");

            Assert.AreEqual(UnlockClickResult.Opened, window.Acquire());

            CollectionAssert.AreEqual(new[] { blade }, run.Perks.Allocated);
            Assert.AreEqual("Sharpened Blade 획득.", window.Notice);
            StringAssert.Contains("획득함", window.EntryText(blade));
            Assert.AreEqual("획득함", window.AcquireLabel);
            Assert.AreEqual("특성 포인트: 2 남음 (사용 1)", window.PointsText);
        }

        [Test]
        public void Window_RefusesWithTheReason()
        {
            var window = new PerkWindowModel(run, catalog);
            StringAssert.Contains("필요: Toughness, Swift Strikes", window.EntryText(champion));

            Assert.AreEqual(UnlockClickResult.Refused, window.Acquire(), "고른 퍽이 없으면 얻지 못한다");

            window.Select(toughness);
            Assert.AreEqual(UnlockClickResult.Refused, window.Acquire());
            Assert.AreEqual("먼저 Sharpened Blade을(를) 얻으세요.", window.Notice);

            window.Select(blade); window.Acquire();
            Assert.AreEqual(UnlockClickResult.Refused, window.Acquire());
            Assert.AreEqual("Sharpened Blade은(는) 이미 얻었습니다.", window.Notice);

            run.CreateBattleInput(1);
            window.Select(toughness);
            Assert.AreEqual(UnlockClickResult.Refused, window.Acquire());
            Assert.AreEqual("전투 중에는 특성을 얻을 수 없습니다.", window.Notice);
        }

        [Test]
        public void Describe_BuildsATextFromTheEffects()
        {
            Assert.AreEqual("플레이어 공격력 +15%", PerkWindowModel.Describe(blade));
            Assert.AreEqual("플레이어 방어력 +5", PerkWindowModel.Describe(champion));
            PerkDefinition shop = Track(PerkDefinition.Create("shop", PerkCategory.Economy,
                runEffects: new[] { new PerkRunEffect(PerkRunEffectKind.ShopPricePercent, -10f), new PerkRunEffect(PerkRunEffectKind.PrepHours, 1f) }));
            Assert.AreEqual("상점 가격 -10%, 준비 시간 +1시간", PerkWindowModel.Describe(shop));
            MoraleBoostPerkEffect avenge = Track(MoraleBoostPerkEffect.Create(PerkTrigger.AllySquadMemberDied, 3f));
            PerkDefinition morale = Track(PerkDefinition.Create("morale", PerkCategory.UnitBoost, specialEffects: new PerkSpecialEffect[] { avenge }));
            Assert.AreEqual("부대 사기 +3 (아군 부대원이 죽을 때)", PerkWindowModel.Describe(morale));
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
