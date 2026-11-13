using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Characters
{
    /// <summary>잡졸 한 종류의 데이터. 훈련장에서 모집한다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Soldier", fileName = "Soldier_")]
    public sealed class SoldierDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Cost recruitCost;
        [SerializeField] List<StatValue> baseStats = new List<StatValue>();
        [Tooltip("준비 중에 모집한 잡졸을 씬에 만들 때 쓰는 아군 유닛 프리팹(D-0b). 이 정의를 쓰는 CharacterActor가 있어야 한다.")]
        [SerializeField] GameObject unitPrefab;

        public string Id => id;
        /// <summary>모집한 잡졸을 씬에 만들 프리팹. 없으면 씬에 만들 수 없다.</summary>
        public GameObject UnitPrefab => unitPrefab;
        public string DisplayName => displayName;
        public Cost RecruitCost => recruitCost;
        public IReadOnlyList<StatValue> BaseStats => baseStats;

        /// <summary>이 잡졸의 기본 스탯이 들어간 스탯 묶음을 새로 만든다.</summary>
        public StatSheet CreateStats()
        {
            var stats = new StatSheet();
            stats.SetBases(baseStats);
            return stats;
        }

        /// <summary>코드에서 잡졸을 만든다(테스트용).</summary>
        public static SoldierDefinition Create(string id, Cost recruitCost, params StatValue[] baseStats)
        {
            var soldier = CreateInstance<SoldierDefinition>();
            soldier.id = id;
            soldier.displayName = id;
            soldier.recruitCost = recruitCost;
            soldier.baseStats = new List<StatValue>(baseStats);
            return soldier;
        }

        /// <summary>코드에서 씬 유닛 프리팹을 정한다(테스트·에디터 설정용).</summary>
        public void SetUnitPrefab(GameObject prefab) => unitPrefab = prefab;
    }
}
