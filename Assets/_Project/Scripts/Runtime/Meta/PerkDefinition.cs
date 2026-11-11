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

        public string Id => id;
        public string DisplayName => displayName;
        public PerkCategory Category => category;
        public int PointCost => pointCost;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Perk;
        public IReadOnlyList<PerkStatEffect> StatEffects => statEffects;
        public IReadOnlyList<PerkSpecialEffect> SpecialEffects => specialEffects;

        /// <summary>코드에서 퍽을 만든다(테스트용).</summary>
        public static PerkDefinition Create(
            string id,
            PerkCategory category,
            IEnumerable<PerkStatEffect> statEffects = null,
            IEnumerable<PerkSpecialEffect> specialEffects = null,
            int pointCost = 1,
            bool unlockedByDefault = true)
        {
            var perk = CreateInstance<PerkDefinition>();
            perk.id = id;
            perk.displayName = id;
            perk.category = category;
            perk.pointCost = pointCost;
            perk.unlockedByDefault = unlockedByDefault;
            if (statEffects != null) perk.statEffects = new List<PerkStatEffect>(statEffects);
            if (specialEffects != null) perk.specialEffects = new List<PerkSpecialEffect>(specialEffects);
            return perk;
        }
    }
}
