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
        // 소모품을 썼을 때 바로 회복하는 양(최대 체력 대비 비율, PREP-04 임시값). 장비에는 쓰지 않는다. 0이면 회복 효과 없음.
        [SerializeField, Range(0f, 1f)] float useHealRatio = 0.35f;
        [SerializeField] List<ItemUpgradeMaterials> upgradeMaterials = new List<ItemUpgradeMaterials>();
        // 장비 화면 보관함 격자와 장비 칸에 그리는 아이콘(UI-07). 없으면 이름 앞 글자를 보여준다.
        [SerializeField] Sprite icon;
        // 장비가 들어가는 칸(UI-07). 장비에만 쓴다.
        [SerializeField] EquipmentSlot slot;

        public string Id => id;
        /// <summary>장비가 들어가는 칸.</summary>
        public EquipmentSlot Slot => slot;
        /// <summary>장비 화면 아이콘. 없으면 null.</summary>
        public Sprite Icon => icon;
        public string DisplayName => displayName;
        public ItemKind Kind => kind;
        /// <summary>구매하거나 생산할 때 드는 비용.</summary>
        public Cost Price => price;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Item;
        /// <summary>착용한 캐릭터에게 붙는 스탯 수정값(장비만).</summary>
        public IReadOnlyList<StatModifier> EquipModifiers => equipModifiers;
        /// <summary>소모품을 썼을 때 회복하는 최대 체력 대비 비율(0~1). 소모품이 아니면 0이다.</summary>
        public float UseHealRatio => kind == ItemKind.Consumable ? Mathf.Clamp01(useHealRatio) : 0f;
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

        /// <summary>칸을 정한 장비를 만든다(테스트용).</summary>
        public static ItemDefinition CreateEquipment(string id, EquipmentSlot slot, Cost price, params StatModifier[] equipModifiers)
        {
            var item = Create(id, ItemKind.Equipment, price, true, equipModifiers);
            item.slot = slot;
            return item;
        }

        /// <summary>회복 비율을 정한 소모품을 만든다(테스트용).</summary>
        public static ItemDefinition CreateConsumable(string id, Cost price, float useHealRatio, bool unlockedByDefault = true)
        {
            var item = Create(id, ItemKind.Consumable, price, unlockedByDefault);
            item.useHealRatio = Mathf.Clamp01(useHealRatio);
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
