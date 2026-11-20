using System;

namespace ProjectNeo.Run
{
    /// <summary>승천 단계 하나에 붙는 규칙 한 줄(FLOW-03). 값의 뜻은 <see cref="AscensionRuleKind"/>를 본다.</summary>
    [Serializable]
    public struct AscensionRule
    {
        public AscensionRuleKind kind;
        public float value;

        public AscensionRule(AscensionRuleKind kind, float value)
        {
            this.kind = kind;
            this.value = value;
        }
    }
}
