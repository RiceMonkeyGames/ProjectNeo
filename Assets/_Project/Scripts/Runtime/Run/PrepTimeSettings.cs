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
        [Header("Work orders (SIEGE-08)")]
        [Tooltip("동시에 일하는 작업대 수. 주문 순서대로 앞에서부터 맡는다.")]
        [SerializeField, Min(1)] int workCrews = 2;
        [Tooltip("성벽·계단·성문 등 조각 하나를 짓는 작업 시간.")]
        [SerializeField, Min(0f)] float pieceWorkHours = 1f;
        [Tooltip("수성 무기 하나를 짓는 작업 시간.")]
        [SerializeField, Min(0f)] float weaponWorkHours = 1f;
        [Tooltip("완전히 부서진 것을 수리하는 작업 시간. 수리 시간 = 이 값 × 깎인 체력 비율.")]
        [SerializeField, Min(0f)] float fullRepairHours = 1f;

        public int WorkCrews => Mathf.Max(1, workCrews);
        public float PieceWorkHours => pieceWorkHours;
        public float WeaponWorkHours => weaponWorkHours;
        public float FullRepairHours => fullRepairHours;
        /// <summary>깎인 체력 비율(0~1)을 수리하는 시간.</summary>
        public float RepairHours(float missingFraction) => fullRepairHours * Mathf.Clamp01(missingFraction);

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

        /// <summary>작업 주문 수치를 정한다(테스트용). 같은 설정을 돌려준다.</summary>
        public PrepTimeSettings WithWork(int crews = 2, float pieceHours = 1f, float weaponHours = 1f, float repairHours = 1f)
        {
            workCrews = Mathf.Max(1, crews);
            pieceWorkHours = Mathf.Max(0f, pieceHours);
            weaponWorkHours = Mathf.Max(0f, weaponHours);
            fullRepairHours = Mathf.Max(0f, repairHours);
            return this;
        }
    }
}
