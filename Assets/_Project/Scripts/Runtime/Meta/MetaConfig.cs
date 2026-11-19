using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>메타 진행에 쓰는 수치. 해금 비용은 해금 트랙 항목마다 따로 있다(<see cref="UnlockEntry.Cost"/>).</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Meta Config", fileName = "MetaConfig")]
    public sealed class MetaConfig : ScriptableObject
    {
        [Header("지휘관 레벨")]
        [SerializeField, Min(1)] int baseXpPerLevel = 100;
        [SerializeField, Min(0)] int xpGrowthPerLevel = 50;
        [SerializeField, Min(0)] int perkPointsPerLevel = 1;

        [Header("환생 보너스")]
        [SerializeField, Min(0f)] float runScoreToXpRate = 0.1f;

        public int PerkPointsPerLevel => perkPointsPerLevel;
        /// <summary>런이 끝날 때 런 점수에 곱해 지휘관 경험치로 바꾸는 비율.</summary>
        public float RunScoreToXpRate => runScoreToXpRate;

        /// <summary>이 레벨에서 다음 레벨로 가는 데 필요한 경험치.</summary>
        public int GetXpToNextLevel(int level)
        {
            return baseXpPerLevel + xpGrowthPerLevel * Mathf.Max(level - 1, 0);
        }

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static MetaConfig Create(int baseXpPerLevel, int xpGrowthPerLevel, int perkPointsPerLevel, float runScoreToXpRate)
        {
            var config = CreateInstance<MetaConfig>();
            config.baseXpPerLevel = baseXpPerLevel;
            config.xpGrowthPerLevel = xpGrowthPerLevel;
            config.perkPointsPerLevel = perkPointsPerLevel;
            config.runScoreToXpRate = runScoreToXpRate;
            return config;
        }
    }
}
