using UnityEngine;

namespace ProjectNeo.Economy
{
    /// <summary>거래 전반에 쓰는 수치.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] float sellRatio = 0.5f;

        [Header("상점 리롤 (PREP-02)")]
        [SerializeField, Min(0)] int rerollBaseCost = 10;
        [SerializeField, Min(0)] int rerollCostStep = 10;

        [Header("주점 리롤 (PREP-12)")]
        [SerializeField, Min(0)] int tavernRerollBaseCost = 10;
        [SerializeField, Min(0)] int tavernRerollCostStep = 10;

        /// <summary>판매가 = 구매가의 골드 × 이 비율.</summary>
        public float SellRatio => sellRatio;
        /// <summary>준비 단계의 첫 리롤 골드.</summary>
        public int RerollBaseCost => rerollBaseCost;
        /// <summary>같은 준비 단계에서 리롤할 때마다 오르는 골드.</summary>
        public int RerollCostStep => rerollCostStep;

        /// <summary>
        /// 이번 준비 단계에서 이미 rerollsDone번 리롤했을 때 다음 리롤의 골드(Q-46).
        /// 같은 폭으로 계속 오르고 상한·횟수 제한은 없다(예: 10, 20, 30, 40…).
        /// </summary>
        public int GetRerollCost(int rerollsDone) =>
            Mathf.Max(0, rerollBaseCost) + Mathf.Max(0, rerollCostStep) * Mathf.Max(0, rerollsDone);

        /// <summary>주점 준비 단계의 첫 리롤 골드(PREP-12).</summary>
        public int TavernRerollBaseCost => tavernRerollBaseCost;
        /// <summary>같은 준비 단계에서 주점을 리롤할 때마다 오르는 골드(PREP-12).</summary>
        public int TavernRerollCostStep => tavernRerollCostStep;

        /// <summary>
        /// 이번 준비 단계에서 주점을 이미 rerollsDone번 리롤했을 때 다음 리롤의 골드(PREP-12, 상점 Q-46과 같은 규칙).
        /// </summary>
        public int GetTavernRerollCost(int rerollsDone) =>
            Mathf.Max(0, tavernRerollBaseCost) + Mathf.Max(0, tavernRerollCostStep) * Mathf.Max(0, rerollsDone);

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static EconomyConfig Create(float sellRatio, int rerollBaseCost = 10, int rerollCostStep = 10,
            int tavernRerollBaseCost = 10, int tavernRerollCostStep = 10)
        {
            var config = CreateInstance<EconomyConfig>();
            config.sellRatio = sellRatio;
            config.rerollBaseCost = rerollBaseCost;
            config.rerollCostStep = rerollCostStep;
            config.tavernRerollBaseCost = tavernRerollBaseCost;
            config.tavernRerollCostStep = tavernRerollCostStep;
            return config;
        }
    }
}
