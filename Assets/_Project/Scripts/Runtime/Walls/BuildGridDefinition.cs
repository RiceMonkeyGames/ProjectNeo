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
        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;
        public int MaxLevels => maxLevels;
        public bool ForbidValleys => forbidValleys;
        public bool GroundStairsOnly => groundStairsOnly;

        public static BuildGridDefinition Create(int width = 16, int depth = 14, float cellSize = 3f, int maxLevels = 1,
            bool forbidValleys = false, bool groundStairsOnly = false)
        {
            var value = CreateInstance<BuildGridDefinition>();
            value.width = Mathf.Max(1, width);
            value.depth = Mathf.Max(1, depth);
            value.cellSize = Mathf.Max(0.1f, cellSize);
            value.maxLevels = Mathf.Max(1, maxLevels);
            value.forbidValleys = forbidValleys;
            value.groundStairsOnly = groundStairsOnly;
            return value;
        }
    }
}
