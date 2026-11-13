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
        [Tooltip("영웅 등급(D-3). 주점 레벨별 등장 풀과 고용 비용의 기준이다.")]
        [SerializeField, Min(1)] int tier = 1;
        [Tooltip("고용한 영웅의 씬 몸체(D-3). UnitCombatAI와 CharacterActor가 있는 프리팹이며, 실행 중에 이 영웅 정의로 다시 설정한다.")]
        [SerializeField] GameObject unitPrefab;

        public string Id => id;
        public string DisplayName => displayName;
        public bool UnlockedByDefault => unlockedByDefault;
        public UnlockCategory UnlockCategory => UnlockCategory.Hero;
        public Cost HireCost => hireCost;
        public int EquipmentSlots => equipmentSlots;
        public IReadOnlyList<StatValue> BaseStats => baseStats;
        public IReadOnlyList<SkillDefinition> Skills => skills;
        public int Tier => tier;
        public GameObject UnitPrefab => unitPrefab;
        public void SetUnitPrefab(GameObject prefab) => unitPrefab = prefab;

        /// <summary>코드에서 영웅을 만든다(테스트용).</summary>
        public static HeroDefinition Create(string id, bool unlockedByDefault = true, int equipmentSlots = 3, params StatValue[] baseStats)
        {
            var hero = CreateInstance<HeroDefinition>();
            hero.id = id;
            hero.displayName = id;
            hero.unlockedByDefault = unlockedByDefault;
            hero.equipmentSlots = equipmentSlots;
            hero.baseStats = new List<StatValue>(baseStats);
            hero.hireCost = new Cost(0);
            return hero;
        }

        /// <summary>코드에서 고용할 수 있는 영웅을 만든다(테스트용, D-3).</summary>
        public static HeroDefinition Create(string id, int tier, Cost hireCost, bool unlockedByDefault = true)
        {
            HeroDefinition hero = Create(id, unlockedByDefault);
            hero.tier = Mathf.Max(1, tier);
            hero.hireCost = hireCost;
            return hero;
        }
    }
}
