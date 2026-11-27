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
        // true면 면 가운데가 솟은 부분이고 빈틈은 그 양옆으로 반 주기 떨어진다(킷 Battlements_01 무늬, SIEGE-20).
        // false면 면 가운데가 빈틈이다(SIEGE-10 상자 성가퀴).
        [SerializeField] bool merlonAtMiddle;
        // 0보다 크면 난간(Lv1)도 이어진 받침이 아니라 낮은 돌이다: 빈틈 받침은 이 높이, 솟은 돌은 난간 높이로 성가퀴와 같은 무늬를 따른다.
        // Game은 킷 Battlements_01을 성벽에 묻은 모양(빈틈 0.224m, 돌 1.216m)이다(SIEGE-20, 사용자 결정 2026-11-27). 0이면 이어진 받침이다.
        [SerializeField, Min(0f)] float parapetGapSill;
        // 성가퀴(Lv2) 덩어리를 칸 가장자리에서 바깥으로 내미는 거리(m). 킷 Battlements_03은 성벽 바깥으로 내밀려 있다(SIEGE-20).
        [SerializeField, Min(0f)] float crenellatedOutset;
        [SerializeField] Material material;
        // 0이면 기본 상자 메쉬(텍스처가 조각마다 늘어남). 0보다 크면 이 길이(m)마다 텍스처가 한 번 반복된다(ART-06, 외형 전용).
        [SerializeField, Min(0f)] float textureTileMeters;

        /// <summary>성벽 안쪽으로 차지하는 두께.</summary>
        public float Thickness => thickness;
        /// <summary>
        /// 난간 높이. 성가퀴에서는 빈틈 아래 받침 높이이며, 사다리 꼭대기가 걸리는 높이다. 낮은 돌 난간(<see cref="ParapetGapSill"/>)에서는
        /// 돌 꼭대기 높이다. Game은 1.216m다(SIEGE-20: Lv1 묻은 Battlements_01의 돌 꼭대기, Lv2 약 1.4배 Battlements_03의 받침).
        /// </summary>
        public float ParapetHeight => parapetHeight;
        /// <summary>난간(Lv1)이 낮은 돌이면 그 빈틈 받침 높이, 아니면 0(이어진 받침). 난간 높이보다 낮게 묶는다.</summary>
        public float ParapetGapSill => Mathf.Min(parapetGapSill, parapetHeight);
        /// <summary>성가퀴(Lv2) 덩어리를 칸 가장자리에서 바깥으로 내미는 거리(m).</summary>
        public float CrenellatedOutset => crenellatedOutset;
        /// <summary>성가퀴의 솟은 부분 높이.</summary>
        public float MerlonHeight => Mathf.Max(merlonHeight, parapetHeight);
        public float MerlonWidth => merlonWidth;
        /// <summary>솟은 부분 사이 빈틈 폭. 사다리(폭 0.7m)가 들어가도록 그보다 넓게 둔다.</summary>
        public float CrenelWidth => crenelWidth;
        /// <summary>면 가운데가 솟은 부분인지(킷 무늬). 아니면 면 가운데가 빈틈이다.</summary>
        public bool MerlonAtMiddle => merlonAtMiddle;
        /// <summary>비어 있으면 받치는 성벽의 재질을 쓴다.</summary>
        public Material Material => material;
        /// <summary>텍스처 반복 길이(m). 0이면 반복하지 않는다(기본 상자).</summary>
        public float TextureTileMeters => textureTileMeters;

        public static BattlementSettings Create(float thickness = 0.4f, float parapetHeight = 1.1f, float merlonHeight = 2.2f,
            float merlonWidth = 0.75f, float crenelWidth = 0.75f, Material material = null, float textureTileMeters = 0f,
            bool merlonAtMiddle = false, float parapetGapSill = 0f, float crenellatedOutset = 0f)
        {
            var value = CreateInstance<BattlementSettings>();
            value.merlonAtMiddle = merlonAtMiddle;
            value.parapetGapSill = Mathf.Max(0f, parapetGapSill);
            value.crenellatedOutset = Mathf.Max(0f, crenellatedOutset);
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
