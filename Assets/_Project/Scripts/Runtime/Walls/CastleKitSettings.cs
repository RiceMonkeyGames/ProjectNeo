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
        const int PieceCount = 34;

        [SerializeField] GameObject[] prefabs = new GameObject[PieceCount];
        // 킷 한 칸(벽판 폭·층 높이). 조각 높이·두께와 모듈 기준 m 값은 늘 이 크기 그대로다(층 높이 = 모듈).
        // 칸이 이보다 넓으면 면 방향으로만 늘리거나 반 모듈 조각으로 짠다(SIEGE-35, CastleKitLook).
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
        // 망루 지붕(데모 탑과 같은 값, 모듈 기준 m): 받침·흉벽을 면에서 바깥으로 내민 거리, 모서리 흉벽 기둥의 대각 내밈과 배율.
        [SerializeField, Min(0f)] float towerOverhang = 0.5f;
        [SerializeField, Min(0f)] float towerCapOffset = 0.3f;
        [SerializeField, Min(0.1f)] float towerCapScale = 1.2f;
        public float TowerOverhang => towerOverhang;
        public float TowerCapOffset => towerCapOffset;
        public float TowerCapScale => towerCapScale;

        // 3층 성벽 계단 방(SIEGE-27, 모듈 기준 m): 면에서 안쪽 벽면까지(Wall_01 두께 절반), 층참 깊이, 바닥·층참 판 두께, 경사 판 두께.
        // 층참 밑으로 걸어 들어오므로(맨 아래 방 층의 맞은편 문) 층참 아래 높이 = 반 층 − 판 두께가 유닛 키(2m)보다 커야 한다.
        [SerializeField, Min(0f)] float roomWallInset = 0.31f;
        [SerializeField, Min(0.5f)] float roomLandingDepth = 1f;
        [SerializeField, Min(0.02f)] float roomSlabThickness = 0.2f;
        [SerializeField, Min(0.02f)] float roomRampThickness = 0.15f;
        // 천장 구멍을 둘째 계단 발치에서 층참 쪽으로 더 넓히는 거리. 층참에서 둘째 계단을 오를 때 플레이어 머리(반경 0.4m 몸통)가
        // 천장 끝에 걸리지 않게 한다(0이면 반 층 오른 곳에서 걸린다).
        [SerializeField, Min(0f)] float roomHoleOverhang = 0.6f;
        public float RoomHoleOverhang => roomHoleOverhang;
        // 계단 한 줄의 폭(사용자 요청 2026-11-23: 맞붙던 2.19m의 약 2/3). 두 줄 사이가 발치에서 층참 밑까지 바닥 통로로 남는다.
        [SerializeField, Min(0.8f)] float roomFlightWidth = 1.46f;
        public float RoomFlightWidth => roomFlightWidth;
        // 계단 모델(Stairs_Wood_Straight_01) 기둥 두 개의 자리(모델 로컬 x, z). 둘째 계단 아래 나무 기둥을 여기에 맞춘다.
        [SerializeField] Vector2 stairPostNear = new Vector2(0.05f, 0.06f);
        [SerializeField] Vector2 stairPostFar = new Vector2(0.07f, -1.97f);
        // 나무 기둥 모델(SM_Bld_House_WoodBeam_01) 높이(모듈 기준 m).
        [SerializeField, Min(0.1f)] float roomPostHeight = 2.5f;
        public Vector2 StairPostNear => stairPostNear;
        public Vector2 StairPostFar => stairPostFar;
        public float RoomPostHeight => roomPostHeight;
        // 나무 기둥 굵기 ÷ 계단 모델 기둥 굵기(0.32m ÷ 약 0.3m). 계단 배율을 곱해 두 기둥이 같은 굵기로 이어진다.
        [SerializeField, Min(0.1f)] float roomPostThickness = 0.95f;
        public float RoomPostThickness => roomPostThickness;
        public float RoomWallInset => roomWallInset;
        public float RoomLandingDepth => roomLandingDepth;
        public float RoomSlabThickness => roomSlabThickness;
        public float RoomRampThickness => roomRampThickness;

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
