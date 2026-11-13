using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Economy
{
    /// <summary>장비 강화 공통 수치. 아이템별 재료 비용은 ItemDefinition에 둔다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Equipment Upgrade Settings", fileName = "EquipmentUpgradeSettings")]
    public sealed class EquipmentUpgradeSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] float modifierBonusPerLevel = 0.1f;
        [SerializeField, Min(0f)] float goldPriceRatio = 0.5f;
        [SerializeField] List<int> maxLevelsBySmithyLevel = new List<int> { 1, 3, 5 };

        public float ModifierBonusPerLevel => modifierBonusPerLevel;
        public float GoldPriceRatio => goldPriceRatio;

        /// <summary>FacilityState.Level과 같은 0 기반 인덱스. 지원하지 않는 시설 레벨은 강화 불가.</summary>
        public int GetMaxLevel(int smithyLevel)
        {
            return smithyLevel >= 0 && smithyLevel < maxLevelsBySmithyLevel.Count
                ? Math.Max(0, maxLevelsBySmithyLevel[smithyLevel]) : 0;
        }

        public static EquipmentUpgradeSettings Create(float modifierBonusPerLevel, float goldPriceRatio,
            params int[] maxLevelsBySmithyLevel)
        {
            if (float.IsNaN(modifierBonusPerLevel) || float.IsInfinity(modifierBonusPerLevel) || modifierBonusPerLevel < 0f)
                throw new ArgumentOutOfRangeException(nameof(modifierBonusPerLevel));
            if (float.IsNaN(goldPriceRatio) || float.IsInfinity(goldPriceRatio) || goldPriceRatio < 0f)
                throw new ArgumentOutOfRangeException(nameof(goldPriceRatio));
            var settings = CreateInstance<EquipmentUpgradeSettings>();
            settings.modifierBonusPerLevel = modifierBonusPerLevel;
            settings.goldPriceRatio = goldPriceRatio;
            settings.maxLevelsBySmithyLevel = new List<int>(maxLevelsBySmithyLevel);
            return settings;
        }
    }
}
