using System;
using System.Collections.Generic;

namespace ProjectNeo.Economy
{
    /// <summary>
    /// 보관함. 구매, 생산, 전투 보상 등 어떤 경로로 얻은 아이템이든 여기에 담긴다.
    /// 캐릭터가 착용한 장비는 보관함에서 빠져 있다.
    /// </summary>
    public sealed class Inventory
    {
        readonly List<ItemInstance> items = new List<ItemInstance>();

        public IReadOnlyList<ItemInstance> Items => items;

        public event Action Changed;

        public ItemInstance Add(ItemDefinition definition)
        {
            var instance = new ItemInstance(definition);
            Add(instance);
            return instance;
        }

        public void Add(ItemInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (items.Contains(item)) return;
            items.Add(item);
            Changed?.Invoke();
        }

        /// <summary>
        /// index 자리에 넣는다(뒤의 아이템은 한 칸씩 밀린다). 범위 밖이면 맨 뒤에 넣는다. 바꿔 낀 장비가 새 장비가
        /// 있던 보관함 자리로 들어갈 때 쓴다(UI-07).
        /// </summary>
        public void Insert(int index, ItemInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (items.Contains(item)) return;
            items.Insert(index >= 0 && index <= items.Count ? index : items.Count, item);
            Changed?.Invoke();
        }

        /// <summary>아이템의 자리. 없으면 -1.</summary>
        public int IndexOf(ItemInstance item) => items.IndexOf(item);

        public bool Remove(ItemInstance item)
        {
            if (!items.Remove(item)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool Contains(ItemInstance item) => items.Contains(item);

        public int CountOf(ItemDefinition definition)
        {
            int count = 0;
            foreach (ItemInstance item in items)
                if (item.Definition == definition) count++;
            return count;
        }
    }
}
