using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
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

        public SquadConfig SquadConfig => squadConfig;
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
    }
}
