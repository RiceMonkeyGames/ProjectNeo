using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Facilities
{
    public enum FacilityType
    {
        Tavern = 0,
        Shop = 1,
        TrainingGround = 2,
        Smithy = 3,
        Defense = 4,
    }

    /// <summary>
    /// 시설의 한 레벨. 효과는 여기에 칸으로 붙이고, 안 쓰는 칸은 기본값으로 둔다.
    /// 새 효과가 필요하면 칸을 추가한다.
    /// </summary>
    [Serializable]
    public struct FacilityLevel
    {
        /// <summary>이 레벨로 올리는 비용. 시작 레벨(첫 칸)에서는 쓰지 않는다.</summary>
        public Cost upgradeCost;
        /// <summary>등장하는 영웅(주점)이나 만들 수 있는 장비(대장간)의 등급.</summary>
        public int qualityTier;
        /// <summary>잡졸 전원에게 붙는 스탯 수정값(훈련장).</summary>
        public List<StatModifier> soldierModifiers;
        /// <summary>모집할 수 있는 잡졸 종류(훈련장).</summary>
        public List<SoldierDefinition> recruitableSoldiers;
        /// <summary>한 번에 내놓는 매물이나 영웅 수(상점, 대장간, 주점).</summary>
        public int offerCount;
        /// <summary>이 레벨에서 나올 수 있는 아이템과 가중치(상점, 대장간). 레벨마다 다르게 적으면 업그레이드할 때 품목이 바뀐다.</summary>
        public List<WeightedEntry<ItemDefinition>> itemPool;
        /// <summary>이 레벨에서 나올 수 있는 영웅과 가중치(주점).</summary>
        public List<WeightedEntry<HeroDefinition>> heroPool;
        /// <summary>이 레벨에서 항상 만들 수 있는 장비와 제작 비용(대장간, Q-35). 레벨마다 전체 목록을 적는다.</summary>
        public List<CraftingRecipe> recipes;
    }

    /// <summary>시설 하나의 데이터. 레벨 목록의 첫 칸이 시작 레벨이다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Facility", fileName = "Facility_")]
    public sealed class FacilityDefinition : ScriptableObject
    {
        [SerializeField] FacilityType type;
        [SerializeField] List<FacilityLevel> levels = new List<FacilityLevel>();
        [SerializeField] string displayName;

        public FacilityType Type => type;
        public IReadOnlyList<FacilityLevel> Levels => levels;
        /// <summary>화면에 보여줄 이름. 비어 있으면 종류 이름을 쓴다.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName) ? type.ToString() : displayName;

        /// <summary>코드에서 시설을 만든다(테스트용).</summary>
        public static FacilityDefinition Create(FacilityType type, params FacilityLevel[] levels) => Create(type, null, levels);

        public static FacilityDefinition Create(FacilityType type, string displayName, params FacilityLevel[] levels)
        {
            var facility = CreateInstance<FacilityDefinition>();
            facility.type = type;
            facility.displayName = displayName;
            facility.levels = new List<FacilityLevel>(levels);
            return facility;
        }
    }
}
