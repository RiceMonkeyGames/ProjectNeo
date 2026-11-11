using UnityEngine;

namespace ProjectNeo.Walls
{
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Grid")]
    public sealed class BuildGridDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] int width = 16;
        [SerializeField, Min(1)] int depth = 14;
        [SerializeField, Min(0.1f)] float cellSize = 3f;
        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;

        public static BuildGridDefinition Create(int width = 16, int depth = 14, float cellSize = 3f)
        {
            var value = CreateInstance<BuildGridDefinition>();
            value.width = Mathf.Max(1, width);
            value.depth = Mathf.Max(1, depth);
            value.cellSize = Mathf.Max(0.1f, cellSize);
            return value;
        }
    }
}
