using System;
using System.Collections.Generic;
using ProjectNeo.Characters;

namespace ProjectNeo.Run
{
    /// <summary>시작 부대 하나의 잡졸 구성. 줄 하나가 잡졸 한 명이다.</summary>
    [Serializable]
    public struct RunSquadSetup
    {
        public List<SoldierDefinition> soldiers;

        public RunSquadSetup(params SoldierDefinition[] soldiers)
        {
            this.soldiers = new List<SoldierDefinition>(soldiers);
        }
    }
}
