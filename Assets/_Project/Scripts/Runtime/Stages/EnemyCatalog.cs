using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>
    /// 적 정의 목록(CONTENT-04). 처치 ID로 보상을 찾고, 프리팹만 있는 예전 편성(디버그 목록·프로토타입)에서 정의를 찾는다.
    /// 같은 ID나 같은 프리팹이 두 번 있으면 앞의 것을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Enemy Catalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] EnemyDefinition[] enemies = new EnemyDefinition[0];

        public IReadOnlyList<EnemyDefinition> Enemies => enemies;

        /// <summary>ID로 찾는다. 없으면 null.</summary>
        public EnemyDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (EnemyDefinition enemy in enemies)
                if (enemy != null && enemy.Id == id) return enemy;
            return null;
        }

        /// <summary>프리팹으로 찾는다. 없으면 null.</summary>
        public EnemyDefinition FindByPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            foreach (EnemyDefinition enemy in enemies)
                if (enemy != null && enemy.Prefab == prefab) return enemy;
            return null;
        }

        public static EnemyCatalog Create(params EnemyDefinition[] enemies)
        {
            var catalog = CreateInstance<EnemyCatalog>();
            catalog.enemies = enemies ?? new EnemyDefinition[0];
            return catalog;
        }
    }
}
