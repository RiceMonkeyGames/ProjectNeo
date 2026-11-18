using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 준비 시간 수치(Q-53, PREP-06). 준비마다 주는 시간과 시계를 진행시키는 행동별 시간. 단위는 시간(hour)이다.
    /// 플레이 후 조절할 시작값이며 코드 없이 에셋에서 바꾼다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Run/Prep Time Settings", fileName = "PrepTime")]
    public sealed class PrepTimeSettings : ScriptableObject
    {
        [Tooltip("준비 단계마다 주는 시간. 남은 시간은 다음 준비로 넘어가지 않는다.")]
        [SerializeField, Min(0f)] float budgetHours = 12f;
        [Tooltip("대장간에서 장비 하나를 만드는 시간.")]
        [SerializeField, Min(0f)] float craftHours = 1f;
        [Tooltip("대장간에서 장비를 한 단계 강화하는 시간.")]
        [SerializeField, Min(0f)] float upgradeHours = 1f;
        [Tooltip("기다리기 한 번에 지나는 시간.")]
        [SerializeField, Min(0f)] float waitHours = 1f;
        [Tooltip("정찰 한 번의 시간(C-5에서 쓴다).")]
        [SerializeField, Min(0f)] float scoutHours = 3f;

        public float BudgetHours => budgetHours;
        public float CraftHours => craftHours;
        public float UpgradeHours => upgradeHours;
        public float WaitHours => waitHours;
        public float ScoutHours => scoutHours;

        public static PrepTimeSettings Create(float budgetHours = 12f, float craftHours = 1f, float upgradeHours = 1f,
            float waitHours = 1f, float scoutHours = 3f)
        {
            var value = CreateInstance<PrepTimeSettings>();
            value.budgetHours = Mathf.Max(0f, budgetHours);
            value.craftHours = Mathf.Max(0f, craftHours);
            value.upgradeHours = Mathf.Max(0f, upgradeHours);
            value.waitHours = Mathf.Max(0f, waitHours);
            value.scoutHours = Mathf.Max(0f, scoutHours);
            return value;
        }
    }
}
