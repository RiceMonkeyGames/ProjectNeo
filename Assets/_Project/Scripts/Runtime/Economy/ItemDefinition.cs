using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Economy
{
    public enum ItemKind
    {
        /// <summary>캐릭터가 착용하는 장비.</summary>
        Equipment = 0,
        /// <summary>한 번 쓰면 사라지는 아이템.</summary>
        Consumable = 1,
    }

    /// <summary>아이템 한 종류의 데이터.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Item", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject, IUnlockable
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] ItemKind kind;
        [SerializeField] Cost price;
        [SerializeField] bool unlockedByDefault = true;
        [SerializeField] List<StatModifier> equipModifiers = new List<StatModifier>();
        [SerializeField] List<ItemUpgradeMaterials> upgradeMaterials = new List<ItemUpgradeMaterials>();

        public string Id => id;
        public string DisplayName => displayName;
        public ItemKind Kind => kind;
        /// <summary>구매하거나 생산할 때 드는 비용.</summary>
        public Cost Price => price;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Item;
        /// <summary>착용한 캐릭터에게 붙는 스탯 수정값(장비만).</summary>
        public IReadOnlyList<StatModifier> EquipModifiers => equipModifiers;
        /// <summary>목표 +1부터 순서대로 적은 강화 재료. 빈 단계는 재료 없음, 단계 누락은 강화 불가.</summary>
        public IReadOnlyList<ItemUpgradeMaterials> UpgradeMaterials => upgradeMaterials;

        /// <summary>코드에서 아이템을 만든다(테스트용).</summary>
        public static ItemDefinition Create(string id, ItemKind kind, Cost price, bool unlockedByDefault = true, params StatModifier[] equipModifiers)
        {
            var item = CreateInstance<ItemDefinition>();
            item.id = id;
            item.displayName = id;
            item.kind = kind;
            item.price = price;
            item.unlockedByDefault = unlockedByDefault;
            item.equipModifiers = new List<StatModifier>(equipModifiers);
            return item;
        }

        /// <summary>강화 재료가 설정된 장비를 만든다(테스트용).</summary>
        public static ItemDefinition CreateUpgradable(string id, Cost price,
            ItemUpgradeMaterials[] upgradeMaterials, bool unlockedByDefault = true,
            params StatModifier[] equipModifiers)
        {
            var item = Create(id, ItemKind.Equipment, price, unlockedByDefault, equipModifiers);
            item.upgradeMaterials = new List<ItemUpgradeMaterials>(upgradeMaterials);
            return item;
        }
    }
}
