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

        public string Id => id;
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
    }
}
