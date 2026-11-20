using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>스테이지 하나의 적 편성. 손으로 짠 웨이브 목록이며 같은 스테이지는 항상 같은 웨이브가 나온다(Q-51).</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "스테이지";
        [Tooltip("위에서부터 순서대로 나온다. 시작 시간은 오름차순으로 둔다.")]
        [SerializeField] StageWave[] waves = new StageWave[0];

        public string DisplayName => displayName;
        public IReadOnlyList<StageWave> Waves => waves;

        public static StageDefinition Create(string displayName, params StageWave[] waves)
        {
            var stage = CreateInstance<StageDefinition>();
            stage.displayName = displayName; stage.waves = waves ?? new StageWave[0];
            return stage;
        }
    }
}
