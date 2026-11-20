using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 영웅 등급·승급 수치(PREP-10, Q-73). 승리마다 주는 경험치, 등급별 필요 경험치·비용, 승급 시간, 등급마다 붙는 능력치.
    /// 플레이 후 조절할 시작값이며 코드 없이 에셋에서 바꾼다(사용자 결정 2026-11-20).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Run/Hero Promotion Settings", fileName = "HeroPromotion")]
    public sealed class HeroPromotionSettings : ScriptableObject
    {
        [Tooltip("승리한 전투에 대장으로 나가 살아남은 영웅이 받는 경험치. 대기 영웅과 플레이어는 받지 않는다.")]
        [SerializeField, Min(0)] int expPerVictory = 100;
        [Tooltip("승급 단계. 0번이 1→2등급, 1번이 2→3등급이다. 단계 수 + 1이 최고 등급이다.")]
        [SerializeField] List<HeroGradeStep> steps = new List<HeroGradeStep>();
        [Tooltip("승급 한 번에 쓰는 준비 시간(Q-53). 0이면 시간을 쓰지 않는다.")]
        [SerializeField, Min(0f)] float promotionHours = 1f;
        [Tooltip("1등급 위로 한 등급마다 붙는 능력치. 예: 체력 +15%, 공격 +15%이면 3등급은 +30%다.")]
        [SerializeField] List<StatModifier> bonusPerGrade = new List<StatModifier>();

        public int ExpPerVictory => expPerVictory;
        public IReadOnlyList<HeroGradeStep> Steps => steps;
        public float PromotionHours => promotionHours;
        public IReadOnlyList<StatModifier> BonusPerGrade => bonusPerGrade;
        /// <summary>최고 등급. 단계가 없으면 1이다.</summary>
        public int MaxGrade => steps.Count + 1;

        /// <summary>지금 등급에서 다음 등급으로 가는 단계. 최고 등급이면 false.</summary>
        public bool TryGetStep(int currentGrade, out HeroGradeStep step)
        {
            int index = currentGrade - 1;
            if (index < 0 || index >= steps.Count) { step = default; return false; }
            step = steps[index];
            return true;
        }

        /// <summary>코드에서 설정을 만든다(테스트용·에디터 설치용).</summary>
        public static HeroPromotionSettings Create(int expPerVictory, float promotionHours, HeroGradeStep[] steps,
            params StatModifier[] bonusPerGrade)
        {
            var value = CreateInstance<HeroPromotionSettings>();
            value.expPerVictory = Mathf.Max(0, expPerVictory);
            value.promotionHours = Mathf.Max(0f, promotionHours);
            value.steps = new List<HeroGradeStep>(steps ?? new HeroGradeStep[0]);
            value.bonusPerGrade = new List<StatModifier>(bonusPerGrade ?? new StatModifier[0]);
            return value;
        }

        /// <summary>사용자가 정한 시작값(2026-11-20): 승리 100, 2등급 200·100골드, 3등급 400·250골드, 1시간, 체력·공격 +15%.</summary>
        public static HeroPromotionSettings CreateDefault() => Create(100, 1f,
            new[] { new HeroGradeStep(200, new Cost(100)), new HeroGradeStep(400, new Cost(250)) },
            StatModifier.Percent(StatType.MaxHealth, 0.15f), StatModifier.Percent(StatType.Attack, 0.15f));
    }
}
