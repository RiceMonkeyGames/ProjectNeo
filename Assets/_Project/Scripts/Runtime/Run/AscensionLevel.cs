using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>승천 단계 하나(FLOW-03). 이 단계에서 새로 붙는 규칙과 화면에 보일 설명을 가진다.</summary>
    [Serializable]
    public sealed class AscensionLevel
    {
        [Tooltip("단계 선택 창에 보이는 한 줄 설명. 예: Enemy HP +15%")]
        public string description = string.Empty;
        public List<AscensionRule> rules = new List<AscensionRule>();

        public AscensionLevel() { }

        public AscensionLevel(string description, params AscensionRule[] rules)
        {
            this.description = description ?? string.Empty;
            this.rules = new List<AscensionRule>(rules ?? new AscensionRule[0]);
        }
    }
}
