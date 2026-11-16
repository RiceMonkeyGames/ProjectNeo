using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>런의 스테이지 순서. 목록 길이가 런 길이다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Stage Catalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [Tooltip("첫 줄이 1 스테이지다.")]
        [SerializeField] StageDefinition[] stages = new StageDefinition[0];

        public IReadOnlyList<StageDefinition> Stages => stages;
        public int Count => stages.Length;

        /// <summary>
        /// 스테이지 번호(1부터)에 맞는 편성. 목록을 넘어가면 마지막 스테이지를 되풀이한다
        /// (런 엔딩 Q-08이 연결되기 전의 임시 규칙, Q-52). 목록이 비었으면 null.
        /// </summary>
        public StageDefinition StageFor(int stageNumber)
        {
            if (stages.Length == 0) return null;
            return stages[Mathf.Clamp(stageNumber - 1, 0, stages.Length - 1)];
        }

        public static StageCatalog Create(params StageDefinition[] stages)
        {
            var catalog = CreateInstance<StageCatalog>();
            catalog.stages = stages ?? new StageDefinition[0];
            return catalog;
        }
    }
}
