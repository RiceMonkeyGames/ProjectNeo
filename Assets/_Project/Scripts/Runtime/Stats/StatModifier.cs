using System;

namespace ProjectNeo.Stats
{
    public enum ModifierKind
    {
        /// <summary>기본값에 그대로 더한다. 예: 공격력 +5</summary>
        Flat = 0,
        /// <summary>비율로 더한다. 0.1이 +10%이고, 여러 개면 합산한다.</summary>
        Percent = 1,
    }

    /// <summary>
    /// 스탯 하나를 바꾸는 수정값. 퍽, 장비, 시설 효과, 사기 효과가 모두 이 데이터로 스탯에 붙는다.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierKind kind;
        public float value;

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            this.stat = stat;
            this.kind = kind;
            this.value = value;
        }

        public static StatModifier Flat(StatType stat, float value) => new StatModifier(stat, ModifierKind.Flat, value);
        public static StatModifier Percent(StatType stat, float value) => new StatModifier(stat, ModifierKind.Percent, value);
    }
}
