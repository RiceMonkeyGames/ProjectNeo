using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public sealed class BuildGridView : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] Material material;
        [SerializeField] float lineWidth = 0.09f;
        Mesh mesh;
        public void Configure(ConstructionWorld owner, Material lineMaterial) { world = owner; material = lineMaterial; }
        void Start()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float width = world.Grid.Width * world.Grid.CellSize;
            float depth = world.Grid.Depth * world.Grid.CellSize;
            for (int x = 0; x <= world.Grid.Width; x++)
                Quad(vertices, triangles, x * world.Grid.CellSize - lineWidth * 0.5f, 0f, lineWidth, depth);
            for (int z = 0; z <= world.Grid.Depth; z++)
                Quad(vertices, triangles, 0f, z * world.Grid.CellSize - lineWidth * 0.5f, width, lineWidth);
            mesh = new Mesh { name = "ConstructionGrid" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        static void Quad(List<Vector3> vertices, List<int> triangles, float x, float z, float width, float depth)
        {
            int index = vertices.Count;
            vertices.Add(new Vector3(x, 0.02f, z)); vertices.Add(new Vector3(x, 0.02f, z + depth));
            vertices.Add(new Vector3(x + width, 0.02f, z + depth)); vertices.Add(new Vector3(x + width, 0.02f, z));
            triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
