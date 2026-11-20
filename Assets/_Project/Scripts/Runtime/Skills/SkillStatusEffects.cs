using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 한 유닛에 걸린 스킬 버프·디버프(COMBAT-06). 처음 걸릴 때 실행 중에 붙는다.
    /// 스킬의 효과 하나마다 수정값 출처가 하나라서, 같은 효과를 다시 받으면 겹치지 않고 시간만 새로 고친다.
    /// 죽거나 꺼지면 모두 풀린다.
    /// </summary>
    public sealed class SkillStatusEffects : MonoBehaviour
    {
        readonly SkillStatusTimers timers = new SkillStatusTimers();
        readonly Dictionary<(SkillDefinition, int), Source> sources = new Dictionary<(SkillDefinition, int), Source>();
        readonly List<object> expired = new List<object>();
        CharacterActor actor;

        /// <summary>수정값 출처. 어떤 스킬의 몇 번째 효과인지 디버그로 볼 수 있다.</summary>
        sealed class Source
        {
            public readonly SkillDefinition Skill;
            public readonly int EffectIndex;
            public Source(SkillDefinition skill, int effectIndex) { Skill = skill; EffectIndex = effectIndex; }
        }

        public int ActiveCount => timers.Count;

        public static SkillStatusEffects On(CharacterActor target)
        {
            if (target == null) return null;
            SkillStatusEffects effects = target.GetComponent<SkillStatusEffects>();
            return effects != null ? effects : target.gameObject.AddComponent<SkillStatusEffects>();
        }

        public bool Has(SkillDefinition skill, int effectIndex) =>
            sources.TryGetValue((skill, effectIndex), out Source source) && timers.Has(source);

        public float Remaining(SkillDefinition skill, int effectIndex) =>
            sources.TryGetValue((skill, effectIndex), out Source source) ? timers.Remaining(source) : 0f;

        /// <summary>스킬 효과의 수정값을 <paramref name="duration"/>초 동안 건다.</summary>
        public void Apply(SkillDefinition skill, int effectIndex, StatModifier modifier, float duration)
        {
            if (skill == null || duration <= 0f || !EnsureActor()) return;
            if (!sources.TryGetValue((skill, effectIndex), out Source source))
            {
                source = new Source(skill, effectIndex);
                sources[(skill, effectIndex)] = source;
            }
            if (timers.Apply(source, duration)) actor.Stats.AddModifier(modifier, source);
        }

        void Update()
        {
            if (timers.Count == 0) return;
            expired.Clear();
            timers.Tick(Time.deltaTime, expired);
            Remove(expired);
        }

        void Awake() => actor = GetComponent<CharacterActor>();

        void OnEnable()
        {
            if (actor != null) actor.Died += ClearAll;
        }

        void OnDisable()
        {
            if (actor != null) actor.Died -= ClearAll;
            ClearAll();
        }

        public void ClearAll()
        {
            expired.Clear();
            timers.Clear(expired);
            Remove(expired);
        }

        void Remove(List<object> keys)
        {
            if (keys.Count == 0 || !EnsureActor()) return;
            foreach (object key in keys) actor.Stats.RemoveModifiersFrom(key);
        }

        bool EnsureActor() => actor != null && actor.Stats != null;
    }
}
