using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성벽 맨 위 칸에 성가퀴를 자동으로 세운다(SIEGE-10). 조각을 쌓거나 빼거나 무너질 때마다 다시 맞춘다.
    /// 성가퀴는 받치는 조각의 자식이라 그 조각과 함께 움직이고 사라진다(따로 부서지지 않는다).
    /// 충돌체는 아군 사격을 통과시키는 <see cref="ShotCover"/>이며, 길찾기 메시에는 벽처럼 반영된다.
    /// 성 바깥 판정은 준비 중 직접 바꾼 배치(<see cref="ConstructionWorld.LayoutRevision"/>)가 바뀔 때만 다시 계산한다.
    /// </summary>
    public sealed class CastleBattlements : MonoBehaviour
    {
        const string RootName = "Battlement";

        [SerializeField] ConstructionWorld world;
        [SerializeField] BattlementSettings settings;
        [SerializeField] BattlementStyle style = BattlementStyle.Parapet;

        sealed class Built
        {
            public GameObject Root;
            public int Signature;
            public bool UnderConstruction;
        }

        readonly Dictionary<PlacedStructure, Built> built = new Dictionary<PlacedStructure, Built>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        readonly Dictionary<int, Built> byPlacement = new Dictionary<int, Built>();
        readonly List<BattlementSegment> segments = new List<BattlementSegment>();
        readonly List<PlacedStructure> stale = new List<PlacedStructure>();
        readonly HashSet<PlacedStructure> live = new HashSet<PlacedStructure>();
        static Mesh cube;
        int outsideRevision = -1;
        bool dirty = true;

        public BattlementStyle Style => style;
        public BattlementSettings Settings => settings;
        /// <summary>지금 성가퀴가 서 있는 조각 수(검증용).</summary>
        public int BuiltCount
        {
            get
            {
                int count = 0;
                foreach (Built entry in built.Values) if (entry.Root != null) count++;
                return count;
            }
        }

        public void Configure(ConstructionWorld owner, BattlementSettings battlementSettings, BattlementStyle initialStyle)
        {
            Unsubscribe();
            world = owner; settings = battlementSettings; style = initialStyle;
            Subscribe();
            dirty = true;
        }

        /// <summary>성 전체 모양을 바꾼다. 같으면 아무것도 하지 않는다.</summary>
        public void SetStyle(BattlementStyle value)
        {
            if (style == value) return;
            style = value;
            dirty = true;
            Refresh();
        }

        /// <summary>
        /// 그 칸 맨 위 조각의 그 면에 지금 성가퀴가 서 있으면 받침(난간) 높이, 아니면 0. 사다리 꼭대기가 이 높이에 걸린다.
        /// 사다리는 면 가운데에 걸리고, 면 가운데는 늘 빈틈이라 솟은 부분에 부딪히지 않는다.
        /// </summary>
        public float SillHeightAt(Vector2Int cell, Vector2Int outward)
        {
            if (world == null || world.Layout == null || style == BattlementStyle.None || settings == null) return 0f;
            int face = BattlementRules.DirectionIndex(outward);
            BuildPlacement top = world.Layout.Top(cell);
            if (face < 0 || top == null || !byPlacement.TryGetValue(top.Id, out Built entry) || entry.Root == null) return 0f;
            return (FacesAt(cell) & (1 << face)) != 0 ? settings.ParapetHeight : 0f;
        }

        /// <summary>그 칸의 맨 위 조각에 성가퀴가 서는 면(비트). 검증용.</summary>
        public int FacesAt(Vector2Int cell)
        {
            if (world == null || world.Layout == null) return 0;
            EnsureOutside();
            return BattlementRules.Faces(world.Layout, world.Grid, outside, cell);
        }

        /// <summary>
        /// 그 지점이 성벽 맨 위면 <paramref name="direction"/> 쪽으로 잰 통로 폭(m), 아니면 +∞(COMBAT-08 진형 줄 수 상한).
        /// 방향이 격자 축에서 30도 넘게 비껴 있으면 재지 않는다(+∞).
        /// </summary>
        public float WalkwayWidthAt(Vector3 point, Vector3 direction)
        {
            if (world == null || world.Layout == null) return float.PositiveInfinity;
            Vector3 local = world.LocalPoint(point);
            Vector2Int cell = world.Layout.WorldToCell(local);
            BuildPlacement top = world.Layout.Top(cell);
            if (top == null || Mathf.Abs(local.y - (top.Elevation + top.Definition.Height)) > OnTopTolerance)
                return float.PositiveInfinity;
            Vector3 axis = world.transform.InverseTransformDirection(direction);
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f) return float.PositiveInfinity;
            axis.Normalize();
            if (Mathf.Max(Mathf.Abs(axis.x), Mathf.Abs(axis.z)) < AxisAlignment) return float.PositiveInfinity;
            Vector2Int step = Mathf.Abs(axis.x) >= Mathf.Abs(axis.z)
                ? new Vector2Int(axis.x > 0f ? 1 : -1, 0) : new Vector2Int(0, axis.z > 0f ? 1 : -1);
            EnsureOutside();
            float thickness = style == BattlementStyle.None || settings == null ? 0f : settings.Thickness;
            return BattlementRules.WalkwayWidth(world.Layout, world.Grid, outside, cell, step, thickness);
        }

        const float OnTopTolerance = 0.75f;
        static readonly float AxisAlignment = Mathf.Cos(30f * Mathf.Deg2Rad);

        /// <summary>
        /// 성이 첫 길찾기 메시를 만들기 직전에 부른다(<see cref="ConstructionWorld"/> Awake).
        /// 곧 만들 메시에 담기므로 다시 만들기를 요청하지 않는다.
        /// </summary>
        public void BuildBeforeNavigation(ConstructionWorld owner)
        {
            if (world == null) world = owner;
            if (world != owner) return;
            Refresh(false);
        }

        void OnEnable() { Subscribe(); dirty = true; }
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            if (world != null && isActiveAndEnabled) { world.Changed -= MarkDirty; world.Changed += MarkDirty; }
        }

        void Unsubscribe()
        {
            if (world != null) world.Changed -= MarkDirty;
        }

        // 배치가 바뀐 그 자리에서 바로 맞춘다. 같은 프레임의 길찾기 다시 만들기가 새 성가퀴를 보게 한다.
        void MarkDirty() { dirty = true; Refresh(); }

        void Update()
        {
            if (!dirty)
                foreach (KeyValuePair<PlacedStructure, Built> entry in built)
                    if (entry.Key != null && entry.Key.IsUnderConstruction != entry.Value.UnderConstruction) { dirty = true; break; }
            if (dirty) Refresh();
        }

        void EnsureOutside()
        {
            if (outsideRevision == world.LayoutRevision) return;
            BattlementRules.CollectOutside(world.Layout, world.Grid, outside);
            outsideRevision = world.LayoutRevision;
        }

        /// <summary>모든 조각의 성가퀴를 지금 배치와 모양에 맞춘다. 바뀐 것이 있으면 길찾기 메시를 다시 만들게 한다.</summary>
        public void Refresh() => Refresh(true);

        void Refresh(bool requestNavigation)
        {
            if (world == null || world.Layout == null) return;
            dirty = false;
            EnsureOutside();
            bool changed = false;

            stale.Clear();
            live.Clear();
            foreach (PlacedStructure structure in world.Structures) live.Add(structure);
            foreach (KeyValuePair<PlacedStructure, Built> entry in built)
                if (entry.Key == null || !live.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (PlacedStructure gone in stale)
            {
                if (built.TryGetValue(gone, out Built entry)) changed |= Discard(entry);
                built.Remove(gone);
            }
            byPlacement.Clear();

            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null || structure.Placement == null) continue;
                if (!built.TryGetValue(structure, out Built entry)) built[structure] = entry = new Built();
                byPlacement[structure.Placement.Id] = entry;
                entry.UnderConstruction = structure.IsUnderConstruction;
                int signature = Signature(structure);
                if (signature == entry.Signature && (entry.Root != null) == (signature != 0)) continue;
                changed |= Discard(entry);
                entry.Signature = signature;
                if (signature != 0) { entry.Root = Build(structure); changed = true; }
            }
            if (changed && requestNavigation) world.RequestNavigationRebuild();
        }

        static bool Discard(Built entry)
        {
            if (entry.Root == null) return false;
            // 파괴는 프레임 끝이라, 그 전에 길찾기 수집과 충돌에서 바로 빠지도록 끈다.
            entry.Root.SetActive(false);
            entry.Root.name = RootName + " (제거됨)";
            Destroy(entry.Root);
            entry.Root = null;
            return true;
        }

        // 0이면 세울 것이 없다. 모양·칸별 면이 같으면 같은 값이다.
        int Signature(PlacedStructure structure)
        {
            if (style == BattlementStyle.None || settings == null || structure.IsUnderConstruction) return 0;
            BuildPlacement placement = structure.Placement;
            if (placement.Definition.Kind == BuildPieceKind.Stairs) return 0;
            int hash = 17, any = 0;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    int faces = world.Layout.Top(cell) == placement ? BattlementRules.Faces(world.Layout, world.Grid, outside, cell) : 0;
                    any |= faces;
                    hash = hash * 31 + faces;
                }
            if (any == 0) return 0;
            hash = hash * 31 + (int)StyleFor(placement);
            return hash == 0 ? 1 : hash;
        }

        /// <summary>
        /// 그 조각에 쓸 모양. 수성 무기가 올라간 조각은 성가퀴 대신 받침만 남겨 포구를 만든다(SIEGE-10, 사용자 결정 2026-11-20).
        /// 그래서 포탄이 솟은 부분을 뚫고 나가 보이지 않는다.
        /// </summary>
        BattlementStyle StyleFor(BuildPlacement placement) =>
            style == BattlementStyle.Crenellated && world.Mounts != null && world.Mounts.HasWeaponOn(placement.Id)
                ? BattlementStyle.Parapet : style;

        /// <summary>그 칸 맨 위 조각이 수성 무기 때문에 포구(받침만)로 서 있는지. 검증용.</summary>
        public bool IsGunPortAt(Vector2Int cell)
        {
            BuildPlacement top = world != null && world.Layout != null ? world.Layout.Top(cell) : null;
            return top != null && style == BattlementStyle.Crenellated && StyleFor(top) != style;
        }

        GameObject Build(PlacedStructure structure)
        {
            BuildPlacement placement = structure.Placement;
            BattlementStyle pieceStyle = StyleFor(placement);
            Material material = settings.Material != null ? settings.Material : WallMaterial(structure);
            var root = new GameObject(RootName);
            root.transform.SetParent(structure.transform, false);
            // 조각 몸통은 칸 크기로 늘어나 있다. 성가퀴는 미터 단위로 놓으므로 그 배율을 되돌린다.
            Vector3 scale = structure.transform.localScale;
            root.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            float size = world.Grid.CellSize;
            float surface = placement.Elevation + placement.Definition.Height;
            float thickness = Mathf.Min(settings.Thickness, size * 0.5f);
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    if (world.Layout.Top(cell) != placement) continue;
                    int faces = BattlementRules.Faces(world.Layout, world.Grid, outside, cell);
                    for (int face = 0; face < 4; face++)
                    {
                        if ((faces & (1 << face)) == 0) continue;
                        BattlementRules.Segments(pieceStyle, settings, size, segments);
                        foreach (BattlementSegment segment in segments)
                            AddSegment(root.transform, material, cell, face, segment, size, surface, thickness);
                    }
                }
            return root;
        }

        void AddSegment(Transform root, Material material, Vector2Int cell, int face, BattlementSegment segment,
            float size, float surface, float thickness)
        {
            float minX = cell.x * size, minZ = cell.y * size;
            float along = segment.Start + segment.Length * 0.5f;
            Vector3 local; Vector3 dimensions;
            switch (face)
            {
                case 0: local = new Vector3(minX + along, 0f, minZ + size - thickness * 0.5f); dimensions = new Vector3(segment.Length, 0f, thickness); break;
                case 1: local = new Vector3(minX + size - thickness * 0.5f, 0f, minZ + along); dimensions = new Vector3(thickness, 0f, segment.Length); break;
                case 2: local = new Vector3(minX + along, 0f, minZ + thickness * 0.5f); dimensions = new Vector3(segment.Length, 0f, thickness); break;
                default: local = new Vector3(minX + thickness * 0.5f, 0f, minZ + along); dimensions = new Vector3(thickness, 0f, segment.Length); break;
            }
            local.y = surface + segment.Height * 0.5f;
            dimensions.y = segment.Height;

            var part = new GameObject(segment.Height > settings.ParapetHeight + 0.001f ? "Merlon" : "Parapet");
            part.transform.SetParent(root, false);
            part.transform.SetPositionAndRotation(world.transform.TransformPoint(local), world.transform.rotation);
            part.transform.localScale = Vector3.one;
            // 부모를 거친 크기가 정확히 치수가 되게 한다(뿌리 배율은 1, 회전은 90도 단위).
            Vector3 lossy = part.transform.lossyScale;
            part.transform.localScale = new Vector3(dimensions.x / Mathf.Max(1e-4f, lossy.x),
                dimensions.y / Mathf.Max(1e-4f, lossy.y), dimensions.z / Mathf.Max(1e-4f, lossy.z));
            part.AddComponent<BoxCollider>();
            part.AddComponent<ShotCover>().Configure(CombatTeam.Allied);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(part.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = Cube();
            var renderer = visual.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
        }

        static Material WallMaterial(PlacedStructure structure)
        {
            // 이전 성가퀴는 이미 꺼져 있어 찾지 않는다. 조각 자신의 첫 외형 재질을 쓴다.
            foreach (Renderer renderer in structure.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterial != null) return renderer.sharedMaterial;
            return null;
        }

        static Mesh Cube()
        {
            if (cube != null) return cube;
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return cube;
        }

        void OnDestroy()
        {
            Unsubscribe();
            foreach (Built entry in built.Values)
                if (entry.Root != null) Destroy(entry.Root);
            built.Clear();
        }
    }
}
