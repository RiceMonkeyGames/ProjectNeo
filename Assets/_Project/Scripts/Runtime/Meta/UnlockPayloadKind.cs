namespace ProjectNeo.Meta
{
    /// <summary>해금 항목을 열면 무엇이 풀리는지. 저장 값이므로 숫자를 바꾸지 않는다.</summary>
    public enum UnlockPayloadKind
    {
        /// <summary>선술집에 등장할 수 있는 영웅. targetId = 영웅 id.</summary>
        Hero = 0,
        /// <summary>대장간 장비 레시피. targetId = 아이템 id.</summary>
        EquipmentRecipe = 1,
        /// <summary>상점이 들여놓을 수 있는 아이템(소모품 포함). targetId = 아이템 id.</summary>
        ShopItem = 2,
        /// <summary>런 시작 골드. amount = 골드.</summary>
        StartingGold = 3,
        /// <summary>런 시작 일반병. amount = 인원.</summary>
        StartingGrunts = 4,
        /// <summary>런 시작 아이템(소모품 등). targetId = 아이템 id, amount = 개수.</summary>
        StartingItem = 5,
    }
}
