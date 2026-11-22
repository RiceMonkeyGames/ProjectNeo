using NUnit.Framework;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>ART-06 성가퀴 돌 텍스처: 늘린 치수(m)에 맞춰 UV가 반복 길이마다 1씩 늘어난다.</summary>
    public class TiledBoxMeshTests
    {
        [Test]
        public void FaceUvSpan_MatchesSizeOverTile()
        {
            var size = new Vector3(3f, 1.1f, 0.4f);
            Mesh mesh = TiledBoxMesh.Build(size, 3f);
            try
            {
                Assert.AreEqual(24, mesh.vertexCount, "면마다 꼭짓점 4개(평면 음영).");
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                // 앞면(+Z): 가로 3m/3m = 1, 세로 1.1m/3m.
                Vector2 span = Span(vertices, normals, uvs, Vector3.forward);
                Assert.AreEqual(1f, span.x, 1e-4f);
                Assert.AreEqual(1.1f / 3f, span.y, 1e-4f);
                // 옆면(+X): 가로는 두께 0.4m.
                span = Span(vertices, normals, uvs, Vector3.right);
                Assert.AreEqual(0.4f / 3f, span.x, 1e-4f);
                foreach (Vector3 vertex in vertices)
                    Assert.LessOrEqual(Mathf.Max(Mathf.Abs(vertex.x), Mathf.Abs(vertex.y), Mathf.Abs(vertex.z)), 0.5f + 1e-5f, "꼭짓점은 단위 상자.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CoplanarBoxes_ShareUvAtSamePoint()
        {
            // 난간(폭 3m, 높이 1.1m)과 그 위로 솟은 부분(폭 0.75m, 높이 2.2m)은 앞면이 같은 평면에서 겹친다.
            var parapetSize = new Vector3(3f, 1.1f, 0.4f);
            var parapetOrigin = new Vector3(1.5f, 3.55f, 0.2f);
            var merlonSize = new Vector3(0.75f, 2.2f, 0.4f);
            var merlonOrigin = new Vector3(0.75f, 4.1f, 0.2f);
            Mesh parapet = TiledBoxMesh.Build(parapetSize, 3f, parapetOrigin);
            Mesh merlon = TiledBoxMesh.Build(merlonSize, 3f, merlonOrigin);
            try
            {
                // 솟은 부분 앞면 왼쪽 아래 모서리(성 좌표 x 0.375, y 3, z 0.4)는 난간 앞면 위의 점이기도 하다.
                var point = new Vector3(0.375f, 3f, 0.4f);
                Vector2 fromMerlon = UvAt(merlon, merlonSize, merlonOrigin, point, Vector3.forward);
                Vector2 expected = new Vector2(-point.x, point.y) / 3f;
                Assert.AreEqual(expected.x, fromMerlon.x, 1e-4f);
                Assert.AreEqual(expected.y, fromMerlon.y, 1e-4f);
                // 난간 앞면도 같은 규칙(성 좌표 ÷ 반복 길이)이라 그 점에서 같은 값을 갖는다.
                Vector2 parapetCorner = UvAt(parapet, parapetSize, parapetOrigin, new Vector3(0f, 3f, 0.4f), Vector3.forward);
                Assert.AreEqual(0f, parapetCorner.x, 1e-4f);
                Assert.AreEqual(1f, parapetCorner.y, 1e-4f);
            }
            finally { Object.DestroyImmediate(parapet); Object.DestroyImmediate(merlon); }
        }

        /// <summary>그 면의 꼭짓점 가운데 성 좌표가 <paramref name="point"/>인 것의 UV.</summary>
        static Vector2 UvAt(Mesh mesh, Vector3 size, Vector3 origin, Vector3 point, Vector3 normal)
        {
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
                if (Vector3.Dot(normals[i], normal) > 0.99f && (origin + Vector3.Scale(vertices[i], size) - point).sqrMagnitude < 1e-6f)
                    return uvs[i];
            Assert.Fail("No vertex at " + point);
            return default;
        }

        static Vector2 Span(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Vector3 normal)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Vector3.Dot(normals[i], normal) < 0.99f) continue;
                min = Vector2.Min(min, uvs[i]); max = Vector2.Max(max, uvs[i]);
            }
            return new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }
    }
}
