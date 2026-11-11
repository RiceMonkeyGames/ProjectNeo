using System;
using System.Collections.Generic;

namespace ProjectNeo.Economy
{
    [Serializable]
    public struct MaterialAmount
    {
        public MaterialDefinition material;
        public int amount;

        public MaterialAmount(MaterialDefinition material, int amount)
        {
            this.material = material;
            this.amount = amount;
        }
    }

    /// <summary>
    /// 모든 사용처가 같이 쓰는 비용 데이터. 골드 칸과 재료 칸을 둘 다 두고, 필요 없는 칸은 0이나 빈 목록으로 둔다.
    /// </summary>
    [Serializable]
    public struct Cost
    {
        public int gold;
        public List<MaterialAmount> materials;

        public Cost(int gold, params MaterialAmount[] materials)
        {
            this.gold = gold;
            this.materials = new List<MaterialAmount>(materials);
        }

        public bool IsFree
        {
            get
            {
                if (gold > 0) return false;
                if (materials == null) return true;
                foreach (MaterialAmount entry in materials)
                    if (entry.amount > 0) return false;
                return true;
            }
        }
    }
}
