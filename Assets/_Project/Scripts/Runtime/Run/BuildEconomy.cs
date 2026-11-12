using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>수리할 조각 하나. 번호는 씬의 배치 번호다.</summary>
    public readonly struct RepairNeed
    {
        public int Id { get; }
        public float Missing { get; }
        public RepairNeed(int id, float missing) { Id = id; Missing = Math.Max(0f, missing); }
    }

    /// <summary>
    /// 준비 단계 건설·철거·수리를 런 지갑과 맞추는 규칙(D-2). 씬을 알지 않는다.
    /// 철거 환불은 이번 준비 단계에 지은 것은 전액, 그 전 것은 표의 비율(내림)이다(2026-11-12 사용자 결정).
    /// 번호는 씬 실행 중의 배치 번호·무기 번호를 쓴다.
    /// </summary>
    public sealed class BuildEconomy
    {
        readonly BuildCostTable table;
        readonly HashSet<int> piecesThisPreparation = new HashSet<int>();
        readonly HashSet<int> weaponsThisPreparation = new HashSet<int>();

        public Wallet Wallet { get; }
        public BuildCostTable Table => table;

        public BuildEconomy(BuildCostTable table, Wallet wallet)
        {
            this.table = table != null ? table : throw new ArgumentNullException(nameof(table));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        /// <summary>새 준비 단계가 시작되면 부른다. 이후 지은 것만 전액 환불 대상이다.</summary>
        public void BeginPreparation()
        {
            piecesThisPreparation.Clear();
            weaponsThisPreparation.Clear();
        }

        public bool CanAfford(BuildPieceDefinition piece) => Wallet.Gold >= table.CostOf(piece);
        public bool CanAfford(SiegeWeaponDefinition weapon) => Wallet.Gold >= table.CostOf(weapon);

        /// <summary>지은 조각의 비용을 낸다. 모자라면 아무것도 내지 않고 false.</summary>
        public bool PayForPiece(BuildPieceDefinition piece, int placementId)
        {
            if (!Wallet.TrySpend(new Cost(table.CostOf(piece)))) return false;
            piecesThisPreparation.Add(placementId);
            return true;
        }

        public bool PayForWeapon(SiegeWeaponDefinition weapon, int mountId)
        {
            if (!Wallet.TrySpend(new Cost(table.CostOf(weapon)))) return false;
            weaponsThisPreparation.Add(mountId);
            return true;
        }

        public bool BuiltThisPreparation(int placementId) => piecesThisPreparation.Contains(placementId);

        public int RefundFor(BuildPieceDefinition piece, int placementId) =>
            Refund(table.CostOf(piece), piecesThisPreparation.Contains(placementId));

        public int RefundFor(SiegeWeaponDefinition weapon, int mountId) =>
            Refund(table.CostOf(weapon), weaponsThisPreparation.Contains(mountId));

        /// <summary>철거한 조각의 환불을 지갑에 넣고 돌려준 골드를 알린다.</summary>
        public int RefundPiece(BuildPieceDefinition piece, int placementId)
        {
            int gold = RefundFor(piece, placementId);
            piecesThisPreparation.Remove(placementId);
            if (gold > 0) Wallet.AddGold(gold);
            return gold;
        }

        public int RefundWeapon(SiegeWeaponDefinition weapon, int mountId)
        {
            int gold = RefundFor(weapon, mountId);
            weaponsThisPreparation.Remove(mountId);
            if (gold > 0) Wallet.AddGold(gold);
            return gold;
        }

        int Refund(int cost, bool thisPreparation) =>
            thisPreparation ? cost : Mathf.FloorToInt(cost * table.OlderRefundRatio);

        /// <summary>깎인 체력을 수리하는 골드(올림).</summary>
        public int RepairCost(float missing) => missing <= 0f ? 0 : Mathf.CeilToInt(missing * table.RepairGoldPerHealth - 0.0001f);

        /// <summary>
        /// 가진 골드로 수리할 조각을 고른다. 가장 많이 깎인 조각부터 하나씩 완전히 수리하며, 비용이 모자란 조각은
        /// 건너뛰고 더 싼 조각을 계속 본다. 지갑은 바꾸지 않는다.
        /// </summary>
        public List<RepairNeed> PlanRepair(IEnumerable<RepairNeed> needs, out int totalCost)
        {
            var sorted = new List<RepairNeed>();
            foreach (RepairNeed need in needs) if (need.Missing > 0f) sorted.Add(need);
            sorted.Sort((a, b) => b.Missing.CompareTo(a.Missing));
            var chosen = new List<RepairNeed>();
            totalCost = 0;
            int budget = Wallet.Gold;
            foreach (RepairNeed need in sorted)
            {
                int cost = RepairCost(need.Missing);
                if (totalCost + cost > budget) continue;
                totalCost += cost;
                chosen.Add(need);
            }
            return chosen;
        }

        /// <summary>모두 수리하는 데 드는 골드(가진 골드와 상관없이).</summary>
        public int FullRepairCost(IEnumerable<RepairNeed> needs)
        {
            int total = 0;
            foreach (RepairNeed need in needs) total += RepairCost(need.Missing);
            return total;
        }
    }
}
