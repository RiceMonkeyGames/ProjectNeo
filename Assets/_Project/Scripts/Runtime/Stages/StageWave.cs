using System;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>웨이브 안의 적 한 무리: 누가, 몇 명, 어디서.</summary>
    [Serializable]
    public struct StageWaveGroup
    {
        [Tooltip("기존 적 프리팹. SiegeAttacker가 있고 비활성 상태여야 한다.")]
        public GameObject enemyPrefab;
        [Min(0)] public int count;
        [Tooltip("비우면 전투 설정의 기본 스폰 칸을 쓴다.")]
        public SpawnPointDefinition spawnPoint;
    }

    /// <summary>웨이브 하나: 전투 시작 뒤 몇 초에 시작하고 어떤 무리가 나오는지.</summary>
    [Serializable]
    public struct StageWave
    {
        [Tooltip("전투 시작 뒤 이 웨이브가 나오는 시간(초). 첫 웨이브는 0.")]
        [Min(0f)] public float startSeconds;
        public StageWaveGroup[] groups;
    }
}
