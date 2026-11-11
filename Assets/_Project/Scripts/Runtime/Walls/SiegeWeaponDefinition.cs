using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 수성 무기 한 종류의 공통 데이터. 대포·발리스타 등 새 무기는 이 에셋과 프리팹을 추가해서 만든다.
    /// 무기별 동작은 프리팹에 붙인 ISiegeWeaponModule 부품이 맡는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Siege Weapon")]
    public sealed class SiegeWeaponDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Vector3 size = Vector3.one;
        [SerializeField] GameObject prefab;
        public string Id => id;
        public string DisplayName => displayName;
        // 실제 크기(미터). 외형 모델에서 가져오지 않는다.
        public Vector3 Size => size;
        // 프리팹은 가로·높이·세로 1, 바닥 y=0 기준. 루트에 PlacedSiegeWeapon, 외형은 Visual 자식에 둔다.
        public GameObject Prefab => prefab;

        public static SiegeWeaponDefinition Create(string id, string name, Vector3 size, GameObject prefab = null)
        {
            var value = CreateInstance<SiegeWeaponDefinition>();
            value.id = id;
            value.displayName = name;
            value.size = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), Mathf.Max(0.1f, size.z));
            value.prefab = prefab;
            return value;
        }
    }
}
