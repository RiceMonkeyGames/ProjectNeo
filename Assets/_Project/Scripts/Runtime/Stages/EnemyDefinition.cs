using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>
    /// 일반 적 한 종류(CONTENT-04). 편성·미리보기·처치 보상이 프리팹 이름 대신 이 정의를 읽는다.
    /// <see cref="Id"/>는 처치·퇴각 집계의 키이며 한 번 정하면 바꾸지 않는다(META-10 이름 변경 표와 맞춘다).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("처치·퇴각 집계와 보상이 쓰는 고유 ID. 한 번 정하면 바꾸지 않는다.")]
        [SerializeField] string id = "enemy";
        [Tooltip("준비 단계 미리보기 등에 보이는 이름.")]
        [SerializeField] string displayName = "적";
        [SerializeField] EnemyRole role = EnemyRole.GroundLadder;
        [Tooltip("전장에 만들 적 프리팹. SiegeAttacker가 있고 비활성 상태여야 한다.")]
        [SerializeField] GameObject prefab;
        [Tooltip("사기의 영향을 받는지. 적은 사기가 없으므로(Q-44) 기본은 꺼짐이며 지금은 어디서도 읽지 않는다.")]
        [SerializeField] bool affectedByMorale;

        [Header("처치 보상")]
        [Tooltip("한 마리 처치마다 주는 골드.")]
        [SerializeField, Min(0)] int killGold;
        [Tooltip("한 마리 처치마다 주는 몬스터 재료.")]
        [SerializeField] EnemyMaterialDrop[] materialDrops = new EnemyMaterialDrop[0];

        public string Id => id;
        public string DisplayName => displayName;
        public EnemyRole Role => role;
        public GameObject Prefab => prefab;
        public bool AffectedByMorale => affectedByMorale;
        public int KillGold => killGold;
        public IReadOnlyList<EnemyMaterialDrop> MaterialDrops => materialDrops;

        public static EnemyDefinition Create(string id, string displayName, EnemyRole role, GameObject prefab,
            int killGold = 0, params EnemyMaterialDrop[] materialDrops)
        {
            var enemy = CreateInstance<EnemyDefinition>();
            enemy.name = id;
            enemy.id = id; enemy.displayName = displayName; enemy.role = role; enemy.prefab = prefab;
            enemy.killGold = Mathf.Max(0, killGold);
            enemy.materialDrops = materialDrops ?? new EnemyMaterialDrop[0];
            return enemy;
        }
    }
}
