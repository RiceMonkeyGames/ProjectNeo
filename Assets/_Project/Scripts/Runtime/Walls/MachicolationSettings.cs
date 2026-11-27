using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성문 위 마치콜레이션 치수와 AI 사용 값(SIEGE-12). 거리는 m. 임시 값이며 플레이 후 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Machicolation Settings")]
    public sealed class MachicolationSettings : ScriptableObject
    {
        // 사격 자리: 바깥 면 가운데(성가퀴 빈틈 가운데)에서 안쪽으로 이만큼(성가퀴 두께 0.4 + 빈틈 사격 자리 0.45).
        [SerializeField, Min(0.1f)] float standInset = 0.85f;
        // 유닛 발이 사격 자리에서 이 수평 거리 안이면 그 구멍을 쓰는 것으로 본다.
        [SerializeField, Min(0.1f)] float standRadius = 0.6f;
        // 통로 폭(면을 따라). 성문 한 칸(7.5m)을 다 덮는다(SIEGE-22: 성문 면에 붙는 성벽 파괴병까지).
        [SerializeField, Min(0.1f)] float shaftWidth = 7.5f;
        // 통로가 바깥 면에서 안쪽으로 뻗는 길이. 사격 자리(0.85m)를 덮는다. 킷 성문 문짝(면에서 0.33~0.78m 안)은
        // 통로 안에 들지만 문짝 뒤는 성문 통로라, 닫힌 문 너머로 쏠 적이 없다(SIEGE-22).
        [SerializeField, Min(0f)] float shaftInward = 1.2f;
        // 통로가 바깥 면에서 바깥쪽으로 뻗는 길이. 성문 앞에 붙은 적을 덮는다.
        [SerializeField, Min(0f)] float shaftOutward = 2f;
        // AI가 지키는 자리에서 이 거리 안의 구멍만 쓰러 간다(7.5m 칸 두 칸 남짓).
        [SerializeField, Min(0f)] float searchRadius = 15f;
        // 외형(돌출 받침) 치수. 충돌체는 없다. 7.5m 칸·5m 층(SIEGE-22): 킷 성문 아치 폭(약 4.3m)에 맞추고,
        // 킷 벽판이 면 밖으로 약 0.3~0.5m 나오므로 그만큼 더 깊게 둔다.
        [SerializeField, Min(0.05f)] float overhangDepth = 0.9f;
        [SerializeField, Min(0.05f)] float overhangHeight = 0.8f;
        [SerializeField, Min(0.05f)] float overhangWidth = 4.3f;
        [SerializeField] Material material;
        [SerializeField] Material holeMaterial;
        // 구멍에서 쓸 수 있는 공격 방식(모듈). 공격자 종류로 자동으로 정해진다. 지금은 화살만.
        [SerializeField] ProjectNeo.Combat.MachicolationAttack[] attacks = System.Array.Empty<ProjectNeo.Combat.MachicolationAttack>();

        public float StandInset => standInset;
        public float StandRadius => standRadius;
        public float ShaftWidth => shaftWidth;
        public float ShaftInward => shaftInward;
        public float ShaftOutward => shaftOutward;
        public float SearchRadius => searchRadius;
        public float OverhangDepth => overhangDepth;
        public float OverhangHeight => overhangHeight;
        public float OverhangWidth => overhangWidth;
        /// <summary>비어 있으면 받치는 조각의 재질을 쓴다.</summary>
        public Material Material => material;
        /// <summary>구멍 표시 재질. 비어 있으면 구멍 표시를 만들지 않는다.</summary>
        public Material HoleMaterial => holeMaterial;
        public ProjectNeo.Combat.MachicolationAttack[] Attacks => attacks;

        public static MachicolationSettings Create(ProjectNeo.Combat.MachicolationAttack[] attacks, float standInset = 0.85f,
            float standRadius = 0.6f, float shaftWidth = 7.5f, float shaftInward = 1.2f, float shaftOutward = 2f,
            float searchRadius = 15f)
        {
            var value = CreateInstance<MachicolationSettings>();
            value.attacks = attacks ?? System.Array.Empty<ProjectNeo.Combat.MachicolationAttack>();
            value.standInset = Mathf.Max(0.1f, standInset);
            value.standRadius = Mathf.Max(0.1f, standRadius);
            value.shaftWidth = Mathf.Max(0.1f, shaftWidth);
            value.shaftInward = Mathf.Max(0f, shaftInward);
            value.shaftOutward = Mathf.Max(0f, shaftOutward);
            value.searchRadius = Mathf.Max(0f, searchRadius);
            return value;
        }

        /// <summary>에디터 설정 메뉴가 재질을 채운다.</summary>
        public void SetMaterials(Material body, Material hole)
        {
            material = body;
            holeMaterial = hole;
        }

        /// <summary>에디터 설정 메뉴가 공격 방식 모듈을 채운다.</summary>
        public void SetAttacks(ProjectNeo.Combat.MachicolationAttack[] value) =>
            attacks = value ?? System.Array.Empty<ProjectNeo.Combat.MachicolationAttack>();
    }
}
