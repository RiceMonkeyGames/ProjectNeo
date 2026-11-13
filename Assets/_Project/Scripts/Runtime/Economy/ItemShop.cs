using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Economy
{
    /// <summary>
    /// 아이템을 사고파는 창구. 상점과 대장간이 같이 쓴다.
    /// 매물은 시설 레벨의 품목 풀에서 랜덤으로 채운다.
    /// </summary>
    public sealed class ItemShop
    {
        readonly List<ItemDefinition> stock = new List<ItemDefinition>();
        readonly EconomyConfig config;

        public IReadOnlyList<ItemDefinition> Stock => stock;

        public event Action Changed;

        public ItemShop(EconomyConfig config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        /// <summary>매물을 비우고 품목 풀에서 count개를 새로 뽑는다. isAvailable로 해금 안 된 아이템을 뺀다.</summary>
        public void Restock(
            IReadOnlyList<WeightedEntry<ItemDefinition>> pool,
            int count,
            IRandomSource random,
            Func<ItemDefinition, bool> isAvailable = null)
        {
            stock.Clear();
            stock.AddRange(WeightedPool.Roll(pool, count, random, isAvailable));
            Changed?.Invoke();
        }

        /// <summary>이번 준비 단계에서 이미 rerollsDone번 리롤했을 때 다음 리롤의 골드(PREP-02).</summary>
        public int GetRerollCost(int rerollsDone) => config.GetRerollCost(rerollsDone);

        /// <summary>매물에 있고 비용을 낼 수 있으면 차감하고 보관함에 넣는다.</summary>
        public bool TryBuy(ItemDefinition item, Wallet wallet, Inventory inventory)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (item == null || !stock.Contains(item)) return false;
            if (!wallet.TrySpend(item.Price)) return false;
            stock.Remove(item);
            inventory.Add(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>판매가. 구매가의 골드에 판매 비율을 곱하고 소수점은 버린다. 재료로 낸 몫은 돌려주지 않는다.</summary>
        public int GetSellPrice(ItemDefinition item)
        {
            if (item == null) return 0;
            return Mathf.FloorToInt(Mathf.Max(item.Price.gold, 0) * config.SellRatio);
        }

        /// <summary>보관함에 있는 아이템을 판다. 어떤 경로로 얻은 아이템이든 팔 수 있다.</summary>
        public bool TrySell(ItemInstance item, Wallet wallet, Inventory inventory)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (item == null || !inventory.Remove(item)) return false;
            wallet.AddGold(GetSellPrice(item.Definition));
            return true;
        }
    }
}
