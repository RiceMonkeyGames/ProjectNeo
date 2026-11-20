using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>영웅 AI 자동 시전 판단 수치(Q-63, COMBAT-06).</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Skill Auto Cast", fileName = "SkillAuto")]
    public sealed class SkillAutoSettings : ScriptableObject
    {
        [Tooltip("몇 초마다 스킬 쓸지 판단하는지. 한 번 판단에 스킬은 하나만 쓴다.")]
        [SerializeField, Min(0.05f)] float thinkInterval = 0.5f;
        [Tooltip("적에게 거는 범위 스킬(자기 주변·조준 지점)을 쓰려면 범위 안에 있어야 하는 적 수.")]
        [SerializeField, Min(1)] int minimumEnemiesInArea = 2;
        [Tooltip("회복 스킬은 범위 안 아군 중 체력 비율이 이 값 이하인 유닛이 있을 때 쓴다.")]
        [SerializeField, Range(0f, 1f)] float healBelowHealthRatio = 0.6f;
        [Tooltip("버프·사기 스킬은 현재 대상 적이 이 거리(m) 안에 있을 때(교전 중) 쓴다.")]
        [SerializeField, Min(0f)] float engageDistance = 12f;

        public float ThinkInterval => thinkInterval;
        public int MinimumEnemiesInArea => minimumEnemiesInArea;
        public float HealBelowHealthRatio => healBelowHealthRatio;
        public float EngageDistance => engageDistance;

        public static SkillAutoSettings Create(float thinkInterval = 0.5f, int minimumEnemiesInArea = 2,
            float healBelowHealthRatio = 0.6f, float engageDistance = 12f)
        {
            var settings = CreateInstance<SkillAutoSettings>();
            settings.thinkInterval = Mathf.Max(0.05f, thinkInterval);
            settings.minimumEnemiesInArea = Mathf.Max(1, minimumEnemiesInArea);
            settings.healBelowHealthRatio = Mathf.Clamp01(healBelowHealthRatio);
            settings.engageDistance = Mathf.Max(0f, engageDistance);
            return settings;
        }
    }
}
