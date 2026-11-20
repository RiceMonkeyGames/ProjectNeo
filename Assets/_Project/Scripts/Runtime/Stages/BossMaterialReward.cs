using System;
using ProjectNeo.Economy;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>보스 처치 보상의 재료 한 줄(SIEGE-09).</summary>
    [Serializable]
    public struct BossMaterialReward
    {
        public MaterialDefinition material;
        [Min(0)] public int amount;

        public BossMaterialReward(MaterialDefinition material, int amount)
        {
            this.material = material;
            this.amount = amount;
        }
    }
}
