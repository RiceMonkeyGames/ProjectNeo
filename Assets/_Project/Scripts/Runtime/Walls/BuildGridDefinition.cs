using UnityEngine;

namespace ProjectNeo.Walls
{
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Grid")]
    public sealed class BuildGridDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] int width = 16;
        [SerializeField, Min(1)] int depth = 14;
        [SerializeField, Min(0.1f)] float cellSize = 3f;
        // 한 칸에 쌓을 수 있는 조각 수. 1이면 쌓기를 쓰지 않는다.
        [SerializeField, Min(1)] int maxLevels = 1;
        // 켜면 성벽 맨 위가 앞뒤(같은 축 양쪽) 이웃보다 모두 낮은 칸(2층-1층-2층 골짜기)을 만들지 않는다(SIEGE-14 시험, 사용자 결정 2026-11-22).
        // 골짜기가 될 칸에 지으면 양옆 높이까지 한 번에 쌓고, 무너질 때는 위층까지 함께 무너진다.
        [SerializeField] bool forbidValleys;
        // 켜면 플레이어가 짓는 계단은 땅 위에만 놓는다(SIEGE-27, 사용자 결정 2026-11-23, 5m 격자).
        // 성벽 사이 오르내림은 자동 바깥 계단(1층↔2층)과 3층 성벽의 계단 방이 맡는다.
        [SerializeField] bool groundStairsOnly;
        // 켜면 성은 격자 남쪽(-z) 가장자리 너머에 있다(SIEGE-40 분지: 북쪽 성벽 한 줄, 사용자 결정 2026-11-28).
        // 격자 남쪽 밖은 성 안이고, 성 바깥은 북·동·서 가장자리에서만 번진다. 끄면 닫힌 성벽 고리 규칙(격자 밖은 모두 바깥)이다.
        [SerializeField] bool castleBehindSouthEdge;
        // 동·서 끝 이 칸 수만큼은 짓지 못한다(분지 성벽 양 끝 원형 탑 자리, SIEGE-40).
        [SerializeField, Min(0)] int blockedSideCells;
        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;
        public int MaxLevels => maxLevels;
        public bool ForbidValleys => forbidValleys;
        public bool GroundStairsOnly => groundStairsOnly;
        public bool CastleBehindSouthEdge => castleBehindSouthEdge;
        public int BlockedSideCells => blockedSideCells;

        /// <summary>그 칸이 격자 안이고 막힌 동·서 끝 칸이 아닌지(지을 수 있는 칸).</summary>
        public bool IsBuildable(Vector2Int cell) =>
            cell.x >= blockedSideCells && cell.x < width - blockedSideCells && cell.y >= 0 && cell.y < depth;

        public static BuildGridDefinition Create(int width = 16, int depth = 14, float cellSize = 3f, int maxLevels = 1,
            bool forbidValleys = false, bool groundStairsOnly = false, bool castleBehindSouthEdge = false, int blockedSideCells = 0)
        {
            var value = CreateInstance<BuildGridDefinition>();
            value.width = Mathf.Max(1, width);
            value.depth = Mathf.Max(1, depth);
            value.cellSize = Mathf.Max(0.1f, cellSize);
            value.maxLevels = Mathf.Max(1, maxLevels);
            value.forbidValleys = forbidValleys;
            value.groundStairsOnly = groundStairsOnly;
            value.castleBehindSouthEdge = castleBehindSouthEdge;
            value.blockedSideCells = Mathf.Max(0, blockedSideCells);
            return value;
        }
    }
}
