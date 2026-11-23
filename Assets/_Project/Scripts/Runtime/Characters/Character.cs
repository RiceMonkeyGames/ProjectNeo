using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Skills;
using ProjectNeo.Stats;

namespace ProjectNeo.Characters
{
    /// <summary>
    /// 런 안에서 살아 있는 영웅 한 명. 스탯, 스킬, 장비를 가진다.
    /// 플레이어 캐릭터와 고용한 영웅이 같은 클래스를 쓰고, 누가 조종하는지만 다르다.
    /// </summary>
    public sealed class Character
    {
        readonly List<ItemInstance> equipment = new List<ItemInstance>();

        public HeroDefinition Definition { get; }
        public StatSheet Stats { get; } = new StatSheet();
        public IReadOnlyList<ItemInstance> Equipment => equipment;
        public IReadOnlyList<SkillDefinition> Skills => Definition.Skills;

        public Character(HeroDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            Stats.SetBases(definition.BaseStats);
        }

        /// <summary>
        /// 장비를 낄 수 있는지. 낄 수 있으면 무기·보조무기·방어구·장신구 칸을 하나씩 갖는다(UI-07, <see cref="EquipmentSlots"/>).
        /// 정의의 칸 수가 0이면(허수아비 등) 장비가 없다.
        /// </summary>
        public bool CanEquip => Definition.EquipmentSlots > 0;

        /// <summary>그 칸에 낀 장비. 비어 있으면 null.</summary>
        public ItemInstance EquippedIn(EquipmentSlot slot)
        {
            foreach (ItemInstance item in equipment)
                if (item.Definition.Slot == slot) return item;
            return null;
        }

        /// <summary>
        /// 보관함에 있는 장비를 꺼내 그 종류 칸에 착용한다. 칸에 다른 장비가 있으면 둘의 자리를 바꾼다: 끼고 있던 장비
        /// (<paramref name="replaced"/>)가 새 장비가 있던 보관함 자리로 들어간다(UI-07, 사용자 2026-11-23).
        /// 장비가 아니거나, 장비를 못 끼는 캐릭터거나, 보관함에 없으면 false.
        /// </summary>
        public bool TryEquip(ItemInstance item, Inventory inventory, out ItemInstance replaced)
        {
            replaced = null;
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (item == null || item.Definition.Kind != ItemKind.Equipment || !CanEquip) return false;
            int place = inventory.IndexOf(item);
            if (!inventory.Remove(item)) return false;
            replaced = EquippedIn(item.Definition.Slot);
            if (replaced != null)
            {
                equipment.Remove(replaced);
                Stats.RemoveModifiersFrom(replaced);
                inventory.Insert(place, replaced);
            }
            equipment.Add(item);
            Stats.AddModifiers(item.EquipModifiers, item);
            return true;
        }

        public bool TryEquip(ItemInstance item, Inventory inventory) => TryEquip(item, inventory, out _);

        /// <summary>착용한 장비를 벗어 보관함에 돌려놓는다.</summary>
        public bool TryUnequip(ItemInstance item, Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!equipment.Remove(item)) return false;
            Stats.RemoveModifiersFrom(item);
            inventory.Add(item);
            return true;
        }
    }
}
