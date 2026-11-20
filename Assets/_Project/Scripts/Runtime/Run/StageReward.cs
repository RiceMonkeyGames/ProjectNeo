using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Flow;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 한 번의 승리로 받는 보상 내역. 같은 전투 번호에 한 번만 런에 지급한다(<see cref="RunState.ApplyReward"/>).
    /// 지휘관 경험치는 런이 끝나도 남는 메타 진행으로 간다. 해금 포인트는 승리마다 주지 않고 런이 끝날 때 한 번 준다(META-08).
    /// </summary>
    public sealed class StageReward
    {
        readonly Dictionary<MaterialDefinition, int> materials = new Dictionary<MaterialDefinition, int>();
        readonly List<ItemDefinition> items = new List<ItemDefinition>();

        public int BattleNumber { get; }
        public int StageNumber { get; }
        public int Gold { get; private set; }
        public IReadOnlyDictionary<MaterialDefinition, int> Materials => materials;
        public IReadOnlyList<ItemDefinition> Items => items;
        public int CommanderXp { get; private set; }

        public StageReward(int battleNumber, int stageNumber)
        {
            if (battleNumber <= 0) throw new ArgumentOutOfRangeException(nameof(battleNumber));
            BattleNumber = battleNumber;
            StageNumber = stageNumber;
        }

        public void AddGold(int amount) { if (amount > 0) Gold += amount; }

        public void AddMaterial(MaterialDefinition material, int amount)
        {
            if (material == null || amount <= 0) return;
            materials.TryGetValue(material, out int current);
            materials[material] = current + amount;
        }

        public void AddItem(ItemDefinition item) { if (item != null) items.Add(item); }
        public void AddCommanderXp(int amount) { if (amount > 0) CommanderXp += amount; }

        /// <summary>
        /// 표와 전투 결과로 보상을 정한다. 승리한 결과만 받는다. 같은 표·결과·랜덤 시드면 같은 보상이 나온다.
        /// 표에 없는 적 종류의 처치는 골드·재료를 주지 않는다.
        /// </summary>
        public static StageReward Roll(StageRewardTable table, int stageNumber, BattleResult result, IRandomSource random)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (result.Outcome != BattleOutcome.Victory) throw new ArgumentException("패배한 전투에는 보상이 없다.", nameof(result));

            var reward = new StageReward(result.BattleNumber, stageNumber);
            int stageBonus = Math.Max(0, stageNumber - 1);
            reward.AddGold(table.BaseGold + table.GoldPerStage * stageBonus);

            foreach (KeyValuePair<string, int> kill in result.EnemiesKilled)
                foreach (KillReward rule in table.KillRewards)
                {
                    if (rule.enemyId != kill.Key) continue;
                    reward.AddGold(rule.goldPerKill * kill.Value);
                    reward.AddMaterial(rule.material, rule.materialPerKill * kill.Value);
                }

            for (int i = 0; i < table.ItemRolls; i++)
            {
                if (random.NextFloat() >= table.ItemChance) continue;
                List<ItemDefinition> picked = WeightedPool.Roll(table.ItemPool, 1, random);
                if (picked.Count > 0) reward.AddItem(picked[0]);
            }

            reward.AddCommanderXp(table.CommanderXp + table.CommanderXpPerStage * stageBonus);
            return reward;
        }
    }
}
