using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>새 런을 시작할 때의 재화·플레이어·부대. 값은 에셋에서 바꾼다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [SerializeField] SquadConfig squadConfig;
        [SerializeField] HeroDefinition playerHero;
        [SerializeField, Min(0)] int startingGold;
        [SerializeField] List<MaterialAmount> startingMaterials = new List<MaterialAmount>();
        [SerializeField] List<RunSquadSetup> startingSquads = new List<RunSquadSetup>();
        [Tooltip("이 런에서 쓰는 시설. 종류마다 하나이며 새 런은 모두 시작 레벨이다.")]
        [SerializeField] List<FacilityDefinition> facilities = new List<FacilityDefinition>();
        [Tooltip("상점 판매 비율 등 거래 수치. 비어 있으면 상점 거래를 쓰지 않는다.")]
        [SerializeField] EconomyConfig economy;
        [SerializeField] EquipmentUpgradeSettings equipmentUpgrades;

        public SquadConfig SquadConfig => squadConfig;
        public IReadOnlyList<FacilityDefinition> Facilities => facilities;
        public EconomyConfig Economy => economy;
        public EquipmentUpgradeSettings EquipmentUpgrades => equipmentUpgrades;
        public HeroDefinition PlayerHero => playerHero;
        public int StartingGold => startingGold;
        public IReadOnlyList<MaterialAmount> StartingMaterials => startingMaterials;
        public IReadOnlyList<RunSquadSetup> StartingSquads => startingSquads;

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static RunConfig Create(SquadConfig squadConfig, HeroDefinition playerHero, int startingGold,
            MaterialAmount[] startingMaterials = null, params RunSquadSetup[] startingSquads)
        {
            var config = CreateInstance<RunConfig>();
            config.squadConfig = squadConfig;
            config.playerHero = playerHero;
            config.startingGold = Mathf.Max(0, startingGold);
            config.startingMaterials = new List<MaterialAmount>(startingMaterials ?? new MaterialAmount[0]);
            config.startingSquads = new List<RunSquadSetup>(startingSquads);
            return config;
        }

        /// <summary>시설까지 정한 설정을 만든다(테스트용).</summary>
        public static RunConfig Create(SquadConfig squadConfig, HeroDefinition playerHero, int startingGold,
            MaterialAmount[] startingMaterials, FacilityDefinition[] facilities, params RunSquadSetup[] startingSquads)
        {
            RunConfig config = Create(squadConfig, playerHero, startingGold, startingMaterials, startingSquads);
            config.facilities = new List<FacilityDefinition>(facilities ?? new FacilityDefinition[0]);
            return config;
        }

        /// <summary>거래 수치를 정한다(테스트용). 같은 설정을 돌려준다.</summary>
        public RunConfig WithEconomy(EconomyConfig config)
        {
            economy = config;
            return this;
        }

        public RunConfig WithEquipmentUpgrades(EquipmentUpgradeSettings settings)
        {
            equipmentUpgrades = settings;
            return this;
        }
    }
}
