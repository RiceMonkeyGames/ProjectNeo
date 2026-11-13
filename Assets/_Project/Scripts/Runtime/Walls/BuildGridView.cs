using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectNeo.Walls
{
    public sealed class BuildGridView : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] Material material;
        [SerializeField] float lineWidth = 0.09f;
        Mesh mesh;
        Vector3[] vertices;
        float currentXWidth;
        float currentZWidth;
        public void Configure(ConstructionWorld owner, Material lineMaterial) { world = owner; material = lineMaterial; }
        void OnEnable() => RenderPipelineManager.beginCameraRendering += PrepareForCamera;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= PrepareForCamera;
        void Start()
        {
            int lineCount = world.Grid.Width + world.Grid.Depth + 2;
            vertices = new Vector3[lineCount * 4];
            var triangles = new int[lineCount * 6];
            for (int line = 0; line < lineCount; line++)
            {
                int vertex = line * 4, triangle = line * 6;
                triangles[triangle] = vertex; triangles[triangle + 1] = vertex + 1; triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex; triangles[triangle + 4] = vertex + 2; triangles[triangle + 5] = vertex + 3;
            }
            mesh = new Mesh { name = "ConstructionGrid" };
            mesh.MarkDynamic();
            UpdateWidths(lineWidth, lineWidth);
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        void PrepareForCamera(ScriptableRenderContext context, Camera camera)
        {
            if (mesh == null || camera.cameraType != CameraType.Game) return;
            float xWidth = lineWidth, zWidth = lineWidth;
            if (camera.orthographic && camera.TryGetComponent(out ConstructionCameraController controller))
            {
                // 비스듬한 시점에서는 선에 수직인 화면 방향의 폭을 계산한다.
                Vector3 origin = transform.position;
                Vector2 screenOrigin = camera.WorldToScreenPoint(origin);
                Vector2 screenX = (Vector2)camera.WorldToScreenPoint(transform.TransformPoint(Vector3.right)) - screenOrigin;
                Vector2 screenZ = (Vector2)camera.WorldToScreenPoint(transform.TransformPoint(Vector3.forward)) - screenOrigin;
                float area = Mathf.Abs(screenX.x * screenZ.y - screenX.y * screenZ.x);
                float xPixelsPerUnit = area / Mathf.Max(screenZ.magnitude, 0.0001f);
                float zPixelsPerUnit = area / Mathf.Max(screenX.magnitude, 0.0001f);
                xWidth = Mathf.Max(lineWidth, controller.MinimumGridLinePixels / Mathf.Max(xPixelsPerUnit, 0.0001f));
                zWidth = Mathf.Max(lineWidth, controller.MinimumGridLinePixels / Mathf.Max(zPixelsPerUnit, 0.0001f));
            }
            UpdateWidths(xWidth, zWidth);
        }
        void UpdateWidths(float xWidth, float zWidth)
        {
            if (Mathf.Approximately(currentXWidth, xWidth) && Mathf.Approximately(currentZWidth, zWidth)) return;
            currentXWidth = xWidth; currentZWidth = zWidth;
            float width = world.Grid.Width * world.Grid.CellSize;
            float depth = world.Grid.Depth * world.Grid.CellSize;
            int index = 0;
            for (int x = 0; x <= world.Grid.Width; x++)
                Quad(index++, x * world.Grid.CellSize - xWidth * 0.5f, 0f, xWidth, depth);
            for (int z = 0; z <= world.Grid.Depth; z++)
                Quad(index++, 0f, z * world.Grid.CellSize - zWidth * 0.5f, width, zWidth);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
        void Quad(int line, float x, float z, float width, float depth)
        {
            int index = line * 4;
            vertices[index] = new Vector3(x, 0.02f, z); vertices[index + 1] = new Vector3(x, 0.02f, z + depth);
            vertices[index + 2] = new Vector3(x + width, 0.02f, z + depth); vertices[index + 3] = new Vector3(x + width, 0.02f, z);
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
