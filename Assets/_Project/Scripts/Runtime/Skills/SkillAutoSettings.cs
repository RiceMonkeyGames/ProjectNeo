using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>영웅 AI 자동 시전 판단 수치(Q-63, COMBAT-06, COMBAT-13, COMBAT-18).</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Skill Auto Cast", fileName = "SkillAuto")]
    public sealed class SkillAutoSettings : ScriptableObject
    {
        [Tooltip("몇 초마다 스킬 쓸지 판단하는지. 한 번 판단에 스킬은 하나만 쓴다.")]
        [SerializeField, Min(0.05f)] float thinkInterval = 0.5f;
        [Tooltip("적에게 거는 범위 스킬(자기 주변·조준 지점)을 쓰려면 범위 안에 있어야 하는 적 수.")]
        [SerializeField, Min(1)] int minimumEnemiesInArea = 2;
        [Tooltip("회복 스킬은 범위 안 아군 중 체력 비율이 이 값 이하인 유닛이 있을 때 쓴다.")]
        [SerializeField, Range(0f, 1f)] float healBelowHealthRatio = 0.6f;
        [Tooltip("버프·사기 스킬은 적이 이 거리(m) 안에 있을 때(교전 중) 쓴다.")]
        [SerializeField, Min(0f)] float engageDistance = 12f;
        [Tooltip("사기만 올리는 스킬은 대상 분대 중 가장 낮은 사기가 최대 사기의 이 비율 이하일 때만 쓴다(COMBAT-13).")]
        [SerializeField, Range(0f, 1f)] float moraleGainBelowRatio = 0.75f;
        [Tooltip("회복 대상 체력 비율이 이 값 이하이면 위급 회복으로 보고 다른 스킬보다 먼저 쓴다(COMBAT-18).")]
        [SerializeField, Range(0f, 1f)] float criticalHealBelowRatio = 0.3f;
        [Tooltip("범위 공격에 이만큼 이상 적이 들면 고가치 공격으로 보고 일반 스킬보다 먼저 쓴다(COMBAT-18).")]
        [SerializeField, Min(1)] int highValueEnemiesInArea = 4;
        [Tooltip("근접 영웅은 높이 차가 이 값(m) 이하인 적만 교전 중으로 본다. 성벽 위에서 성벽 아래 적에게 버프를 쓰지 않게 한다(COMBAT-18). 0이면 끈다.")]
        [SerializeField, Min(0f)] float meleeEngageMaxHeight = 2.5f;

        public float ThinkInterval => thinkInterval;
        public int MinimumEnemiesInArea => minimumEnemiesInArea;
        public float HealBelowHealthRatio => healBelowHealthRatio;
        public float EngageDistance => engageDistance;
        public float MoraleGainBelowRatio => moraleGainBelowRatio;
        public float CriticalHealBelowRatio => criticalHealBelowRatio;
        public int HighValueEnemiesInArea => highValueEnemiesInArea;
        public float MeleeEngageMaxHeight => meleeEngageMaxHeight;

        public static SkillAutoSettings Create(float thinkInterval = 0.5f, int minimumEnemiesInArea = 2,
            float healBelowHealthRatio = 0.6f, float engageDistance = 12f, float moraleGainBelowRatio = 0.75f,
            float criticalHealBelowRatio = 0.3f, int highValueEnemiesInArea = 4, float meleeEngageMaxHeight = 2.5f)
        {
            var settings = CreateInstance<SkillAutoSettings>();
            settings.thinkInterval = Mathf.Max(0.05f, thinkInterval);
            settings.minimumEnemiesInArea = Mathf.Max(1, minimumEnemiesInArea);
            settings.healBelowHealthRatio = Mathf.Clamp01(healBelowHealthRatio);
            settings.engageDistance = Mathf.Max(0f, engageDistance);
            settings.moraleGainBelowRatio = Mathf.Clamp01(moraleGainBelowRatio);
            settings.criticalHealBelowRatio = Mathf.Clamp01(criticalHealBelowRatio);
            settings.highValueEnemiesInArea = Mathf.Max(1, highValueEnemiesInArea);
            settings.meleeEngageMaxHeight = Mathf.Max(0f, meleeEngageMaxHeight);
            return settings;
        }
    }
}
