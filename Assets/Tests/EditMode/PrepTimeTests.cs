using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PREP-06 준비 시간(Q-53): 시계 규칙, 대장간 시간 비용, 기다리기, 준비마다 새 예산, 런 저장.</summary>
    public class PrepTimeTests
    {
        readonly List<Object> created = new List<Object>();
        HeroDefinition playerHero;
        ItemDefinition sword;
        MaterialDefinition hide;
        FacilityDefinition smithy;
        SquadConfig squadConfig;
        EquipmentUpgradeSettings upgrades;
        PrepTimeSettings time;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [SetUp]
        public void SetUp()
        {
            hide = Track(MaterialDefinition.Create("hide"));
            var materials = new ItemUpgradeMaterials[3];
            for (int i = 0; i < materials.Length; i++) materials[i] = new ItemUpgradeMaterials(new MaterialAmount(hide, 1));
            sword = Track(ItemDefinition.CreateUpgradable("sword", new Cost(40), materials, true, StatModifier.Flat(StatType.Attack, 10f)));
            smithy = Track(FacilityDefinition.Create(FacilityType.Smithy,
                new FacilityLevel { upgradeCost = new Cost(0), recipes = new List<CraftingRecipe> { new CraftingRecipe(sword, new Cost(10)) } }));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            squadConfig = Track(SquadConfig.Create(4, 10));
            upgrades = Track(EquipmentUpgradeSettings.Create(0.1f, 0.5f, 3, 3, 3));
            time = Track(PrepTimeSettings.Create(budgetHours: 3f, craftHours: 1f, upgradeHours: 1f, waitHours: 1f, scoutHours: 3f));
        }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        RunState NewRun(PrepTimeSettings settings)
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, 1000,
                new[] { new MaterialAmount(hide, 50) }, new[] { smithy }))
                .WithEconomy(Track(EconomyConfig.Create(0.5f))).WithEquipmentUpgrades(upgrades).WithPrepTime(settings);
            return RunState.CreateNew(config);
        }

        [Test]
        public void 시계는_쓴_만큼만_가고_예산을_넘겨_쓸_수_없다()
        {
            var clock = new PrepClock();
            clock.Begin(3f);
            float advanced = 0f;
            clock.Advanced += hours => advanced += hours;

            Assert.IsTrue(clock.TrySpend(1f));
            Assert.IsTrue(clock.TrySpend(1.5f));
            Assert.AreEqual(0.5f, clock.Remaining, 0.0001f);
            Assert.IsFalse(clock.TrySpend(1f), "남은 시간보다 많이 쓸 수 없다.");
            Assert.AreEqual(2.5f, clock.Used, 0.0001f, "실패하면 아무것도 바뀌지 않는다.");
            Assert.IsTrue(clock.TrySpend(0.5f));
            Assert.IsTrue(clock.IsExhausted);
            Assert.AreEqual(3f, advanced, 0.0001f);
            Assert.IsTrue(clock.TrySpend(0f), "0시간 행동은 시간이 없어도 된다.");

            clock.Begin(12f);
            Assert.AreEqual(12f, clock.Remaining, 0.0001f, "새 준비는 남은 시간을 넘기지 않고 예산을 새로 채운다.");
            clock.Restore(12f, 20f);
            Assert.AreEqual(12f, clock.Used, 0.0001f, "되살릴 때 쓴 시간은 예산을 넘지 않는다.");
        }

        [Test]
        public void 첫_준비에서_예산이_차고_제작과_강화가_시간을_쓰며_모자라면_아무것도_바꾸지_않는다()
        {
            RunState run = NewRun(time);
            Assert.IsTrue(run.BeginPreparationClockIfNeeded());
            Assert.IsFalse(run.BeginPreparationClockIfNeeded(), "같은 준비에서는 다시 채우지 않는다.");
            Assert.AreEqual(3f, run.Clock.Remaining, 0.0001f);

            ItemInstance made = run.TryCraft(sword);
            Assert.IsNotNull(made);
            Assert.AreEqual(2f, run.Clock.Remaining, 0.0001f, "제작은 1시간");
            Assert.IsTrue(run.TryUpgradeEquipment(made));
            Assert.AreEqual(1f, run.Clock.Remaining, 0.0001f, "강화는 1시간");
            Assert.IsTrue(run.TryWait());
            Assert.IsTrue(run.Clock.IsExhausted);

            int gold = run.Wallet.Gold, hides = run.Wallet.GetMaterial(hide), items = run.Inventory.Items.Count;
            Assert.AreEqual(CraftCheck.NotEnoughTime, run.CheckCraft(sword));
            Assert.IsNull(run.TryCraft(sword));
            Assert.AreEqual(EquipmentUpgradeCheck.NotEnoughTime, run.CheckUpgradeEquipment(made));
            Assert.IsFalse(run.TryUpgradeEquipment(made));
            Assert.IsFalse(run.CanWait);
            Assert.IsFalse(run.TryWait());
            Assert.AreEqual(gold, run.Wallet.Gold, "시간이 없으면 골드를 쓰지 않는다.");
            Assert.AreEqual(hides, run.Wallet.GetMaterial(hide));
            Assert.AreEqual(items, run.Inventory.Items.Count);
            Assert.AreEqual(1, made.UpgradeLevel);
        }

        [Test]
        public void 전투_중에는_시간을_쓸_수_없고_다음_준비에서_예산을_새로_채운다()
        {
            RunState run = NewRun(time);
            run.BeginPreparationClockIfNeeded();
            Assert.IsTrue(run.TryWait());
            Assert.AreEqual(2f, run.Clock.Remaining, 0.0001f);

            run.CreateBattleInput(1);
            Assert.IsFalse(run.CanWait, "전투 대기 중에는 기다릴 수 없다.");
            Assert.IsFalse(run.TrySpendPrepTime(1f));
            Assert.IsFalse(run.BeginPreparationClockIfNeeded(), "전투 대기 중에는 새 준비를 시작하지 않는다.");
            Assert.AreEqual(BattleResultIssue.None,
                run.Apply(new BattleResult(1, BattleOutcome.Victory, BattleEndReason.EnemiesDefeated)));

            Assert.IsTrue(run.BeginPreparationClockIfNeeded());
            Assert.AreEqual(3f, run.Clock.Remaining, 0.0001f, "남은 2시간은 사라지고 예산 3시간으로 다시 시작한다.");
            Assert.AreEqual(1, run.ClockPreparation);
        }

        [Test]
        public void 시간_설정이_없으면_시간_제약_없이_제작하고_기다리기는_없다()
        {
            RunState run = NewRun(null);
            Assert.IsFalse(run.BeginPreparationClockIfNeeded());
            for (int i = 0; i < 5; i++) Assert.IsNotNull(run.TryCraft(sword));
            Assert.IsFalse(run.CanWait);
            Assert.IsFalse(run.TryWait());
            Assert.IsTrue(run.TrySpendPrepTime(100f));
        }

        [Test]
        public void 저장하면_쓴_시간이_남고_예전_저장은_다음_준비에서_예산을_새로_채운다()
        {
            RunState run = NewRun(time);
            run.BeginPreparationClockIfNeeded();
            Assert.IsTrue(run.TryWait());
            Assert.IsTrue(run.TryWait());
            var content = Track(RunContentCatalog.Create(new[] { sword }, new[] { hide }, new[] { playerHero },
                new SoldierDefinition[0], new[] { smithy }, new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            var options = new RunRestoreOptions { SquadConfig = squadConfig, EquipmentUpgrades = upgrades, PrepTime = time };

            RunSnapshot saved = run.Capture(1, content);
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(0, restored.ClockPreparation);
            Assert.AreEqual(1f, restored.Clock.Remaining, 0.0001f, "되살린 준비는 쓴 2시간을 그대로 기억한다.");
            Assert.IsFalse(restored.BeginPreparationClockIfNeeded(), "같은 준비라 예산을 다시 채우지 않는다.");

            RunSnapshot old = run.Capture(1, content);
            old.prepTime = new PrepTimeRecord();
            RunState fromOld = RunState.Restore(old, content, options, out error);
            Assert.IsNotNull(fromOld, error);
            Assert.IsTrue(fromOld.BeginPreparationClockIfNeeded(), "시계 기록이 없는 예전 저장은 새로 채운다.");
            Assert.AreEqual(3f, fromOld.Clock.Remaining, 0.0001f);
        }
    }
}
