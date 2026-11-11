using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public enum BuildPieceKind { Wall = 0, Stairs = 1, Gate = 2 }

    [Serializable]
    public struct BuildMountPoint
    {
        public string id;
        public Vector3 normalizedPosition;
        public Vector3 localEulerAngles;
        public BuildMountPoint(string id, Vector3 position, Vector3 eulerAngles = default)
        { this.id = id; normalizedPosition = position; localEulerAngles = eulerAngles; }
    }

    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Piece")]
    public sealed class BuildPieceDefinition : ScriptableObject
    {
        [SerializeField] BuildPieceKind kind;
        [SerializeField] string displayName;
        [SerializeField] Vector2Int footprint = Vector2Int.one;
        [SerializeField, Min(0.1f)] float height = 3f;
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [SerializeField] GameObject prefab;
        [SerializeField] List<BuildMountPoint> mountPoints = new List<BuildMountPoint>();
        public BuildPieceKind Kind => kind;
        public string DisplayName => displayName;
        public Vector2Int Footprint => footprint;
        public float Height => height;
        public float MaxHealth => maxHealth;
        // 프리팹은 가로·높이·세로 1, 바닥 y=0을 기준으로 만든다. 외형과 충돌은 서로 분리한다.
        public GameObject Prefab => prefab;
        // 바닥 칸과 별개의 설치 지점. 향후 수성 무기는 지지 조각 ID + 지점 ID로 연결한다.
        public IReadOnlyList<BuildMountPoint> MountPoints => mountPoints;

        public static BuildPieceDefinition Create(BuildPieceKind kind, string name, Vector2Int footprint,
            float height = 3f, float maxHealth = 100f, GameObject prefab = null, params BuildMountPoint[] mountPoints)
        {
            var value = CreateInstance<BuildPieceDefinition>();
            value.kind = kind;
            value.displayName = name;
            value.footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            value.height = Mathf.Max(0.1f, height);
            value.maxHealth = Mathf.Max(1f, maxHealth);
            value.prefab = prefab;
            value.mountPoints = new List<BuildMountPoint>(mountPoints);
            return value;
        }
    }
}
