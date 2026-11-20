using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 승천 단계 표(FLOW-03, Q-78). 첫 줄이 승천 1이고, 단계 N은 1~N줄의 규칙을 모두 쓴다. 0은 기본 게임이다.
    /// 줄 수가 최고 단계다. 점수·해금 포인트 배율은 1 + 단계 × <see cref="RewardMultiplierPerLevel"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Run/Ascension Table", fileName = "Ascension")]
    public sealed class AscensionTable : ScriptableObject
    {
        [Tooltip("첫 줄이 승천 1이다. 높은 단계는 낮은 단계의 규칙을 모두 함께 쓴다.")]
        [SerializeField] List<AscensionLevel> levels = new List<AscensionLevel>();
        [Tooltip("단계마다 점수와 해금 포인트에 더하는 배율. 0.1이면 승천 10은 x2.0이다.")]
        [SerializeField, Min(0f)] float rewardMultiplierPerLevel = 0.1f;

        public IReadOnlyList<AscensionLevel> Levels => levels;
        /// <summary>고를 수 있는 가장 높은 단계.</summary>
        public int MaxLevel => levels.Count;
        public float RewardMultiplierPerLevel => rewardMultiplierPerLevel;

        /// <summary>단계를 0 ~ <see cref="MaxLevel"/> 안으로 맞춘다.</summary>
        public int Clamp(int level) => Mathf.Clamp(level, 0, MaxLevel);

        /// <summary>단계 N(1부터)에서 새로 붙는 규칙. 범위 밖이면 null.</summary>
        public AscensionLevel LevelAt(int level) => level >= 1 && level <= levels.Count ? levels[level - 1] : null;

        /// <summary>단계 N까지 쌓인 규칙을 한 줄씩(예: "A1 Enemy HP +15%"). 단계 0이면 빈 목록.</summary>
        public List<string> DescribeUpTo(int level)
        {
            var lines = new List<string>();
            for (int i = 1; i <= Clamp(level); i++)
            {
                AscensionLevel step = LevelAt(i);
                lines.Add($"A{i} " + (step != null && !string.IsNullOrEmpty(step.description) ? step.description : "-"));
            }
            return lines;
        }

        public static AscensionTable Create(float rewardMultiplierPerLevel, params AscensionLevel[] levels)
        {
            var table = CreateInstance<AscensionTable>();
            table.rewardMultiplierPerLevel = Mathf.Max(0f, rewardMultiplierPerLevel);
            table.levels = new List<AscensionLevel>(levels ?? new AscensionLevel[0]);
            return table;
        }
    }
}
