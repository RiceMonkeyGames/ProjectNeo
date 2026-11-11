using System;
using System.Collections.Generic;

namespace ProjectNeo.Economy
{
    /// <summary>
    /// 실제로 가진 아이템 한 개. 같은 종류를 여러 개 가질 수 있어서 종류(ItemDefinition)와 구분한다.
    /// 장비 강화 단계 같은 개별 상태는 나중에 여기에 붙인다.
    /// </summary>
    public sealed class ItemInstance
    {
        public ItemDefinition Definition { get; }

        public ItemInstance(ItemDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        }
    }

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
