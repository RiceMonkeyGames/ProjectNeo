using System;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>웨이브 안의 적 한 무리: 누가, 몇 명, 어디서.</summary>
    [Serializable]
    public struct StageWaveGroup
    {
        [Tooltip("나올 적 종류(CONTENT-04). 이름·보상·프리팹을 여기서 읽는다.")]
        public EnemyDefinition enemy;
        [Tooltip("예비값: 적 종류를 비웠을 때만 쓰는 적 프리팹. SiegeAttacker가 있고 비활성 상태여야 한다.")]
        public GameObject enemyPrefab;
        [Min(0)] public int count;
        [Tooltip("비우면 전투 설정의 기본 스폰 칸을 쓴다.")]
        public SpawnPointDefinition spawnPoint;
        [Tooltip("켜면 준비 단계 미리보기에서 종류를 숨기고 '? × 수'로만 보인다(C-5, Q-53). 전장에 나오면 공개된다.")]
        public bool hidden;

        /// <summary>실제로 만들 프리팹. 적 종류가 있으면 그 프리팹, 없으면 예비 프리팹.</summary>
        public GameObject Prefab => enemy != null ? enemy.Prefab : enemyPrefab;
    }

    /// <summary>웨이브 하나: 전투 시작 뒤 몇 초에 시작하고 어떤 무리가 나오는지.</summary>
    [Serializable]
    public struct StageWave
    {
        [Tooltip("전투 시작 뒤 이 웨이브가 나오는 시간(초). 첫 웨이브는 0.")]
        [Min(0f)] public float startSeconds;
        public StageWaveGroup[] groups;
        [Tooltip("이 웨이브와 함께 나오는 보스(SIEGE-09). 스테이지마다 마지막 웨이브에 하나만 둔다. 비우면 보스가 없다.")]
        public BossDefinition boss;
        [Tooltip("보스가 나오는 지점. 비우면 전투 설정의 기본 스폰 칸을 쓴다.")]
        public SpawnPointDefinition bossSpawnPoint;
    }
}
