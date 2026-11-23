namespace ProjectNeo.Economy
{
    /// <summary>
    /// 장비 종류이자 장비 칸(UI-07, 사용자 2026-11-23). 장비를 낄 수 있는 캐릭터는 종류마다 한 칸씩 갖고,
    /// 같은 종류 칸이 차 있으면 새 장비로 바꿔 낀다. 저장 값이 바뀌지 않도록 숫자를 고정한다.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon = 0,
        Offhand = 1,
        Armor = 2,
        Accessory = 3,
        Helmet = 4,
    }

    /// <summary>장비 칸 순서와 이름.</summary>
    public static class EquipmentSlots
    {
        /// <summary>
        /// 장비 창에 그리는 칸 순서: 초상화 둘레를 오른쪽 위에서 왼쪽 아래로 투구, 방어구, 장신구, 보조무기, 무기
        /// (사용자 2026-11-23).
        /// </summary>
        public static readonly EquipmentSlot[] All =
            { EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Accessory, EquipmentSlot.Offhand, EquipmentSlot.Weapon };

        public static int Count => All.Length;

        public static string DisplayName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => "무기",
            EquipmentSlot.Offhand => "보조무기",
            EquipmentSlot.Armor => "방어구",
            EquipmentSlot.Accessory => "장신구",
            EquipmentSlot.Helmet => "투구",
            _ => slot.ToString(),
        };
    }
}
