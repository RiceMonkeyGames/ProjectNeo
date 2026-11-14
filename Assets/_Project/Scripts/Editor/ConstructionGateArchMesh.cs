using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>SIEGE-06: unit stone gate arch mesh with per-face vertices for flat shading.</summary>
    public static class ConstructionGateArchMesh
    {
        public const string AssetPath = "Assets/_Project/Art/Placeholder/ConstructionGateArch.asset";
        public const float HalfOpeningWidth = 0.35f;
        public const float SpringY = 0.52f;
        public const float ArchTopY = 0.82f;
        const int ArchSegments = 8;

        public static void Fill(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Left and right stone pillars.
            AddBox(vertices, triangles, -0.5f, -HalfOpeningWidth, 0f, 1f, -0.5f, 0.5f);
            AddBox(vertices, triangles, HalfOpeningWidth, 0.5f, 0f, 1f, -0.5f, 0.5f);

            // Curved arch lintel spanning [-HalfOpeningWidth, +HalfOpeningWidth].
            for (int i = 0; i < ArchSegments; i++)
            {
                float a0 = Mathf.PI * (1f - (float)i / ArchSegments);
                float a1 = Mathf.PI * (1f - (float)(i + 1) / ArchSegments);
                float x0 = Mathf.Cos(a0) * HalfOpeningWidth;
                float y0 = SpringY + Mathf.Sin(a0) * (ArchTopY - SpringY);
                float x1 = Mathf.Cos(a1) * HalfOpeningWidth;
                float y1 = SpringY + Mathf.Sin(a1) * (ArchTopY - SpringY);

                // Front (-Z)
                AddQuad(vertices, triangles,
                    new Vector3(x0, y0, -0.5f), new Vector3(x0, 1f, -0.5f),
                    new Vector3(x1, 1f, -0.5f), new Vector3(x1, y1, -0.5f));
                // Back (+Z)
                AddQuad(vertices, triangles,
                    new Vector3(x1, y1, 0.5f), new Vector3(x1, 1f, 0.5f),
                    new Vector3(x0, 1f, 0.5f), new Vector3(x0, y0, 0.5f));
                // Top (+Y)
                AddQuad(vertices, triangles,
                    new Vector3(x0, 1f, -0.5f), new Vector3(x0, 1f, 0.5f),
                    new Vector3(x1, 1f, 0.5f), new Vector3(x1, 1f, -0.5f));
                // Arch soffit (inner underside)
                AddQuad(vertices, triangles,
                    new Vector3(x0, y0, 0.5f), new Vector3(x0, y0, -0.5f),
                    new Vector3(x1, y1, -0.5f), new Vector3(x1, y1, 0.5f));
            }

            mesh.Clear();
            mesh.name = "UnitGateArch";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void AddBox(List<Vector3> vertices, List<int> triangles,
            float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            // Front (-Z)
            AddQuad(vertices, triangles,
                new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ),
                new Vector3(maxX, maxY, minZ), new Vector3(maxX, minY, minZ));
            // Back (+Z)
            AddQuad(vertices, triangles,
                new Vector3(maxX, minY, maxZ), new Vector3(maxX, maxY, maxZ),
                new Vector3(minX, maxY, maxZ), new Vector3(minX, minY, maxZ));
            // Left (-X)
            AddQuad(vertices, triangles,
                new Vector3(minX, minY, maxZ), new Vector3(minX, maxY, maxZ),
                new Vector3(minX, maxY, minZ), new Vector3(minX, minY, minZ));
            // Right (+X)
            AddQuad(vertices, triangles,
                new Vector3(maxX, minY, minZ), new Vector3(maxX, maxY, minZ),
                new Vector3(maxX, maxY, maxZ), new Vector3(maxX, minY, maxZ));
            // Top (+Y)
            AddQuad(vertices, triangles,
                new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ),
                new Vector3(maxX, maxY, maxZ), new Vector3(maxX, maxY, minZ));
            // Bottom (-Y)
            AddQuad(vertices, triangles,
                new Vector3(minX, minY, maxZ), new Vector3(minX, minY, minZ),
                new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ));
        }

        static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        public static Mesh EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
            if (existing != null)
            {
                Fill(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var mesh = new Mesh();
            Fill(mesh);
            AssetDatabase.CreateAsset(mesh, AssetPath);
            return mesh;
        }
    }
}
