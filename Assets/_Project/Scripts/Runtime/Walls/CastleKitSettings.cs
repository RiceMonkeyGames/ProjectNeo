using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성 킷 조각 프리팹(SIEGE-14 시험). 배열 순서는 <see cref="CastleKitPiece"/> 값과 같다.
    /// 프리팹은 PolygonFantasyKingdom 성 조각의 충돌체 없는 복사본이며 모듈 크기(<see cref="ModuleSize"/>, 5m) 기준으로 만들어져 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Castle Kit Settings")]
    public sealed class CastleKitSettings : ScriptableObject
    {
        const int PieceCount = 21;

        [SerializeField] GameObject[] prefabs = new GameObject[PieceCount];
        // 킷 한 칸(벽판 폭·층 높이). 칸 크기가 이와 다르면 조각을 고르게 늘리거나 줄인다.
        [SerializeField, Min(0.1f)] float moduleSize = 5f;
        // 바깥 면에서 장식 벽판(PanelAccent)을 넣는 간격(칸). 격자 번호 기준이라 성벽을 늘려도 무늬가 그대로다.
        [SerializeField, Min(1)] int accentSpacingCells = 2;
        // 성벽 위 계단 폭(모듈 기준 m). 계단 모델(Stairs_Wood_Straight_01)의 폭이다.
        [SerializeField, Min(0.1f)] float stairWidth = 2.16f;
        // 안쪽 면 돋을새김 띠의 기준점이 맨 윗층 꼭대기에서 내려간 거리(모듈 기준 m). 데모 성벽과 같은 값이다.
        [SerializeField, Min(0f)] float innerReliefDrop = 2.75f;

        public float ModuleSize => moduleSize;
        public int AccentSpacingCells => Mathf.Max(1, accentSpacingCells);
        public float StairWidth => stairWidth;
        public float InnerReliefDrop => innerReliefDrop;
        // 꺾임 계단은 원래 아래 칸 약 48.7°, 위 칸 약 45°라 유닛이 오를 수 있는 경사(46°)를 넘는 곳이 있다. 수평(로컬 x·z)만 늘려
        // 약 45.5°·41.8°로 맞춘다. x 0~4.08 × 1.12 = 4.57m는 칸 안에, z 끝(5.13 × 1.12 = 5.75m)은 높은 칸 바닥에 약 0.6m 걸친다.
        public float CornerStairStretch => cornerStairStretch;
        // 무너진 칸 V자 반쪽(부서진 벽판·단면)의 가로 비율. 1이면 반쪽이 칸 절반(2.5m)을 다 채워 바닥 틈이 거의 없다.
        // 0.7이면 반쪽이 1.75m라 가운데에 약 1.5m 바닥 통로가 남아 유닛이 잔해에 파묻혀 보이지 않는다.
        public float BreachHalfWidth => breachHalfWidth;
        [SerializeField, Range(0.3f, 1f)] float breachHalfWidth = 0.7f;
        [SerializeField, Min(1f)] float cornerStairStretch = 1.12f;

        public GameObject Prefab(CastleKitPiece piece)
        {
            int index = (int)piece;
            return prefabs != null && index >= 0 && index < prefabs.Length ? prefabs[index] : null;
        }

        public static CastleKitSettings Create(float moduleSize = 5f, int accentSpacingCells = 2)
        {
            var value = CreateInstance<CastleKitSettings>();
            value.moduleSize = Mathf.Max(0.1f, moduleSize);
            value.accentSpacingCells = Mathf.Max(1, accentSpacingCells);
            return value;
        }

        /// <summary>에디터 설정 메뉴가 조각 프리팹을 채운다.</summary>
        public void SetPrefab(CastleKitPiece piece, GameObject prefab)
        {
            if (prefabs == null || prefabs.Length < PieceCount) System.Array.Resize(ref prefabs, PieceCount);
            prefabs[(int)piece] = prefab;
        }
    }
}
