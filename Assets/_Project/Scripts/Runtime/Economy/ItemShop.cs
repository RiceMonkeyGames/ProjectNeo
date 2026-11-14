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
        readonly List<bool> locked = new List<bool>();
        readonly EconomyConfig config;

        public IReadOnlyList<ItemDefinition> Stock => stock;
        public IReadOnlyList<bool> Locked => locked;

        public int LockedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < locked.Count; i++)
                    if (locked[i]) count++;
                return count;
            }
        }

        public event Action Changed;

        public ItemShop(EconomyConfig config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        public bool IsLocked(int index) => index >= 0 && index < locked.Count && locked[index];

        /// <summary>index번째 매물의 잠금을 토글한다(PREP-03). 해제는 항상 가능하고, 잠금은 maxLocked 미만일 때만 된다.</summary>
        public bool TryToggleLock(int index, int maxLocked)
        {
            if (index < 0 || index >= stock.Count) return false;
            if (locked[index])
            {
                locked[index] = false;
                Changed?.Invoke();
                return true;
            }
            if (LockedCount >= maxLocked) return false;
            locked[index] = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>잠긴 매물은 남기고 빈 자리(count - 잠긴 수)만 품목 풀에서 새로 뽑는다. isAvailable로 해금 안 된 아이템을 뺀다.</summary>
        public void Restock(
            IReadOnlyList<WeightedEntry<ItemDefinition>> pool,
            int count,
            IRandomSource random,
            Func<ItemDefinition, bool> isAvailable = null)
        {
            var kept = new List<ItemDefinition>();
            for (int i = 0; i < stock.Count; i++)
                if (i < locked.Count && locked[i] && stock[i] != null) kept.Add(stock[i]);

            stock.Clear();
            locked.Clear();
            foreach (ItemDefinition item in kept)
            {
                stock.Add(item);
                locked.Add(true);
            }

            int toRoll = Math.Max(0, count - kept.Count);
            if (toRoll > 0)
            {
                foreach (ItemDefinition item in WeightedPool.Roll(pool, toRoll, random, isAvailable))
                {
                    stock.Add(item);
                    locked.Add(false);
                }
            }
            Changed?.Invoke();
        }

        /// <summary>이번 준비 단계에서 이미 rerollsDone번 리롤했을 때 다음 리롤의 골드(PREP-02).</summary>
        public int GetRerollCost(int rerollsDone) => config.GetRerollCost(rerollsDone);

        /// <summary>매물에 있고 비용을 낼 수 있으면 차감하고 보관함에 넣는다.</summary>
        public bool TryBuy(ItemDefinition item, Wallet wallet, Inventory inventory)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (item == null) return false;
            return TryBuyAt(stock.IndexOf(item), wallet, inventory);
        }

        /// <summary>index번째 매물을 사고 잠금이 있으면 함께 지운다.</summary>
        public bool TryBuyAt(int index, Wallet wallet, Inventory inventory)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (index < 0 || index >= stock.Count) return false;
            ItemDefinition item = stock[index];
            if (item == null || !wallet.TrySpend(item.Price)) return false;
            stock.RemoveAt(index);
            if (index < locked.Count) locked.RemoveAt(index);
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
