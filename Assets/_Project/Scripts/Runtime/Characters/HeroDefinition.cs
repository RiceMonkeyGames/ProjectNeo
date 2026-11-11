using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Characters
{
    /// <summary>영웅 한 명의 데이터. 플레이어 캐릭터도 "조종하는 영웅"이라서 이 데이터를 쓴다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Hero", fileName = "Hero_")]
    public sealed class HeroDefinition : ScriptableObject, IUnlockable
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] bool unlockedByDefault = true;
        [SerializeField] Cost hireCost;
        [SerializeField, Min(0)] int equipmentSlots = 3;
        [SerializeField] List<StatValue> baseStats = new List<StatValue>();
        [SerializeField] List<SkillDefinition> skills = new List<SkillDefinition>();

        public string Id => id;
        public string DisplayName => displayName;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Hero;
        public Cost HireCost => hireCost;
        public int EquipmentSlots => equipmentSlots;
        public IReadOnlyList<StatValue> BaseStats => baseStats;
        public IReadOnlyList<SkillDefinition> Skills => skills;

        /// <summary>코드에서 영웅을 만든다(테스트용).</summary>
        public static HeroDefinition Create(string id, bool unlockedByDefault = true, int equipmentSlots = 3, params StatValue[] baseStats)
        {
            var hero = CreateInstance<HeroDefinition>();
            hero.id = id;
            hero.displayName = id;
            hero.unlockedByDefault = unlockedByDefault;
            hero.equipmentSlots = equipmentSlots;
            hero.baseStats = new List<StatValue>(baseStats);
            return hero;
        }
    }
}
