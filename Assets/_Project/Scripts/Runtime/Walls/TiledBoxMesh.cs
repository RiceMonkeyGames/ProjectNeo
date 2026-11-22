using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 단위 상자(−0.5~0.5) 메쉬이되, 늘린 뒤의 실제 치수(m)에 맞춰 텍스처가 <c>tileMeters</c>마다 한 번 반복되게 UV를 잡는다(ART-06).
    /// UV는 상자 가운데의 위치(<c>origin</c>, 성 좌표 m)를 더한 좌표로 잡는다. 그래서 같은 평면에 겹친 두 조각(난간과 솟은 부분,
    /// 모서리에서 만나는 두 면)이 같은 무늬를 그려, 깊이가 겹쳐도 깜빡임이 보이지 않는다. 외형 전용이며 충돌과는 관계없다.
    /// </summary>
    public static class TiledBoxMesh
    {
        // 면 방향(바깥쪽), 면 위 가로·세로 축.
        static readonly (Vector3 normal, Vector3 u, Vector3 v)[] Faces =
        {
            (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
            (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
            (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.back),
        };

        /// <summary>새 메쉬를 만든다. 꼭짓점은 단위 상자, UV는 (origin + 실제 위치) ÷ 반복 길이. 쓰는 쪽이 메쉬를 지운다.</summary>
        public static Mesh Build(Vector3 size, float tileMeters, Vector3 origin = default)
        {
            float tile = Mathf.Max(0.01f, tileMeters);
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            foreach ((Vector3 normal, Vector3 u, Vector3 v) in Faces)
            {
                int start = vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    float a = i == 1 || i == 2 ? 0.5f : -0.5f;
                    float b = i >= 2 ? 0.5f : -0.5f;
                    Vector3 corner = normal * 0.5f + u * a + v * b;
                    vertices.Add(corner);
                    normals.Add(normal);
                    // 면 위 위치를 성 좌표(m)로 바꿔 반복 길이로 나눈다.
                    Vector3 position = origin + Vector3.Scale(corner, size);
                    uvs.Add(new Vector2(Vector3.Dot(position, u), Vector3.Dot(position, v)) / tile);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = "TiledBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
