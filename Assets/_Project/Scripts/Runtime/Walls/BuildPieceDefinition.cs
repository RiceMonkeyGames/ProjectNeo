using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 값은 저장·에셋에 남으므로 바꾸지 않는다. 보루(SIEGE-11)는 성벽 밖으로 튀어나온 수성 무기 자리다.
    /// 망루(SIEGE-14, 2×2)는 보루보다 비싸고 강한 바깥 조각으로, 지붕에 선 아군 원거리 유닛이 강해진다.
    /// </summary>
    public enum BuildPieceKind { Wall = 0, Stairs = 1, Gate = 2, Bastion = 3, Tower = 4 }

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
        [Tooltip("맨 위 윗면에 선 아군 원거리 공격자에게 붙는 스탯 수정값(망루). 내려오면 뗀다. 비우면 효과 없음.")]
        [SerializeField] List<ProjectNeo.Stats.StatModifier> occupantModifiers = new List<ProjectNeo.Stats.StatModifier>();
        public BuildPieceKind Kind => kind;
        public string DisplayName => displayName;
        public Vector2Int Footprint => footprint;
        public float Height => height;
        public float MaxHealth => maxHealth;
        // 프리팹은 가로·높이·세로 1, 바닥 y=0을 기준으로 만든다. 외형과 충돌은 서로 분리한다.
        public GameObject Prefab => prefab;
        // 바닥 칸과 별개의 설치 지점. 향후 수성 무기는 지지 조각 ID + 지점 ID로 연결한다.
        public IReadOnlyList<BuildMountPoint> MountPoints => mountPoints;
        public IReadOnlyList<ProjectNeo.Stats.StatModifier> OccupantModifiers => occupantModifiers;

        /// <summary>윗면에 선 아군 원거리 공격자의 수정값을 정한다(에디터 설정·시험용).</summary>
        public void SetOccupantModifiers(params ProjectNeo.Stats.StatModifier[] modifiers) =>
            occupantModifiers = new List<ProjectNeo.Stats.StatModifier>(modifiers);

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
