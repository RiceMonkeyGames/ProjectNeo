using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class BuildEconomyTests
    {
        BuildPieceDefinition wall;
        BuildPieceDefinition stairs;
        BuildPieceDefinition gate;
        SiegeWeaponDefinition cannon;
        Wallet wallet;
        BuildEconomy economy;

        [SetUp]
        public void SetUp()
        {
            wall = BuildPieceDefinition.Create(BuildPieceKind.Wall, "wall", Vector2Int.one);
            stairs = BuildPieceDefinition.Create(BuildPieceKind.Stairs, "stairs", new Vector2Int(1, 2));
            gate = BuildPieceDefinition.Create(BuildPieceKind.Gate, "gate", new Vector2Int(2, 1));
            cannon = SiegeWeaponDefinition.Create("cannon", "cannon", Vector3.one);
            var table = BuildCostTable.Create(new[] { new PieceGoldCost(wall, 10), new PieceGoldCost(stairs, 15) },
                new[] { new WeaponGoldCost(cannon, 30) }, 0.5f, 0.2f);
            wallet = new Wallet();
            wallet.AddGold(50);
            economy = new BuildEconomy(table, wallet);
        }

        [Test]
        public void 표에_있는_조각과_무기만_비용이_있고_없는_것은_무료다()
        {
            Assert.AreEqual(10, economy.Table.CostOf(wall));
            Assert.AreEqual(15, economy.Table.CostOf(stairs));
            Assert.AreEqual(30, economy.Table.CostOf(cannon));
            Assert.AreEqual(0, economy.Table.CostOf(gate));
            Assert.AreEqual(0, economy.Table.CostOf((BuildPieceDefinition)null));
        }

        [Test]
        public void 비용을_내고_모자라면_아무것도_내지_않는다()
        {
            Assert.IsTrue(economy.PayForWeapon(cannon, 1));
            Assert.AreEqual(20, wallet.Gold);
            Assert.IsTrue(economy.CanAfford(stairs));
            Assert.IsFalse(economy.CanAfford(cannon));
            Assert.IsFalse(economy.PayForWeapon(cannon, 2));
            Assert.AreEqual(20, wallet.Gold);
            Assert.IsTrue(economy.PayForPiece(stairs, 7));
            Assert.AreEqual(5, wallet.Gold);
            Assert.IsTrue(economy.BuiltThisPreparation(7));
        }

        [Test]
        public void 이번_준비에_지은_것은_전액_그_전_것은_비율만큼_돌려준다()
        {
            economy.PayForPiece(wall, 1);
            economy.PayForWeapon(cannon, 1);
            Assert.AreEqual(10, economy.RefundFor(wall, 1));
            Assert.AreEqual(30, economy.RefundFor(cannon, 1));

            economy.BeginPreparation();
            Assert.IsFalse(economy.BuiltThisPreparation(1));
            Assert.AreEqual(5, economy.RefundFor(wall, 1), "지난 판에 지은 것은 50%");
            Assert.AreEqual(15, economy.RefundFor(cannon, 1));
            Assert.AreEqual(7, economy.RefundFor(stairs, 99), "처음부터 있던 조각도 같은 비율, 내림");

            int before = wallet.Gold;
            Assert.AreEqual(5, economy.RefundPiece(wall, 1));
            Assert.AreEqual(before + 5, wallet.Gold);
        }

        [Test]
        public void 철거하면_이번_준비_기록에서_빠진다()
        {
            economy.PayForPiece(wall, 3);
            Assert.AreEqual(10, economy.RefundPiece(wall, 3));
            Assert.IsFalse(economy.BuiltThisPreparation(3));
        }

        [Test]
        public void 수리_비용은_깎인_체력에_비례하고_올림한다()
        {
            Assert.AreEqual(0, economy.RepairCost(0f));
            Assert.AreEqual(2, economy.RepairCost(10f));
            Assert.AreEqual(3, economy.RepairCost(10.5f));
            Assert.AreEqual(1, economy.RepairCost(0.1f));
        }

        [Test]
        public void 모두_수리는_가장_많이_깎인_조각부터_가진_골드만큼_고르고_지갑은_바꾸지_않는다()
        {
            var needs = new List<RepairNeed>
            {
                new RepairNeed(1, 50f),   // 10골드
                new RepairNeed(2, 200f),  // 40골드
                new RepairNeed(3, 25f),   // 5골드
                new RepairNeed(4, 0f),
            };
            Assert.IsTrue(wallet.TrySpend(new Cost(3)));
            Assert.AreEqual(55, economy.FullRepairCost(needs));
            List<RepairNeed> chosen = economy.PlanRepair(needs, out int cost);
            CollectionAssert.AreEqual(new[] { 2, 3 }, chosen.ConvertAll(need => need.Id),
                "40골드짜리 다음 10골드는 모자라 건너뛰고 5골드짜리를 고른다.");
            Assert.AreEqual(45, cost);
            Assert.AreEqual(47, wallet.Gold);
        }
    }
}
