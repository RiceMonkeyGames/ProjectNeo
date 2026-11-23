using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// PREP-10 영웅 등급·승급(Q-73, 사용자 결정 2026-11-20): 고용 등급에서 시작, 승리한 대장만 경험치, 영웅의 전당에서
    /// 골드 + 준비 1시간으로 승급, 등급마다 체력·공격 +15%, 저장·되살리기.
    /// </summary>
    public class HeroPromotionTests
    {
        readonly List<Object> created = new List<Object>();
        SquadConfig squadConfig;
        HeroDefinition playerHero, footman, knight, champion;
        FacilityDefinition tavern, heroHall;
        HeroPromotionSettings promotion;
        PrepTimeSettings prepTime;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            squadConfig = Track(SquadConfig.Create(4, 10));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.MaxHealth, 100f)));
            footman = Track(HeroDefinition.Create("footman", 1, new Cost(60), true,
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 20f)));
            knight = Track(HeroDefinition.Create("knight", 2, new Cost(120), true,
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 20f)));
            champion = Track(HeroDefinition.Create("champion", 3, new Cost(200)));
            tavern = Track(FacilityDefinition.Create(FacilityType.Tavern, new FacilityLevel
            {
                upgradeCost = new Cost(0), offerCount = 3,
                heroPool = new[] { footman, knight, champion }.Select(hero => new WeightedEntry<HeroDefinition>(hero)).ToList(),
            }));
            heroHall = Track(FacilityDefinition.Create(FacilityType.HeroHall, "Hero Hall", new FacilityLevel { upgradeCost = new Cost(0) }));
            promotion = Track(HeroPromotionSettings.CreateDefault());
            prepTime = Track(PrepTimeSettings.Create(12f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        RunState NewRun(int gold = 2000, bool withHall = true, bool withPromotion = true)
        {
            var facilities = withHall ? new[] { tavern, heroHall } : new[] { tavern };
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, gold, null, facilities))
                .WithPrepTime(prepTime);
            if (withPromotion) config.WithHeroPromotion(promotion);
            RunState run = RunState.CreateNew(config);
            run.Army.TryAddSquad();
            run.Army.TryAddSquad();
            run.RestockTavernIfNeeded();
            return run;
        }

        static RunHero Hire(RunState run, HeroDefinition hero, int captainOf)
        {
            RunHero hired = run.TryHire(hero, captainOf < 0 ? null : run.Army.Squads[captainOf]);
            Assert.IsNotNull(hired, "test hire must succeed: " + hero.Id);
            return hired;
        }

        static void Fight(RunState run, BattleOutcome outcome, params (RunUnit unit, UnitFate fate)[] fates)
        {
            BattleInput input = run.CreateBattleInput(1);
            var result = new BattleResult(input.BattleNumber, outcome,
                outcome == BattleOutcome.Victory ? BattleEndReason.EnemiesDefeated : BattleEndReason.PlayerDied);
            foreach ((RunUnit unit, UnitFate fate) in fates) result.SetFate(unit.Id, fate);
            Assert.AreEqual(BattleResultIssue.None, run.Apply(result));
        }

        [Test]
        public void HiredHero_StartsAtItsHireTierGrade_WithTheGradeBonus()
        {
            RunState run = NewRun();
            RunHero low = Hire(run, footman, -1);
            RunHero high = Hire(run, knight, -1);
            RunHero top = Hire(run, champion, -1);

            Assert.AreEqual(1, low.Grade);
            Assert.AreEqual(2, high.Grade);
            Assert.AreEqual(3, top.Grade);
            Assert.AreEqual(100f, low.Character.Stats.GetValue(StatType.MaxHealth), 0.001f, "grade 1 has no bonus");
            Assert.AreEqual(115f, high.Character.Stats.GetValue(StatType.MaxHealth), 0.001f);
            Assert.AreEqual(23f, high.Character.Stats.GetValue(StatType.Attack), 0.001f);
            Assert.AreEqual(3, run.Player.Grade, "the player is always at the highest grade (UI-07)");
            Assert.AreEqual(130f, run.Player.Character.Stats.GetValue(StatType.MaxHealth), 0.001f, "and gets that grade's bonus");
            CollectionAssert.AreEqual(new[] { low, high, top }, run.GradedHeroes, "the player is not listed for promotion");
        }

        [Test]
        public void Skills_AreFixedHeroSkills_OpenedByGrade_AndThePlayerHasAll()
        {
            var skills = new ProjectNeo.Skills.SkillDefinition[3];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = Track(ProjectNeo.Skills.SkillDefinition.Create("s" + i, 10f, ProjectNeo.Skills.SkillTargetMode.Self, 0f, 3f));
            SetSkills(playerHero, skills);
            SetSkills(footman, skills);
            RunState run = NewRun();
            RunHero hero = Hire(run, footman, 0);

            CollectionAssert.AreEqual(skills, run.PlayerSkills(), "the player has all 3 fixed skills from the start");
            CollectionAssert.AreEqual(new[] { skills[0] }, RunState.OpenSkills(hero), "grade 1 opens the first skill");
            Fight(run, BattleOutcome.Victory);
            Fight(run, BattleOutcome.Victory);
            Assert.IsTrue(run.TryPromote(hero));
            CollectionAssert.AreEqual(new[] { skills[0], skills[1] }, RunState.OpenSkills(hero), "each grade opens the next one");
        }

        static void SetSkills(HeroDefinition hero, ProjectNeo.Skills.SkillDefinition[] skills)
        {
            var data = new UnityEditor.SerializedObject(hero);
            UnityEditor.SerializedProperty list = data.FindProperty("skills");
            list.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Victory_GivesExpOnlyToSurvivingCaptains_AndExpStopsAtTheNextGradeNeed()
        {
            RunState run = NewRun();
            RunHero captain = Hire(run, footman, 0);
            RunHero fled = Hire(run, knight, 1);
            RunHero reserve = Hire(run, champion, -1);

            Fight(run, BattleOutcome.Victory, (fled, UnitFate.Fled));

            Assert.AreEqual(100, captain.Exp);
            Assert.AreEqual(100, fled.Exp, "a captain who fled still survived");
            Assert.AreEqual(0, reserve.Exp, "reserve heroes do not fight");
            Assert.AreEqual(0, run.Player.Exp);

            Fight(run, BattleOutcome.Victory);
            Fight(run, BattleOutcome.Victory);
            Assert.AreEqual(200, captain.Exp, "grade 2 needs 200; EXP does not stack past it");
            Assert.AreEqual(300, fled.Exp, "grade 3 needs 400");
        }

        [Test]
        public void DefeatOrDeath_GivesNoExp()
        {
            RunState run = NewRun();
            RunHero survivor = Hire(run, footman, 0);
            RunHero fallen = Hire(run, knight, 1);

            Fight(run, BattleOutcome.Defeat);
            Assert.AreEqual(0, survivor.Exp, "no EXP on defeat");

            Fight(run, BattleOutcome.Victory, (fallen, UnitFate.Dead));
            Assert.AreEqual(100, survivor.Exp);
            CollectionAssert.Contains(run.FallenHeroes, fallen);
            Assert.AreEqual(0, fallen.Exp);
        }

        [Test]
        public void CheckPromote_ReportsTheFirstReason()
        {
            RunState run = NewRun(gold: 300);
            RunHero hero = Hire(run, footman, 0);
            RunHero top = Hire(run, champion, -1);

            Assert.AreEqual(HeroPromoteCheck.NotPromotable, run.CheckPromote(run.Player));
            Assert.AreEqual(HeroPromoteCheck.MaxGrade, run.CheckPromote(top));
            Assert.AreEqual(HeroPromoteCheck.NotEnoughExp, run.CheckPromote(hero));

            Fight(run, BattleOutcome.Victory);
            Fight(run, BattleOutcome.Victory);
            Assert.AreEqual(HeroPromoteCheck.NotEnoughFunds, run.CheckPromote(hero), "40 gold left after hiring, 100 needed");

            run.Wallet.AddGold(1000);
            run.BeginPreparationClockIfNeeded();
            Assert.IsTrue(run.TrySpendPrepTime(11.5f));
            Assert.AreEqual(HeroPromoteCheck.NotEnoughTime, run.CheckPromote(hero));

            BattleInput pending = run.CreateBattleInput(1);
            Assert.AreEqual(HeroPromoteCheck.BattlePending, run.CheckPromote(hero));
            Assert.IsFalse(run.TryPromote(hero));
            Assert.AreEqual(1, hero.Grade);
            Assert.Greater(pending.BattleNumber, 0);
        }

        [Test]
        public void CheckPromote_NeedsTheHallAndTheSettings()
        {
            RunHero noHall = Hire(NewRun(withHall: false), footman, -1);
            Assert.AreEqual(1, noHall.Grade);

            RunState withoutHall = NewRun(withHall: false);
            Assert.AreEqual(HeroPromoteCheck.NotConfigured, withoutHall.CheckPromote(Hire(withoutHall, footman, -1)));

            RunState withoutSettings = NewRun(withPromotion: false);
            RunHero hero = Hire(withoutSettings, knight, -1);
            Assert.AreEqual(2, hero.Grade, "the hire tier grade stays even without settings");
            Assert.AreEqual(100f, hero.Character.Stats.GetValue(StatType.MaxHealth), 0.001f, "no grade bonus without settings");
            Assert.AreEqual(HeroPromoteCheck.NotConfigured, withoutSettings.CheckPromote(hero));
        }

        [Test]
        public void TryPromote_PaysGoldAndOneHour_RaisesTheGrade_ResetsExp_AndAddsTheBonus()
        {
            RunState run = NewRun(gold: 1000);
            RunHero hero = Hire(run, footman, 0);
            for (int i = 0; i < 2; i++) Fight(run, BattleOutcome.Victory);
            run.BeginPreparationClockIfNeeded();
            int gold = run.Wallet.Gold;
            float hoursLeft = run.Clock.Remaining;
            int changed = 0;
            run.Changed += () => changed++;

            Assert.AreEqual(HeroPromoteCheck.Ok, run.CheckPromote(hero));
            Assert.IsTrue(run.TryPromote(hero));

            Assert.AreEqual(2, hero.Grade);
            Assert.AreEqual(0, hero.Exp);
            Assert.AreEqual(gold - 100, run.Wallet.Gold);
            Assert.AreEqual(hoursLeft - 1f, run.Clock.Remaining, 0.001f);
            Assert.AreEqual(115f, hero.Character.Stats.GetValue(StatType.MaxHealth), 0.001f);
            Assert.Greater(changed, 0);

            for (int i = 0; i < 4; i++) Fight(run, BattleOutcome.Victory);
            run.BeginPreparationClockIfNeeded();
            Assert.IsTrue(run.TryPromote(hero));
            Assert.AreEqual(3, hero.Grade);
            Assert.AreEqual(gold - 350, run.Wallet.Gold);
            Assert.AreEqual(130f, hero.Character.Stats.GetValue(StatType.MaxHealth), 0.001f, "+15% per grade above 1, not stacked twice");
            Assert.AreEqual(26f, hero.Character.Stats.GetValue(StatType.Attack), 0.001f);
            Assert.AreEqual(HeroPromoteCheck.MaxGrade, run.CheckPromote(hero));
        }

        [Test]
        public void SaveAndRestore_KeepGradeAndExp_AndOldSavesStartAtTheHireTier_AndGetTheHall()
        {
            RunState run = NewRun(gold: 1000);
            RunHero hero = Hire(run, footman, 0);
            RunHero reserve = Hire(run, knight, -1);
            for (int i = 0; i < 2; i++) Fight(run, BattleOutcome.Victory);
            run.BeginPreparationClockIfNeeded();
            Assert.IsTrue(run.TryPromote(hero));
            Fight(run, BattleOutcome.Victory);

            RunContentCatalog content = Track(RunContentCatalog.Create(null, null,
                new[] { playerHero, footman, knight, champion }, null, new[] { tavern, heroHall }));
            var options = new RunRestoreOptions
            {
                SquadConfig = squadConfig, PrepTime = prepTime, HeroPromotion = promotion, Facilities = new[] { tavern, heroHall },
            };
            RunSnapshot saved = run.Capture(2, content);
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNull(error);
            RunHero restoredHero = restored.GradedHeroes.Single(h => h.Id == hero.Id);
            Assert.AreEqual(2, restoredHero.Grade);
            Assert.AreEqual(100, restoredHero.Exp);
            Assert.AreEqual(115f, restoredHero.Character.Stats.GetValue(StatType.MaxHealth), 0.001f);
            Assert.AreEqual(JsonUtility.ToJson(saved), JsonUtility.ToJson(restored.Capture(2, content)));

            // PREP-10 이전 저장: 등급 칸이 없고(0) 영웅의 전당도 없다.
            foreach (HeroRecord record in saved.heroes) { record.grade = 0; record.exp = 0; }
            saved.facilities.RemoveAll(record => record.type == (int)FacilityType.HeroHall);
            RunState old = RunState.Restore(saved, content, options, out error);
            Assert.IsNull(error);
            Assert.AreEqual(1, old.GradedHeroes.Single(h => h.Id == hero.Id).Grade);
            Assert.AreEqual(2, old.GradedHeroes.Single(h => h.Id == reserve.Id).Grade, "starts at its hire tier");
            Assert.IsNotNull(old.HeroHall, "the hall from the start config is added at level 0");
        }
    }
}
