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

namespace ProjectNeo.Tests
{
    /// <summary>META-02 런 퍽: 런 효과(보상 골드·상점 가격·준비 시간), 전투 중 찍기 금지, 런 저장.</summary>
    public class RunPerkTests
    {
        readonly List<Object> created = new List<Object>();
        HeroDefinition playerHero;
        ItemDefinition sword;
        FacilityDefinition shop;
        SquadConfig squadConfig;
        PrepTimeSettings time;
        EconomyConfig economy;
        PerkDefinition gold, discount, hours, attack;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            sword = Track(ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40)));
            shop = Track(FacilityDefinition.Create(FacilityType.Shop, new FacilityLevel
            {
                upgradeCost = new Cost(0), offerCount = 1,
                itemPool = new List<WeightedEntry<ItemDefinition>> { new WeightedEntry<ItemDefinition>(sword) },
            }));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            squadConfig = Track(SquadConfig.Create(4, 10));
            time = Track(PrepTimeSettings.Create(budgetHours: 3f, craftHours: 1f, upgradeHours: 1f, waitHours: 1f, scoutHours: 3f));
            economy = Track(EconomyConfig.Create(0.5f));
            gold = Track(PerkDefinition.Create("gold", PerkCategory.Economy,
                runEffects: new[] { new PerkRunEffect(PerkRunEffectKind.RewardGoldPercent, 25f) }));
            discount = Track(PerkDefinition.Create("discount", PerkCategory.Economy,
                runEffects: new[] { new PerkRunEffect(PerkRunEffectKind.ShopPricePercent, -20f) }));
            hours = Track(PerkDefinition.Create("hours", PerkCategory.Economy,
                runEffects: new[] { new PerkRunEffect(PerkRunEffectKind.PrepHours, 2f) }));
            attack = Track(PerkDefinition.Create("attack", PerkCategory.Combat,
                new[] { new PerkStatEffect(PerkTarget.Player, StatModifier.Percent(StatType.Attack, 0.25f)) }));
        }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        RunState NewRun(int perkPoints)
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, 100, null, new[] { shop }))
                .WithEconomy(economy).WithPrepTime(time);
            RunState run = RunState.CreateNew(config);
            run.SetupPerks(new MetaProgress { perkPoints = perkPoints });
            return run;
        }

        [Test]
        public void PerkPointsLimitAllocations_AndBonusPointsAddMore()
        {
            RunState run = NewRun(1);

            Assert.IsTrue(run.TryAllocatePerk(gold));
            Assert.IsFalse(run.TryAllocatePerk(discount), "포인트 1개로는 퍽 하나만 찍는다");
            run.Perks.BonusPoints = 1;
            Assert.IsTrue(run.TryAllocatePerk(discount));
            Assert.IsFalse(run.TryAllocatePerk(gold), "같은 퍽은 두 번 찍지 않는다");
        }

        [Test]
        public void PerksCannotBeAllocatedDuringBattle()
        {
            RunState run = NewRun(2);
            run.CreateBattleInput(1);

            Assert.IsFalse(run.CanAllocatePerk(gold), "전투 중에는 찍지 않는다(Q-06)");
            Assert.IsFalse(run.TryAllocatePerk(gold));
            Assert.AreEqual(0, run.Perks.Allocated.Count);
        }

        [Test]
        public void RewardGoldPerk_AddsPercentOfTheRolledGold()
        {
            RunState run = NewRun(1);
            Assert.AreEqual(0, run.PerkRewardGoldBonus(100));

            run.TryAllocatePerk(gold);

            Assert.AreEqual(25, run.PerkRewardGoldBonus(100));
            Assert.AreEqual(19, run.PerkRewardGoldBonus(75), "18.75는 반올림한다");
        }

        [Test]
        public void ShopPerk_LowersTheGoldPrice_AndBuyingPaysThatPrice()
        {
            RunState run = NewRun(1);
            run.RestockShopIfNeeded();
            Assert.AreEqual(40, run.Shop.GetBuyPrice(sword).gold);

            run.TryAllocatePerk(discount);

            Assert.AreEqual(32, run.Shop.GetBuyPrice(sword).gold);
            Assert.IsTrue(run.TryBuyAt(0));
            Assert.AreEqual(100 - 32, run.Wallet.Gold);
        }

        [Test]
        public void PrepHoursPerk_ExtendsThisPreparation_AndEveryLaterOne()
        {
            RunState run = NewRun(1);
            run.BeginPreparationClockIfNeeded();
            Assert.AreEqual(3f, run.Clock.Budget, 0.0001f);
            Assert.IsTrue(run.TrySpendPrepTime(1f));

            run.TryAllocatePerk(hours);

            Assert.AreEqual(5f, run.Clock.Budget, 0.0001f, "이번 준비에 바로 2시간이 늘어난다");
            Assert.AreEqual(4f, run.Clock.Remaining, 0.0001f, "쓴 시간은 그대로다");
        }

        [Test]
        public void RunSave_KeepsAllocatedPerks_AndSkipsMissingOnes()
        {
            RunState run = NewRun(2);
            run.TryAllocatePerk(gold);
            run.TryAllocatePerk(attack);
            var content = Track(RunContentCatalog.Create(new[] { sword }, new MaterialDefinition[0], new[] { playerHero },
                new SoldierDefinition[0], new[] { shop }, new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            content.SetPerks(new[] { gold });
            var options = new RunRestoreOptions { SquadConfig = squadConfig, PrepTime = time, Economy = economy };

            RunSnapshot saved = run.Capture(1, content);
            CollectionAssert.AreEqual(new[] { "gold", "attack" }, saved.perks);
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.IsNull(restored.Perks, "퍽 묶음은 씬이 메타 진행으로 만든다");
            restored.SetupPerks(new MetaProgress { perkPoints = 2 });

            CollectionAssert.AreEqual(new[] { gold }, restored.Perks.Allocated.ToList(), "목록에 없는 퍽(attack)은 건너뛴다");
            CollectionAssert.AreEqual(new[] { "gold" }, restored.Capture(1, content).perks);
        }

        [Test]
        public void SkillLoadout_EquipsLearnedSkills_AndSurvivesTheRunSave()
        {
            var skills = new ProjectNeo.Skills.SkillDefinition[4];
            var grants = new PerkDefinition[4];
            for (int i = 0; i < skills.Length; i++)
            {
                skills[i] = Track(ProjectNeo.Skills.SkillDefinition.Create("s" + i, 10f, ProjectNeo.Skills.SkillTargetMode.Self, 0f, 3f));
                grants[i] = Track(PerkDefinition.Create("grant" + i, PerkCategory.Combat, grantedSkill: skills[i]));
            }
            RunState run = NewRun(4);
            foreach (PerkDefinition grant in grants) run.TryAllocatePerk(grant);

            CollectionAssert.AreEqual(new[] { skills[0], skills[1], skills[2] }, run.EquippedPlayerSkills(), "3개까지는 배운 순서로 채운다");
            Assert.IsFalse(run.TryEquipPlayerSkill("s3"), "빈 칸이 없으면 장착하지 못한다");
            Assert.IsTrue(run.UnequipPlayerSkill(1));
            Assert.IsFalse(run.TryEquipPlayerSkill("unknown"), "배우지 않은 스킬은 장착하지 못한다");
            Assert.IsTrue(run.TryEquipPlayerSkill("s3"));
            Assert.IsFalse(run.TryEquipPlayerSkill("s3"), "이미 장착한 스킬");

            var content = Track(RunContentCatalog.Create(new[] { sword }, new MaterialDefinition[0], new[] { playerHero },
                new SoldierDefinition[0], new[] { shop }, new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            content.SetPerks(grants);
            var options = new RunRestoreOptions { SquadConfig = squadConfig, PrepTime = time, Economy = economy };
            RunSnapshot saved = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(run.Capture(1, content)));
            CollectionAssert.AreEqual(new[] { "s0", "s3", "s2" }, saved.playerSkills);

            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            restored.SetupPerks(new MetaProgress { perkPoints = 4 });
            CollectionAssert.AreEqual(new[] { skills[0], skills[3], skills[2] }, restored.EquippedPlayerSkills(), "칸 순서를 지킨다");

            saved.playerSkills = null;
            RunState old = RunState.Restore(saved, content, options, out error);
            Assert.IsNotNull(old, error);
            old.SetupPerks(new MetaProgress { perkPoints = 4 });
            CollectionAssert.AreEqual(new[] { skills[0], skills[1], skills[2] }, old.EquippedPlayerSkills(), "예전 저장은 배운 순서로 채운다");
        }

        [Test]
        public void RestoredPerks_NeedEnoughPoints()
        {
            RunState run = NewRun(1);
            run.Perks.BonusPoints = 1;
            run.TryAllocatePerk(gold);
            run.TryAllocatePerk(discount);
            var content = Track(RunContentCatalog.Create(new[] { sword }, new MaterialDefinition[0], new[] { playerHero },
                new SoldierDefinition[0], new[] { shop }, new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            content.SetPerks(new[] { gold, discount });
            var options = new RunRestoreOptions { SquadConfig = squadConfig, PrepTime = time, Economy = economy };

            RunState restored = RunState.Restore(run.Capture(1, content), content, options, out string error);
            Assert.IsNotNull(restored, error);
            restored.SetupPerks(new MetaProgress { perkPoints = 1 });

            CollectionAssert.AreEqual(new[] { gold }, restored.Perks.Allocated.ToList(), "덤 포인트로 찍은 퍽은 포인트가 모자라 빠진다");
            Assert.AreEqual(40, restored.Shop.GetBuyPrice(sword).gold);
        }

        [Test]
        public void PerkRunEffectTotals_AddUpAcrossPerks()
        {
            PerkDefinition more = Track(PerkDefinition.Create("more", PerkCategory.Economy, runEffects: new[]
            {
                new PerkRunEffect(PerkRunEffectKind.RewardGoldPercent, 10f),
                new PerkRunEffect(PerkRunEffectKind.PrepHours, 1f),
            }));
            var loadout = new PerkLoadout(new MetaProgress { perkPoints = 2 });
            loadout.TryAllocate(gold);
            loadout.TryAllocate(more);

            Assert.AreEqual(35f, loadout.RunEffectTotal(PerkRunEffectKind.RewardGoldPercent), 0.0001f);
            Assert.AreEqual(1f, loadout.RunEffectTotal(PerkRunEffectKind.PrepHours), 0.0001f);
            Assert.AreEqual(0f, loadout.RunEffectTotal(PerkRunEffectKind.ShopPricePercent), 0.0001f);
        }
    }
}
