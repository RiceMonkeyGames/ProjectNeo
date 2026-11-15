using ProjectNeo.Economy;

namespace ProjectNeo.Run
{
    /// <summary>전투 중 소모품을 쓸 수 있는지 판정한 결과(PREP-04).</summary>
    public enum ConsumableUseCheck
    {
        Ok = 0,
        /// <summary>보관함에 쓸 수 있는 소모품이 없다.</summary>
        NoItem = 1,
        /// <summary>쓰는 사람이 죽었다.</summary>
        Dead = 2,
        /// <summary>체력이 가득 차 있어 쓰지 않는다(낭비 방지).</summary>
        FullHealth = 3,
        /// <summary>재사용 대기 중이다.</summary>
        CoolingDown = 4,
        /// <summary>전투 중이 아니다.</summary>
        NotInBattle = 5,
    }

    /// <summary>
    /// 전투 소모품 사용 규칙(PREP-04). 지금은 최대 체력의 일정 비율을 바로 회복하는 회복약만 있다.
    /// 누가 어떤 키로 쓰는지는 알지 않는다.
    /// </summary>
    public static class ConsumableUse
    {
        /// <summary>회복 효과가 있는 소모품인지.</summary>
        public static bool IsUsable(ItemDefinition item) =>
            item != null && item.Kind == ItemKind.Consumable && item.UseHealRatio > 0f;

        /// <summary>보관함에서 먼저 얻은 순서대로 쓸 수 있는 소모품 하나를 고른다. 없으면 null.</summary>
        public static ItemInstance FindUsable(Inventory inventory)
        {
            if (inventory == null) return null;
            foreach (ItemInstance item in inventory.Items)
                if (item != null && IsUsable(item.Definition)) return item;
            return null;
        }

        /// <summary>보관함에 있는 쓸 수 있는 소모품 수.</summary>
        public static int CountUsable(Inventory inventory)
        {
            if (inventory == null) return 0;
            int count = 0;
            foreach (ItemInstance item in inventory.Items)
                if (item != null && IsUsable(item.Definition)) count++;
            return count;
        }

        /// <summary>지금 쓸 수 있는지. 죽음, 소모품 없음, 재사용 대기, 가득 찬 체력 순으로 막는다.</summary>
        public static ConsumableUseCheck Check(ItemInstance item, bool alive, float currentHealth, float maximumHealth,
            float cooldownRemaining)
        {
            if (!alive) return ConsumableUseCheck.Dead;
            if (item == null || !IsUsable(item.Definition)) return ConsumableUseCheck.NoItem;
            if (cooldownRemaining > 0f) return ConsumableUseCheck.CoolingDown;
            if (currentHealth >= maximumHealth) return ConsumableUseCheck.FullHealth;
            return ConsumableUseCheck.Ok;
        }

        /// <summary>회복량. 최대 체력에 아이템의 회복 비율을 곱한다. 넘치는 양은 체력 쪽에서 자른다.</summary>
        public static float HealAmount(ItemDefinition item, float maximumHealth)
        {
            if (!IsUsable(item) || float.IsNaN(maximumHealth) || maximumHealth <= 0f) return 0f;
            return maximumHealth * item.UseHealRatio;
        }
    }
}
