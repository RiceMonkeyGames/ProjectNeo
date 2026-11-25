using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("시설 하나를 한 단계 올리는 시간(PREP-11). 장인 1명이 그동안 일한다.")]
        [SerializeField, Min(0f)] float facilityUpgradeHours = 1f;
        [Header("Artisans (PREP-11)")]
        [Tooltip("런을 시작할 때의 장인 수. 장인 수만큼 건설·수리 주문을 앞에서부터 동시에 진행한다.")]
        [FormerlySerializedAs("workCrews")]
        [SerializeField, Min(1)] int startingArtisans = 2;
        [Tooltip("대장간 레벨별 장인 상한(첫 칸이 Lv1). 대장간이 없으면 시작 장인 수가 상한이다.")]
        [SerializeField] List<int> artisanCapBySmithyLevel = new List<int> { 3, 4, 5 };
        [Tooltip("장인 고용 골드. n번째 고용이 n번째 칸을 쓰고, 칸이 모자라면 마지막 값을 쓴다.")]
        [SerializeField] List<int> artisanHireGold = new List<int> { 50, 100, 150 };
        [Header("Work orders (SIEGE-08)")]
        [Tooltip("성벽·계단·성문 등 조각 하나를 짓는 작업 시간.")]
        [SerializeField, Min(0f)] float pieceWorkHours = 1f;
        [Tooltip("수성 무기 하나를 짓는 작업 시간.")]
        [SerializeField, Min(0f)] float weaponWorkHours = 1f;
        [Tooltip("완전히 부서진 것을 수리하는 작업 시간. 수리 시간 = 이 값 × 깎인 체력 비율.")]
        [SerializeField, Min(0f)] float fullRepairHours = 1f;

        public int StartingArtisans => Mathf.Max(1, startingArtisans);
        public float FacilityUpgradeHours => facilityUpgradeHours;

        /// <summary>대장간 레벨(0부터)의 장인 상한. 시작 장인 수보다 작지 않다. 칸이 모자라면 마지막 값을 쓴다.</summary>
        public int ArtisanCap(int smithyLevel)
        {
            if (artisanCapBySmithyLevel == null || artisanCapBySmithyLevel.Count == 0) return StartingArtisans;
            int index = Mathf.Clamp(smithyLevel, 0, artisanCapBySmithyLevel.Count - 1);
            return Mathf.Max(StartingArtisans, artisanCapBySmithyLevel[index]);
        }

        /// <summary>이미 <paramref name="hired"/>명을 고용했을 때 다음 장인 고용 골드.</summary>
        public int ArtisanHireGold(int hired)
        {
            if (artisanHireGold == null || artisanHireGold.Count == 0) return 0;
            return Mathf.Max(0, artisanHireGold[Mathf.Clamp(hired, 0, artisanHireGold.Count - 1)]);
        }
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

        /// <summary>작업 주문 수치를 정한다(테스트용). <paramref name="crews"/>는 시작 장인 수다. 같은 설정을 돌려준다.</summary>
        public PrepTimeSettings WithWork(int crews = 2, float pieceHours = 1f, float weaponHours = 1f, float repairHours = 1f)
        {
            startingArtisans = Mathf.Max(1, crews);
            pieceWorkHours = Mathf.Max(0f, pieceHours);
            weaponWorkHours = Mathf.Max(0f, weaponHours);
            fullRepairHours = Mathf.Max(0f, repairHours);
            return this;
        }

        /// <summary>장인 수치를 정한다(테스트용). 넘기지 않은 목록은 그대로 둔다. 같은 설정을 돌려준다.</summary>
        public PrepTimeSettings WithArtisans(int starting = 2, int[] capBySmithyLevel = null, int[] hireGold = null,
            float facilityHours = 1f)
        {
            startingArtisans = Mathf.Max(1, starting);
            if (capBySmithyLevel != null) artisanCapBySmithyLevel = new List<int>(capBySmithyLevel);
            if (hireGold != null) artisanHireGold = new List<int>(hireGold);
            facilityUpgradeHours = Mathf.Max(0f, facilityHours);
            return this;
        }
    }
}
