using System;
using ProjectNeo.Economy;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>적 한 마리를 처치할 때마다 주는 몬스터 재료 한 줄(CONTENT-04).</summary>
    [Serializable]
    public struct EnemyMaterialDrop
    {
        public MaterialDefinition material;
        [Min(0)] public int amountPerKill;

        public EnemyMaterialDrop(MaterialDefinition material, int amountPerKill)
        {
            this.material = material;
            this.amountPerKill = amountPerKill;
        }
    }
}
