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
        [SerializeField] List<KillReward> killRewards = new List<KillReward>();

        [Header("완성품 아이템")]
        [SerializeField, Range(0f, 1f)] float itemChance = 0.5f;
        [SerializeField, Min(0)] int itemRolls = 1;
        [SerializeField] List<WeightedEntry<ItemDefinition>> itemPool = new List<WeightedEntry<ItemDefinition>>();

        [Header("메타")]
        [SerializeField, Min(0)] int commanderXp = 40;
        [SerializeField, Min(0)] int commanderXpPerStage = 10;
        [SerializeField, Min(0)] int unlockPoints = 5;

        public int BaseGold => baseGold;
        public int GoldPerStage => goldPerStage;
        public IReadOnlyList<KillReward> KillRewards => killRewards;
        /// <summary>아이템 한 번 굴림이 성공할 확률.</summary>
        public float ItemChance => itemChance;
        /// <summary>아이템을 굴리는 횟수. 굴릴 때마다 확률로 하나를 풀에서 뽑는다(중복 가능).</summary>
        public int ItemRolls => itemRolls;
        public IReadOnlyList<WeightedEntry<ItemDefinition>> ItemPool => itemPool;
        public int CommanderXp => commanderXp;
        public int CommanderXpPerStage => commanderXpPerStage;
        public int UnlockPoints => unlockPoints;

        /// <summary>코드에서 표를 만든다(테스트용).</summary>
        public static StageRewardTable Create(int baseGold, int goldPerStage, KillReward[] killRewards = null,
            float itemChance = 0f, int itemRolls = 1, WeightedEntry<ItemDefinition>[] itemPool = null,
            int commanderXp = 0, int commanderXpPerStage = 0, int unlockPoints = 0)
        {
            var table = CreateInstance<StageRewardTable>();
            table.baseGold = Mathf.Max(0, baseGold);
            table.goldPerStage = Mathf.Max(0, goldPerStage);
            table.killRewards = new List<KillReward>(killRewards ?? new KillReward[0]);
            table.itemChance = Mathf.Clamp01(itemChance);
            table.itemRolls = Mathf.Max(0, itemRolls);
            table.itemPool = new List<WeightedEntry<ItemDefinition>>(itemPool ?? new WeightedEntry<ItemDefinition>[0]);
            table.commanderXp = Mathf.Max(0, commanderXp);
            table.commanderXpPerStage = Mathf.Max(0, commanderXpPerStage);
            table.unlockPoints = Mathf.Max(0, unlockPoints);
            return table;
        }
    }
}
