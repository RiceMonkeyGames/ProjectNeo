using System.Collections.Generic;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 준비 단계 건설·철거·수리 비용(D-2). 모든 값은 이 에셋에서 바꾼다. 현재 값은 프로토타입용 임의 값이다.
    /// 표에 없는 조각·무기는 무료다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Build Cost Table", fileName = "BuildCosts")]
    public sealed class BuildCostTable : ScriptableObject
    {
        [SerializeField] List<PieceGoldCost> pieces = new List<PieceGoldCost>();
        [SerializeField] List<WeaponGoldCost> weapons = new List<WeaponGoldCost>();
        [Tooltip("이번 준비 단계 전에 지은 조각·무기를 철거할 때 돌려받는 비율. 이번 준비에 지은 것은 전액이다.")]
        [SerializeField, Range(0f, 1f)] float olderRefundRatio = 0.5f;
        [Tooltip("깎인 체력 1을 수리하는 골드. 합계는 올림한다.")]
        [SerializeField, Min(0f)] float repairGoldPerHealth = 0.2f;

        public IReadOnlyList<PieceGoldCost> Pieces => pieces;
        public IReadOnlyList<WeaponGoldCost> Weapons => weapons;
        public float OlderRefundRatio => olderRefundRatio;
        public float RepairGoldPerHealth => repairGoldPerHealth;

        public int CostOf(BuildPieceDefinition piece)
        {
            foreach (PieceGoldCost entry in pieces) if (entry.piece == piece && piece != null) return Mathf.Max(0, entry.gold);
            return 0;
        }

        public int CostOf(SiegeWeaponDefinition weapon)
        {
            foreach (WeaponGoldCost entry in weapons) if (entry.weapon == weapon && weapon != null) return Mathf.Max(0, entry.gold);
            return 0;
        }

        /// <summary>코드에서 표를 만든다(테스트용).</summary>
        public static BuildCostTable Create(PieceGoldCost[] pieces = null, WeaponGoldCost[] weapons = null,
            float olderRefundRatio = 0.5f, float repairGoldPerHealth = 0.2f)
        {
            var table = CreateInstance<BuildCostTable>();
            table.pieces = new List<PieceGoldCost>(pieces ?? new PieceGoldCost[0]);
            table.weapons = new List<WeaponGoldCost>(weapons ?? new WeaponGoldCost[0]);
            table.olderRefundRatio = Mathf.Clamp01(olderRefundRatio);
            table.repairGoldPerHealth = Mathf.Max(0f, repairGoldPerHealth);
            return table;
        }
    }
}
