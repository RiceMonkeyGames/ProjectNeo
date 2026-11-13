using System;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>ART-01: unit stair ramp with per-face vertices so each face is flat-shaded like wall boxes.</summary>
    public static class ConstructionRampMesh
    {
        public const string AssetPath = "Assets/_Project/Art/Placeholder/ConstructionRamp.asset";

        static readonly Vector3 FrontLeft = new Vector3(-0.5f, 0f, -0.5f), FrontRight = new Vector3(0.5f, 0f, -0.5f);
        static readonly Vector3 BackLeft = new Vector3(-0.5f, 0f, 0.5f), BackRight = new Vector3(0.5f, 0f, 0.5f);
        static readonly Vector3 TopLeft = new Vector3(-0.5f, 1f, 0.5f), TopRight = new Vector3(0.5f, 1f, 0.5f);

        /// <summary>Overwrites mesh data in place (keeps asset GUID and prefab/collider references).</summary>
        public static void Fill(Mesh mesh)
        {
            // Faces keep the original winding: slope, left, right, back, bottom.
            Vector3[][] faces =
            {
                new[] { FrontLeft, TopLeft, TopRight, FrontRight },
                new[] { FrontLeft, BackLeft, TopLeft },
                new[] { FrontRight, TopRight, BackRight },
                new[] { BackLeft, BackRight, TopRight, TopLeft },
                new[] { FrontLeft, FrontRight, BackRight, BackLeft },
            };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            foreach (Vector3[] face in faces)
            {
                int start = vertices.Count;
                vertices.AddRange(face);
                for (int i = 1; i < face.Length - 1; i++) triangles.AddRange(new[] { start, start + i, start + i + 1 });
            }
            mesh.Clear();
            mesh.name = "UnitRamp";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        [MenuItem("ProjectNeo/Setup/Flatten Stair Shading")]
        public static void FlattenFromMenu() => Debug.Log(Flatten());

        /// <summary>Rebuilds the existing ramp asset with flat faces. Shape and collider geometry are unchanged.</summary>
        public static string Flatten()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
            if (mesh == null) throw new InvalidOperationException("Ramp mesh not found: " + AssetPath);
            Fill(mesh);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return $"Flattened ramp: vertices={mesh.vertexCount}, triangles={mesh.triangles.Length / 3}.";
        }
    }
}
