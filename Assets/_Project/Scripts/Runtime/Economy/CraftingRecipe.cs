using System;

namespace ProjectNeo.Economy
{
    /// <summary>
    /// 대장간 고정 레시피 하나(Q-35). 만들 장비와 제작 비용(골드 + 몬스터 재료)을 담는다.
    /// 레시피 목록은 대장간 시설 레벨 데이터에 레벨마다 적는다.
    /// </summary>
    [Serializable]
    public struct CraftingRecipe
    {
        public ItemDefinition item;
        public Cost cost;

        public CraftingRecipe(ItemDefinition item, Cost cost)
        {
            this.item = item;
            this.cost = cost;
        }
    }
}
