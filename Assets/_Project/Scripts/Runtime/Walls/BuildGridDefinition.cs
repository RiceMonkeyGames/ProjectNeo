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
        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;
        public int MaxLevels => maxLevels;

        public static BuildGridDefinition Create(int width = 16, int depth = 14, float cellSize = 3f, int maxLevels = 1)
        {
            var value = CreateInstance<BuildGridDefinition>();
            value.width = Mathf.Max(1, width);
            value.depth = Mathf.Max(1, depth);
            value.cellSize = Mathf.Max(0.1f, cellSize);
            value.maxLevels = Mathf.Max(1, maxLevels);
            return value;
        }
    }
}
