using System;
using System.Collections.Generic;

namespace ProjectNeo.Economy
{
    /// <summary>아이템 강화 한 단계의 재료 비용. 목록의 첫 칸은 +1, 두 번째는 +2 비용이다.</summary>
    [Serializable]
    public struct ItemUpgradeMaterials
    {
        public List<MaterialAmount> materials;

        public ItemUpgradeMaterials(params MaterialAmount[] materials)
        {
            this.materials = new List<MaterialAmount>(materials);
        }
    }
}
