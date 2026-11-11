using System;

namespace ProjectNeo.Stats
{
    /// <summary>"어떤 스탯이 얼마"를 담는 한 줄. 영웅과 잡졸의 기본 스탯을 에셋에 적을 때 쓴다.</summary>
    [Serializable]
    public struct StatValue
    {
        public StatType stat;
        public float value;

        public StatValue(StatType stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }
}
