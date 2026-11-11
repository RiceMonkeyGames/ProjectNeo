using System;
using System.Collections.Generic;

namespace ProjectNeo.Stats
{
    /// <summary>
    /// 캐릭터나 유닛 하나의 스탯 묶음.
    /// 최종값 = (기본값 + Flat 합계) × (1 + Percent 합계), 0 미만으로는 내려가지 않는다.
    /// 수정값은 출처(퍽, 장비 등)와 함께 붙이고, 출처 단위로 뗀다.
    /// </summary>
    public sealed class StatSheet
    {
        struct Entry
        {
            public object source;
            public StatModifier modifier;
        }

        readonly Dictionary<StatType, float> baseValues = new Dictionary<StatType, float>();
        readonly List<Entry> entries = new List<Entry>();

        /// <summary>해당 스탯의 기본값이나 수정값이 바뀌면 호출된다.</summary>
        public event Action<StatType> Changed;

        public float GetBase(StatType stat) => baseValues.TryGetValue(stat, out float value) ? value : 0f;

        public void SetBase(StatType stat, float value)
        {
            baseValues[stat] = value;
            Changed?.Invoke(stat);
        }

        public void SetBases(IEnumerable<StatValue> values)
        {
            if (values == null) return;
            foreach (StatValue entry in values)
                SetBase(entry.stat, entry.value);
        }

        public void AddModifier(StatModifier modifier, object source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            entries.Add(new Entry { source = source, modifier = modifier });
            Changed?.Invoke(modifier.stat);
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers, object source)
        {
            if (modifiers == null) return;
            foreach (StatModifier modifier in modifiers)
                AddModifier(modifier, source);
        }

        /// <summary>이 출처가 붙인 수정값을 모두 떼고, 뗀 개수를 돌려준다.</summary>
        public int RemoveModifiersFrom(object source)
        {
            int removed = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(entries[i].source, source)) continue;
                StatType stat = entries[i].modifier.stat;
                entries.RemoveAt(i);
                removed++;
                Changed?.Invoke(stat);
            }
            return removed;
        }

        public float GetValue(StatType stat)
        {
            float flat = 0f;
            float percent = 0f;
            foreach (Entry entry in entries)
            {
                if (entry.modifier.stat != stat) continue;
                if (entry.modifier.kind == ModifierKind.Flat) flat += entry.modifier.value;
                else percent += entry.modifier.value;
            }
            float value = (GetBase(stat) + flat) * (1f + percent);
            return value < 0f ? 0f : value;
        }
    }
}
