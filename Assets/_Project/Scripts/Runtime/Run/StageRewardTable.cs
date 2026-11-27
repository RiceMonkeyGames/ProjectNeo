using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 스테이지 승리 보상 수치. 모든 값은 이 에셋에서 바꾼다. 현재 값은 프로토타입용 임의 값이다(2026-11-12 결정).
    /// 포로 병력 보상은 씬 유닛을 새로 만드는 기능(고용·모집)이 생긴 뒤 붙인다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stage Reward Table", fileName = "StageRewards")]
    public sealed class StageRewardTable : ScriptableObject
    {
        [Header("골드")]
        [SerializeField, Min(0)] int baseGold = 50;
        [SerializeField, Min(0)] int goldPerStage = 10;

        [Header("처치 보상 (몬스터 재료)")]
        [Tooltip("적 종류 목록(CONTENT-04). 처치한 적 ID로 적 종류를 찾아 그 처치 골드·재료를 준다. 목록에 없는 적은 보상이 없다.")]
        [SerializeField] Stages.EnemyCatalog enemies;

        [Header("완성품 아이템")]
        [SerializeField, Range(0f, 1f)] float itemChance = 0.5f;
        [SerializeField, Min(0)] int itemRolls = 1;
        [SerializeField] List<WeightedEntry<ItemDefinition>> itemPool = new List<WeightedEntry<ItemDefinition>>();

        [Header("메타")]
        [SerializeField, Min(0)] int commanderXp = 40;
        [SerializeField, Min(0)] int commanderXpPerStage = 10;

        [Header("런 종료 해금 포인트 (META-08)")]
        [Tooltip("런이 끝날 때 클리어한 스테이지마다 주는 해금 포인트(사용자 결정 2026-11-19).")]
        [SerializeField, Min(0)] int unlockPointsPerStageCleared = 1;
        [Tooltip("런이 끝날 때 처치한 보스마다 주는 해금 포인트(사용자 결정 2026-11-19). 보스는 SIEGE-09.")]
        [SerializeField, Min(0)] int unlockPointsPerBossKilled = 1;

        [Header("런 점수 (META-04, Q-70)")]
        [SerializeField, Min(0)] int scorePerStageCleared = 1000;
        [SerializeField, Min(0)] int scorePerBossKilled = 500;
        [SerializeField, Min(0)] int scorePerEnemyKilled = 10;
        [Tooltip("성문이 멀쩡한 채로 이긴 스테이지마다.")]
        [SerializeField, Min(0)] int scorePerGateIntactVictory = 200;
        [Tooltip("런이 끝날 때 살아 있는 영웅(플레이어 제외)마다.")]
        [SerializeField, Min(0)] int scorePerLivingHero = 100;
        [Tooltip("런이 끝날 때 살아 있는 잡졸마다.")]
        [SerializeField, Min(0)] int scorePerLivingGrunt = 10;
        [Tooltip("런이 끝날 때 가진 골드를 이 값으로 나눈 몫(내림)이 점수다.")]
        [SerializeField, Min(1)] int goldPerScorePoint = 10;
        [Tooltip("마지막 스테이지를 깨고 엔딩에 닿으면.")]
        [SerializeField, Min(0)] int scoreForEnding = 5000;

        public int ScorePerStageCleared => scorePerStageCleared;
        public int ScorePerBossKilled => scorePerBossKilled;
        public int ScorePerEnemyKilled => scorePerEnemyKilled;
        public int ScorePerGateIntactVictory => scorePerGateIntactVictory;
        public int ScorePerLivingHero => scorePerLivingHero;
        public int ScorePerLivingGrunt => scorePerLivingGrunt;
        public int GoldPerScorePoint => Mathf.Max(1, goldPerScorePoint);
        public int ScoreForEnding => scoreForEnding;

        public int BaseGold => baseGold;
        public int GoldPerStage => goldPerStage;
        public Stages.EnemyCatalog Enemies => enemies;
        /// <summary>아이템 한 번 굴림이 성공할 확률.</summary>
        public float ItemChance => itemChance;
        /// <summary>아이템을 굴리는 횟수. 굴릴 때마다 확률로 하나를 풀에서 뽑는다(중복 가능).</summary>
        public int ItemRolls => itemRolls;
        public IReadOnlyList<WeightedEntry<ItemDefinition>> ItemPool => itemPool;
        public int CommanderXp => commanderXp;
        public int CommanderXpPerStage => commanderXpPerStage;
        public int UnlockPointsPerStageCleared => unlockPointsPerStageCleared;
        public int UnlockPointsPerBossKilled => unlockPointsPerBossKilled;

        /// <summary>런이 끝날 때 줄 해금 포인트(META-08). 클리어한 스테이지 수와 처치한 보스 수에 비례한다.</summary>
        public int RunEndUnlockPoints(int stagesCleared, int bossesKilled) =>
            unlockPointsPerStageCleared * Mathf.Max(0, stagesCleared) + unlockPointsPerBossKilled * Mathf.Max(0, bossesKilled);

        /// <summary>코드에서 표를 만든다(테스트용).</summary>
        public static StageRewardTable Create(int baseGold, int goldPerStage, Stages.EnemyCatalog enemies = null,
            float itemChance = 0f, int itemRolls = 1, WeightedEntry<ItemDefinition>[] itemPool = null,
            int commanderXp = 0, int commanderXpPerStage = 0, int unlockPointsPerStageCleared = 1, int unlockPointsPerBossKilled = 1)
        {
            var table = CreateInstance<StageRewardTable>();
            table.baseGold = Mathf.Max(0, baseGold);
            table.goldPerStage = Mathf.Max(0, goldPerStage);
            table.enemies = enemies;
            table.itemChance = Mathf.Clamp01(itemChance);
            table.itemRolls = Mathf.Max(0, itemRolls);
            table.itemPool = new List<WeightedEntry<ItemDefinition>>(itemPool ?? new WeightedEntry<ItemDefinition>[0]);
            table.commanderXp = Mathf.Max(0, commanderXp);
            table.commanderXpPerStage = Mathf.Max(0, commanderXpPerStage);
            table.unlockPointsPerStageCleared = Mathf.Max(0, unlockPointsPerStageCleared);
            table.unlockPointsPerBossKilled = Mathf.Max(0, unlockPointsPerBossKilled);
            return table;
        }
    }
}
