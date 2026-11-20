using System;
using ProjectNeo.Economy;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 한 등급 올라가는 승급 한 단계(PREP-10). 목록의 0번이 1→2등급, 1번이 2→3등급이다.
    /// </summary>
    [Serializable]
    public struct HeroGradeStep
    {
        /// <summary>이 단계로 승급하려면 모아야 하는 전투 경험치.</summary>
        public int expRequired;
        /// <summary>승급 비용(골드, 필요하면 재료).</summary>
        public Cost cost;

        public HeroGradeStep(int expRequired, Cost cost)
        {
            this.expRequired = expRequired;
            this.cost = cost;
        }
    }
}
