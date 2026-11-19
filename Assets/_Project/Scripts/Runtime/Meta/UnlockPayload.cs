using System;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>해금 항목이 여는 것. 종류 + 대상 id + 수량. 실제 런 콘텐츠에 적용하는 것은 META-07이다.</summary>
    [Serializable]
    public struct UnlockPayload
    {
        [SerializeField] UnlockPayloadKind kind;
        [SerializeField] string targetId;
        [SerializeField, Min(0)] int amount;

        public UnlockPayloadKind Kind => kind;
        public string TargetId => targetId ?? string.Empty;
        public int Amount => amount;

        /// <summary>영웅·레시피·상점 아이템처럼 기존 콘텐츠 하나를 가리키는 종류인지.</summary>
        public bool IsContent => kind == UnlockPayloadKind.Hero || kind == UnlockPayloadKind.EquipmentRecipe || kind == UnlockPayloadKind.ShopItem;

        public UnlockPayload(UnlockPayloadKind kind, string targetId, int amount)
        {
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.amount = Mathf.Max(amount, 0);
        }

        public static UnlockPayload Hero(string heroId) => new UnlockPayload(UnlockPayloadKind.Hero, heroId, 1);
        public static UnlockPayload EquipmentRecipe(string itemId) => new UnlockPayload(UnlockPayloadKind.EquipmentRecipe, itemId, 1);
        public static UnlockPayload ShopItem(string itemId) => new UnlockPayload(UnlockPayloadKind.ShopItem, itemId, 1);
        public static UnlockPayload StartingGold(int gold) => new UnlockPayload(UnlockPayloadKind.StartingGold, string.Empty, gold);
        public static UnlockPayload StartingGrunts(int count) => new UnlockPayload(UnlockPayloadKind.StartingGrunts, string.Empty, count);
        public static UnlockPayload StartingItem(string itemId, int count) => new UnlockPayload(UnlockPayloadKind.StartingItem, itemId, count);
    }
}
