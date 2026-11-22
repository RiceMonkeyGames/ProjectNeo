using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>성가퀴 치수(SIEGE-10). 높이는 성벽 위 걷는 면에서 잰다. 임시 값이며 플레이 후 조정한다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Battlement Settings")]
    public sealed class BattlementSettings : ScriptableObject
    {
        [SerializeField, Min(0.05f)] float thickness = 0.4f;
        // 웅크린 2m 유닛(1.1m)이 다 가려지고 선 유닛은 상체가 드러나는 높이(COMBAT-10, 0.8m에서 올림).
        [SerializeField, Min(0.1f)] float parapetHeight = 1.1f;
        [SerializeField, Min(0.1f)] float merlonHeight = 2.2f;
        [SerializeField, Min(0.1f)] float merlonWidth = 0.75f;
        [SerializeField, Min(0.1f)] float crenelWidth = 0.75f;
        [SerializeField] Material material;
        // 0이면 기본 상자 메쉬(텍스처가 조각마다 늘어남). 0보다 크면 이 길이(m)마다 텍스처가 한 번 반복된다(ART-06, 외형 전용).
        [SerializeField, Min(0f)] float textureTileMeters;

        /// <summary>성벽 안쪽으로 차지하는 두께.</summary>
        public float Thickness => thickness;
        /// <summary>난간 높이. 성가퀴에서는 빈틈 아래 받침 높이이며, 사다리 꼭대기가 걸리는 높이다.</summary>
        public float ParapetHeight => parapetHeight;
        /// <summary>성가퀴의 솟은 부분 높이.</summary>
        public float MerlonHeight => Mathf.Max(merlonHeight, parapetHeight);
        public float MerlonWidth => merlonWidth;
        /// <summary>솟은 부분 사이 빈틈 폭. 사다리(폭 0.7m)가 들어가도록 그보다 넓게 둔다.</summary>
        public float CrenelWidth => crenelWidth;
        /// <summary>비어 있으면 받치는 성벽의 재질을 쓴다.</summary>
        public Material Material => material;
        /// <summary>텍스처 반복 길이(m). 0이면 반복하지 않는다(기본 상자).</summary>
        public float TextureTileMeters => textureTileMeters;

        public static BattlementSettings Create(float thickness = 0.4f, float parapetHeight = 1.1f, float merlonHeight = 2.2f,
            float merlonWidth = 0.75f, float crenelWidth = 0.75f, Material material = null, float textureTileMeters = 0f)
        {
            var value = CreateInstance<BattlementSettings>();
            value.textureTileMeters = Mathf.Max(0f, textureTileMeters);
            value.thickness = Mathf.Max(0.05f, thickness);
            value.parapetHeight = Mathf.Max(0.1f, parapetHeight);
            value.merlonHeight = Mathf.Max(0.1f, merlonHeight);
            value.merlonWidth = Mathf.Max(0.1f, merlonWidth);
            value.crenelWidth = Mathf.Max(0.1f, crenelWidth);
            value.material = material;
            return value;
        }
    }
}
