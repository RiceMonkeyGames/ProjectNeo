using NUnit.Framework;
using ProjectNeo.Economy;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class WalletTests
    {
        Wallet wallet;
        MaterialDefinition fang;
        MaterialDefinition hide;

        [SetUp]
        public void SetUp()
        {
            wallet = new Wallet();
            fang = MaterialDefinition.Create("fang");
            hide = MaterialDefinition.Create("hide");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fang);
            Object.DestroyImmediate(hide);
        }

        [Test]
        public void DefaultCost_IsFree_AndAlwaysAffordable()
        {
            Cost free = default;

            Assert.IsTrue(free.IsFree);
            Assert.IsTrue(wallet.TrySpend(free));
        }

        [Test]
        public void TrySpend_GoldOnly_DeductsGold()
        {
            wallet.AddGold(100);

            Assert.IsTrue(wallet.TrySpend(new Cost(30)));
            Assert.AreEqual(70, wallet.Gold);
        }

        [Test]
        public void TrySpend_MaterialOnly_DeductsMaterial()
        {
            wallet.AddMaterial(fang, 5);

            Assert.IsTrue(wallet.TrySpend(new Cost(0, new MaterialAmount(fang, 3))));
            Assert.AreEqual(2, wallet.GetMaterial(fang));
        }

        [Test]
        public void TrySpend_GoldAndMaterial_DeductsBoth()
        {
            wallet.AddGold(50);
            wallet.AddMaterial(fang, 2);

            Assert.IsTrue(wallet.TrySpend(new Cost(50, new MaterialAmount(fang, 2))));
            Assert.AreEqual(0, wallet.Gold);
            Assert.AreEqual(0, wallet.GetMaterial(fang));
        }

        [Test]
        public void TrySpend_WhenMaterialIsShort_DeductsNothing()
        {
            wallet.AddGold(100);
            wallet.AddMaterial(fang, 1);
            var cost = new Cost(40, new MaterialAmount(fang, 1), new MaterialAmount(hide, 1));

            Assert.IsFalse(wallet.CanAfford(cost));
            Assert.IsFalse(wallet.TrySpend(cost));
            Assert.AreEqual(100, wallet.Gold);
            Assert.AreEqual(1, wallet.GetMaterial(fang));
        }

        [Test]
        public void CanAfford_SumsDuplicateMaterialRows()
        {
            wallet.AddMaterial(fang, 3);
            var cost = new Cost(0, new MaterialAmount(fang, 2), new MaterialAmount(fang, 2));

            Assert.IsFalse(wallet.CanAfford(cost));
        }

        [Test]
        public void Add_NegativeAmount_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => wallet.AddGold(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => wallet.AddMaterial(fang, -1));
        }
    }
}
