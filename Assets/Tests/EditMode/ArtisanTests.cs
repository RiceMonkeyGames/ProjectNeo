using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PREP-11 장인: 대장간 레벨 상한과 고용, 시설 강화 시간, 장인을 쓰는 시계 걸음, 작업 우선순위, 런 저장.</summary>
    public class ArtisanTests
    {
        readonly List<Object> created = new List<Object>();
        HeroDefinition playerHero;
        ItemDefinition sword;
        FacilityDefinition smithy;
        FacilityDefinition shop;
        SquadConfig squadConfig;
        PrepTimeSettings time;

        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        static WorkOrder Build(int key, float hours = 1f) => new WorkOrder(WorkKind.Build, WorkTarget.Piece, key, hours);

        [SetUp]
        public void SetUp()
        {
            sword = Track(ItemDefinition.CreateEquipment("sword", EquipmentSlot.Weapon, new Cost(40), StatModifier.Flat(StatType.Attack, 10f)));
            var recipes = new List<CraftingRecipe> { new CraftingRecipe(sword, new Cost(10)) };
            smithy = Track(FacilityDefinition.Create(FacilityType.Smithy,
                new FacilityLevel { upgradeCost = new Cost(0), recipes = recipes },
                new FacilityLevel { upgradeCost = new Cost(20), recipes = recipes },
                new FacilityLevel { upgradeCost = new Cost(20), recipes = recipes }));
            shop = Track(FacilityDefinition.Create(FacilityType.Shop,
                new FacilityLevel { upgradeCost = new Cost(0) }, new FacilityLevel { upgradeCost = new Cost(30) }));
            playerHero = Track(HeroDefinition.Create("player", true, 3, new StatValue(StatType.Attack, 20f)));
            squadConfig = Track(SquadConfig.Create(4, 10));
            time = Track(PrepTimeSettings.Create(budgetHours: 12f)).WithArtisans(2, new[] { 3, 4, 5 }, new[] { 50, 100, 150 }, 1f);
        }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        RunState NewRun(int gold, params FacilityDefinition[] facilities)
        {
            RunConfig config = Track(RunConfig.Create(squadConfig, playerHero, gold, new MaterialAmount[0], facilities))
                .WithEconomy(Track(EconomyConfig.Create(0.5f))).WithPrepTime(time);
            return RunState.CreateNew(config);
        }

        [Test]
        public void 대장간_레벨이_상한을_열고_고용은_골드를_낸다()
        {
            RunState run = NewRun(1000, smithy);
            Assert.AreEqual(2, run.ArtisanCount, "런은 시작 장인 2명으로 시작한다.");
            Assert.AreEqual(3, run.ArtisanCap, "대장간 Lv1 상한은 3명이다.");
            Assert.AreEqual(50, run.NextArtisanHireGold);

            Assert.IsTrue(run.TryHireArtisan());
            Assert.AreEqual(3, run.ArtisanCount);
            Assert.AreEqual(950, run.Wallet.Gold, "첫 고용은 50골드다.");
            Assert.AreEqual(0f, run.Clock.Used, 0.0001f, "고용은 시간을 쓰지 않는다.");
            Assert.AreEqual(ArtisanHireCheck.AtCap, run.CheckHireArtisan());
            Assert.IsFalse(run.TryHireArtisan());

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            Assert.AreEqual(4, run.ArtisanCap, "대장간 Lv2는 자리 하나만 연다.");
            Assert.AreEqual(3, run.ArtisanCount, "강화만으로 장인이 늘지는 않는다.");
            Assert.AreEqual(100, run.NextArtisanHireGold);
            Assert.IsTrue(run.TryHireArtisan());
            Assert.AreEqual(4, run.ArtisanCount);
            Assert.AreEqual(1000 - 50 - 20 - 100, run.Wallet.Gold);
        }

        [Test]
        public void 골드가_모자라거나_대장간이_없으면_고용할_수_없다()
        {
            RunState poor = NewRun(30, smithy);
            Assert.AreEqual(ArtisanHireCheck.NotEnoughFunds, poor.CheckHireArtisan());
            Assert.IsFalse(poor.TryHireArtisan());
            Assert.AreEqual(30, poor.Wallet.Gold);
            Assert.AreEqual(2, poor.ArtisanCount);

            RunState noSmithy = NewRun(1000);
            Assert.AreEqual(2, noSmithy.ArtisanCap, "대장간이 없으면 시작 장인 수가 상한이다.");
            Assert.AreEqual(ArtisanHireCheck.AtCap, noSmithy.CheckHireArtisan());
        }

        [Test]
        public void 시설_강화는_한_시간과_장인_하나를_쓰고_시간이_모자라면_골드를_쓰지_않는다()
        {
            RunState run = NewRun(1000, smithy, shop);
            var steps = new List<PrepClockStep>();
            run.BeginPreparationClockIfNeeded();
            run.Clock.Stepped += steps.Add;

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Shop));
            Assert.AreEqual(1f, run.Clock.Used, 0.0001f);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(1, steps[0].OccupiedArtisans, "시설 강화는 장인 1명을 쓴다.");

            Assert.IsNotNull(run.TryCraft(sword));
            Assert.AreEqual(1, steps[1].OccupiedArtisans, "대장간 제작도 장인 1명을 쓴다.");
            Assert.IsTrue(run.TryWait());
            Assert.AreEqual(0, steps[2].OccupiedArtisans, "기다리기는 장인을 쓰지 않는다.");

            run.Clock.Restore(run.Clock.Budget, run.Clock.Budget - 0.5f);
            int gold = run.Wallet.Gold;
            Assert.AreEqual(FacilityUpgradeCheck.NotEnoughTime, run.CheckUpgradeFacility(FacilityType.Smithy));
            Assert.IsFalse(run.TryUpgradeFacility(FacilityType.Smithy));
            Assert.AreEqual(gold, run.Wallet.Gold, "시간이 모자라면 골드를 쓰지 않는다.");
            Assert.AreEqual(0, run.Smithy.Level);
        }

        [Test]
        public void 장인이_빠진_걸음에는_뒤쪽_자리가_쉰다()
        {
            var queue = new WorkQueue(3);
            WorkOrder a = Build(1), b = Build(2), c = Build(3), d = Build(4);
            queue.Add(a); queue.Add(b); queue.Add(c); queue.Add(d);
            Assert.AreEqual(3, queue.AssignedCount);
            Assert.AreEqual(1, queue.WaitingCount);
            Assert.IsTrue(queue.IsAssigned(c));
            Assert.IsFalse(queue.IsAssigned(d), "장인 3명이면 넷째 주문은 기다린다.");

            queue.Advance(0.5f, new PrepClockStep(0.5f, 1).FreeArtisans(3));
            Assert.AreEqual(0.5f, a.Done, 0.0001f);
            Assert.AreEqual(0.5f, b.Done, 0.0001f);
            Assert.AreEqual(0f, c.Done, 0.0001f, "장인 하나가 빠지면 자리를 맡은 주문 중 맨 뒤가 쉰다.");

            queue.Advance(0.5f);
            Assert.IsTrue(a.IsComplete && b.IsComplete);
            Assert.AreEqual(0.5f, c.Done, 0.0001f, "기다리기는 모든 장인이 일한다.");
            queue.Advance(1f, 0);
            Assert.AreEqual(0.5f, c.Done, 0.0001f, "일할 장인이 없으면 진행하지 않는다.");
        }

        [Test]
        public void 우선순위를_바꾸면_대기_주문이_자리를_맡는다()
        {
            var queue = new WorkQueue(2);
            WorkOrder a = Build(1), b = Build(2), c = Build(3);
            queue.Add(a); queue.Add(b); queue.Add(c);

            Assert.IsTrue(queue.MoveToFront(c));
            CollectionAssert.AreEqual(new[] { c, a, b }, queue.Orders);
            Assert.IsTrue(queue.IsAssigned(c));
            Assert.IsFalse(queue.IsAssigned(b));
            Assert.IsFalse(queue.MoveToFront(c), "이미 맨 앞이다.");

            Assert.IsTrue(queue.MoveDown(c));
            CollectionAssert.AreEqual(new[] { a, c, b }, queue.Orders);
            Assert.IsTrue(queue.MoveUp(b));
            CollectionAssert.AreEqual(new[] { a, b, c }, queue.Orders);
            Assert.IsFalse(queue.MoveDown(c), "맨 뒤는 더 내려갈 수 없다.");

            queue.Advance(1f);
            Assert.IsTrue(a.IsComplete);
            Assert.IsFalse(queue.MoveUp(b), "끝난 주문은 건너뛰어 b가 이미 맨 앞이다.");
            Assert.IsFalse(queue.MoveUp(a), "끝난 주문은 옮기지 않는다.");
        }

        [Test]
        public void 고용한_장인은_저장에_남고_예전_저장은_시작_장인만_쓴다()
        {
            RunState run = NewRun(1000, smithy);
            Assert.IsTrue(run.TryHireArtisan());
            var content = Track(RunContentCatalog.Create(new[] { sword }, new MaterialDefinition[0], new[] { playerHero },
                new SoldierDefinition[0], new[] { smithy }, new BuildPieceDefinition[0], new SiegeWeaponDefinition[0]));
            var options = new RunRestoreOptions { SquadConfig = squadConfig, PrepTime = time };

            RunSnapshot saved = run.Capture(1, content);
            RunState restored = RunState.Restore(saved, content, options, out string error);
            Assert.IsNotNull(restored, error);
            Assert.AreEqual(3, restored.ArtisanCount);
            Assert.AreEqual(100, restored.NextArtisanHireGold);

            saved.hiredArtisans = 0;
            RunState fromOld = RunState.Restore(saved, content, options, out error);
            Assert.IsNotNull(fromOld, error);
            Assert.AreEqual(2, fromOld.ArtisanCount, "장인 기록이 없는 예전 저장은 시작 장인 수로 읽는다.");
        }
    }
}
