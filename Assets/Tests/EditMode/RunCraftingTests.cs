using System.Linq;
using NUnit.Framework;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;

namespace ProjectNeo.Tests
{
    /// <summary>D-6 대장간 고정 레시피 제작과 장비 판매의 런 규칙(Q-35).</summary>
    public class RunCraftingTests
    {
        ItemDefinition sword, armor, steel, potion;
        MaterialDefinition hide;
        FacilityDefinition smithy;

        [SetUp]
        public void SetUp()
        {
            hide = MaterialDefinition.Create("hide");
            sword = ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40));
            armor = ItemDefinition.Create("armor", ItemKind.Equipment, new Cost(30));
            steel = ItemDefinition.Create("steel", ItemKind.Equipment, new Cost(80), false);
            potion = ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10));
            smithy = FacilityDefinition.Create(FacilityType.Smithy, "대장간",
                Level(0, new CraftingRecipe(sword, new Cost(30, new MaterialAmount(hide, 2)))),
                Level(50, new CraftingRecipe(sword, new Cost(30, new MaterialAmount(hide, 2))),
                    new CraftingRecipe(armor, new Cost(20)),
                    new CraftingRecipe(steel, new Cost(60))));
        }

        static FacilityLevel Level(int gold, params CraftingRecipe[] recipes) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            recipes = recipes.ToList(),
        };

        RunState NewRun(int gold = 200, int hides = 5)
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            run.AddFacility(smithy);
            run.SetupShop(EconomyConfig.Create(0.5f));
            run.Wallet.AddGold(gold);
            if (hides > 0) run.Wallet.AddMaterial(hide, hides);
            return run;
        }

        [Test]
        public void 레시피는_현재_대장간_레벨_목록이고_업그레이드하면_늘어난다()
        {
            RunState run = NewRun();
            CollectionAssert.AreEqual(new[] { sword }, run.CraftableRecipes().Select(r => r.item));
            Assert.AreEqual(CraftCheck.NotCraftable, run.CheckCraft(armor));

            Assert.IsTrue(run.TryUpgradeFacility(FacilityType.Smithy));
            CollectionAssert.AreEqual(new[] { sword, armor, steel }, run.CraftableRecipes().Select(r => r.item));
        }

        [Test]
        public void 해금되지_않은_장비는_목록에서_빠지고_만들_수_없다()
        {
            RunState run = NewRun();
            run.TryUpgradeFacility(FacilityType.Smithy);
            System.Func<ItemDefinition, bool> unlocked = item => item.UnlockedByDefault;
            CollectionAssert.AreEqual(new[] { sword, armor }, run.CraftableRecipes(unlocked).Select(r => r.item));
            Assert.AreEqual(CraftCheck.NotCraftable, run.CheckCraft(steel, unlocked));
            Assert.IsNull(run.TryCraft(steel, unlocked));
        }

        [Test]
        public void 제작하면_골드와_재료를_내고_보관함에_들어가며_레시피는_남는다()
        {
            RunState run = NewRun();
            int changed = 0;
            run.Changed += () => changed++;

            ItemInstance crafted = run.TryCraft(sword);

            Assert.IsNotNull(crafted);
            Assert.AreSame(sword, crafted.Definition);
            Assert.IsTrue(run.Inventory.Contains(crafted));
            Assert.AreEqual(170, run.Wallet.Gold);
            Assert.AreEqual(3, run.Wallet.GetMaterial(hide));
            Assert.Greater(changed, 0);
            Assert.IsNotNull(run.TryCraft(sword), "고정 레시피라 같은 장비를 다시 만들 수 있다.");
            Assert.AreEqual(2, run.Inventory.Items.Count);
        }

        [Test]
        public void 골드나_재료가_모자라면_만들지_않고_런이_그대로다()
        {
            RunState poorGold = NewRun(gold: 10);
            Assert.AreEqual(CraftCheck.NotEnoughFunds, poorGold.CheckCraft(sword));
            Assert.IsNull(poorGold.TryCraft(sword));
            Assert.AreEqual(10, poorGold.Wallet.Gold);
            Assert.AreEqual(5, poorGold.Wallet.GetMaterial(hide));

            RunState poorHide = NewRun(hides: 1);
            Assert.AreEqual(CraftCheck.NotEnoughFunds, poorHide.CheckCraft(sword));
            Assert.IsNull(poorHide.TryCraft(sword));
            Assert.AreEqual(200, poorHide.Wallet.Gold);
            Assert.AreEqual(0, poorHide.Inventory.Items.Count);
        }

        [Test]
        public void 전투_입력이_나간_뒤에는_만들_수_없다()
        {
            RunState run = NewRun();
            run.CreateBattleInput(1);
            Assert.AreEqual(CraftCheck.BattlePending, run.CheckCraft(sword));
            Assert.IsNull(run.TryCraft(sword));
        }

        [Test]
        public void 대장간이_없으면_레시피가_없다()
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            run.Wallet.AddGold(100);
            Assert.AreEqual(0, run.CraftableRecipes().Count);
            Assert.AreEqual(CraftCheck.NotCraftable, run.CheckCraft(sword));
        }

        [Test]
        public void 대장간은_장비만_상점과_같은_가격으로_판다()
        {
            RunState run = NewRun(gold: 0);
            ItemInstance blade = run.Inventory.Add(sword);
            ItemInstance drink = run.Inventory.Add(potion);

            Assert.IsFalse(run.TrySellAtSmithy(drink), "소모품은 대장간에서 팔지 않는다.");
            Assert.IsTrue(run.Inventory.Contains(drink));
            Assert.IsTrue(run.TrySellAtSmithy(blade));
            Assert.AreEqual(run.Shop.GetSellPrice(sword), run.Wallet.Gold);
            Assert.AreEqual(20, run.Wallet.Gold);
            Assert.IsFalse(run.Inventory.Contains(blade));
        }
    }
}
