using UnityEngine;

namespace ProjectNeo.Economy
{
    /// <summary>거래 전반에 쓰는 수치.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] float sellRatio = 0.5f;

        /// <summary>판매가 = 구매가의 골드 × 이 비율.</summary>
        public float SellRatio => sellRatio;

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static EconomyConfig Create(float sellRatio)
        {
            var config = CreateInstance<EconomyConfig>();
            config.sellRatio = sellRatio;
            return config;
        }
    }
}
