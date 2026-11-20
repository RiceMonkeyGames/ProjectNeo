using System;

namespace ProjectNeo.Meta
{
    /// <summary>"런의 무엇을 얼마나"를 담는 퍽 효과(META-02).</summary>
    [Serializable]
    public struct PerkRunEffect
    {
        public PerkRunEffectKind kind;
        public float value;

        public PerkRunEffect(PerkRunEffectKind kind, float value)
        {
            this.kind = kind;
            this.value = value;
        }
    }
}
