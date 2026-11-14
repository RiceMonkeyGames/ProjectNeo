using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
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

        public bool HasFreeEquipmentSlot => equipment.Count < Definition.EquipmentSlots;

        /// <summary>보관함에 있는 장비를 꺼내 착용한다. 장비가 아니거나 칸이 없으면 false.</summary>
        public bool TryEquip(ItemInstance item, Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (item == null || item.Definition.Kind != ItemKind.Equipment) return false;
            if (!HasFreeEquipmentSlot) return false;
            if (!inventory.Remove(item)) return false;
            equipment.Add(item);
            Stats.AddModifiers(item.EquipModifiers, item);
            return true;
        }

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
