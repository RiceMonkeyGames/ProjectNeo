using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Meta
{
    public enum PerkCategory
    {
        /// <summary>전투: 플레이어 캐릭터가 강해진다.</summary>
        Combat = 0,
        /// <summary>내실: 골드, 아이템 등 준비 요소에 도움을 준다.</summary>
        Economy = 1,
        /// <summary>유닛 강화: 잡졸 또는 영웅을 강화한다.</summary>
        UnitBoost = 2,
    }

    /// <summary>스탯 효과가 붙는 대상.</summary>
    public enum PerkTarget
    {
        Player = 0,
        Heroes = 1,
        Soldiers = 2,
    }

    /// <summary>"누구의 어떤 스탯을 얼마나"를 담는 퍽 효과.</summary>
    [Serializable]
    public struct PerkStatEffect
    {
        public PerkTarget target;
        public StatModifier modifier;

        public PerkStatEffect(PerkTarget target, StatModifier modifier)
        {
            this.target = target;
            this.modifier = modifier;
        }
    }

    /// <summary>
    /// 퍽 하나의 데이터. 효과는 두 종류를 목록으로 붙인다:
    /// 스탯 효과(스탯 수정값)와 특수 효과(전투 중 특정 시점에 발동).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Perk", fileName = "Perk_")]
    public sealed class PerkDefinition : ScriptableObject, IUnlockable
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] PerkCategory category;
        [SerializeField, Min(1)] int pointCost = 1;
        [SerializeField] bool unlockedByDefault = true;
        [SerializeField] List<PerkStatEffect> statEffects = new List<PerkStatEffect>();
        [SerializeField] List<PerkSpecialEffect> specialEffects = new List<PerkSpecialEffect>();
        [Tooltip("런 전체 효과(보상 골드·상점 가격·준비 시간, META-02).")]
        [SerializeField] List<PerkRunEffect> runEffects = new List<PerkRunEffect>();
        [Tooltip("이 퍽을 찍기 전에 모두 찍어 두어야 하는 퍽(META-09 퍽 트리).")]
        [SerializeField] List<PerkDefinition> prerequisites = new List<PerkDefinition>();
        [Tooltip("퍽 창에 보여 줄 한 줄 설명. 비어 있으면 효과로 만든 설명을 쓴다.")]
        [SerializeField] string description;
        [Tooltip("이 퍽을 찍으면 배우는 플레이어 액티브 스킬(COMBAT-06, Q-72). 비우면 스킬을 주지 않는다.")]
        [SerializeField] ProjectNeo.Skills.SkillDefinition grantedSkill;

        /// <summary>이 퍽으로 배우는 플레이어 스킬. 없으면 null.</summary>
        public ProjectNeo.Skills.SkillDefinition GrantedSkill => grantedSkill;

        public string Id => id;
        public string DisplayName => displayName;
        public PerkCategory Category => category;
        public int PointCost => pointCost;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Perk;
        public IReadOnlyList<PerkStatEffect> StatEffects => statEffects;
        public IReadOnlyList<PerkSpecialEffect> SpecialEffects => specialEffects;
        public IReadOnlyList<PerkRunEffect> RunEffects => runEffects ??= new List<PerkRunEffect>();
        public IReadOnlyList<PerkDefinition> Prerequisites => prerequisites ??= new List<PerkDefinition>();
        public string Description => description ?? string.Empty;

        /// <summary>이 퍽의 런 효과 중 한 종류의 합.</summary>
        public float RunEffectTotal(PerkRunEffectKind kind)
        {
            float total = 0f;
            foreach (PerkRunEffect effect in RunEffects)
                if (effect.kind == kind) total += effect.value;
            return total;
        }

        /// <summary>코드에서 퍽을 만든다(테스트·샘플용).</summary>
        public static PerkDefinition Create(
            string id,
            PerkCategory category,
            IEnumerable<PerkStatEffect> statEffects = null,
            IEnumerable<PerkSpecialEffect> specialEffects = null,
            int pointCost = 1,
            bool unlockedByDefault = true,
            IEnumerable<PerkRunEffect> runEffects = null,
            string displayName = null,
            IEnumerable<PerkDefinition> prerequisites = null,
            string description = null,
            ProjectNeo.Skills.SkillDefinition grantedSkill = null)
        {
            var perk = CreateInstance<PerkDefinition>();
            perk.id = id;
            perk.grantedSkill = grantedSkill;
            perk.displayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            if (runEffects != null) perk.runEffects = new List<PerkRunEffect>(runEffects);
            if (prerequisites != null) foreach (PerkDefinition required in prerequisites) if (required != null) perk.prerequisites.Add(required);
            perk.description = description ?? string.Empty;
            perk.category = category;
            perk.pointCost = pointCost;
            perk.unlockedByDefault = unlockedByDefault;
            if (statEffects != null) perk.statEffects = new List<PerkStatEffect>(statEffects);
            if (specialEffects != null) perk.specialEffects = new List<PerkSpecialEffect>(specialEffects);
            return perk;
        }
    }
}
