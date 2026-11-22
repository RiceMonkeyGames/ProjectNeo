using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성벽·보루·성문 칸을 PolygonFantasyKingdom 성 킷 조각으로 꾸민다(SIEGE-14 시험).
    /// 배치가 바뀔 때마다 <see cref="CastleKitRules"/>로 다시 계산하고 바뀐 칸만 다시 만든다. 꾸민 조각은 임시 외형(Visual)을 숨긴다.
    /// 조각 충돌·길찾기·전투 규칙은 조각 루트의 상자 그대로이고, 킷 프리팹 중 충돌체를 가진 것(튀어나온 기둥, 성벽 위 계단)만
    /// 길찾기 메시에 들어간다. 무너진 칸은 기억해 두었다가 그 칸을 향한 면을 비운다(다시 지으면 잊는다).
    /// </summary>
    public sealed class CastleKitDresser : MonoBehaviour
    {
        const string RootName = "CastleKit";

        [SerializeField] ConstructionWorld world;
        [SerializeField] CastleKitSettings settings;
        // 쇠창살: 준비 단계에서 올리고 전투 시작에 내린다(사용자 결정 2026-11-22, 연결은 Game 통합 때). 시험 씬은 키로 바꾼다.
        [SerializeField] bool portcullisRaised = true;
        [SerializeField, Min(0.05f)] float gateMotionSeconds = 1.5f;
        // 쇠창살을 올리는 높이(모듈 기준 m). 성문 칸이 2층이면 쇠창살이 그대로 위층 바깥 벽판 속으로 올라가 숨는다.
        [SerializeField, Min(0f)] float portcullisLift = 4.6f;
        // 성문 위층이 없을 때(무너짐 등)만 쓰는 대체 연출: 올린 쇠창살이 문틀 윗부분에 남는 높이 비율(위 통로를 뚫지 않게 접힌다).
        [SerializeField, Range(0.05f, 1f)] float raisedPortcullisFraction = 0.18f;
        [SerializeField, Range(0f, 150f)] float openDoorAngle = 100f;

        float portcullisBlend = 1f;
        float doorBlend;

        sealed class GatePart
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public float MeshTop;
            public Vector3 Hinge;
            public float Swing;
        }

        readonly List<GatePart> portcullis = new List<GatePart>();
        readonly List<GatePart> doors = new List<GatePart>();
        GameObject gatePartsOwner;

        /// <summary>쇠창살을 올렸는지. 바꾸면 <see cref="gateMotionSeconds"/>에 걸쳐 움직인다.</summary>
        public bool PortcullisRaised { get => portcullisRaised; set => portcullisRaised = value; }
        /// <summary>쇠창살이 올라간 정도(0 내림 ~ 1 올림, 검증용).</summary>
        public float PortcullisBlend => portcullisBlend;
        /// <summary>문짝이 열린 정도(0 닫힘 ~ 1 열림, 검증용).</summary>
        public float DoorBlend => doorBlend;

        sealed class Built
        {
            public GameObject Root;
            public int Signature;
            public bool HasColliders;
        }

        readonly Dictionary<Vector2Int, Built> built = new Dictionary<Vector2Int, Built>();
        readonly Dictionary<Vector2Int, List<CastleKitPlacement>> byCell = new Dictionary<Vector2Int, List<CastleKitPlacement>>();
        readonly Stack<List<CastleKitPlacement>> pool = new Stack<List<CastleKitPlacement>>();
        readonly List<CastleKitPlacement> placements = new List<CastleKitPlacement>();
        readonly List<Vector2Int> stale = new List<Vector2Int>();
        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        // 기본 성(시작 배치)이 둘러싼 안뜰. 성벽이 무너져 틈이 나도 늘 안쪽이다(성가퀴와 같은 규칙).
        readonly HashSet<Vector2Int> enclosed = new HashSet<Vector2Int>();
        bool enclosedReady;
        readonly HashSet<int> unfinished = new HashSet<int>();
        readonly Dictionary<Vector2Int, int> breaches = new Dictionary<Vector2Int, int>();
        // 무너진 보루 칸. 돌무더기만 깔고 뒤의 성벽은 온전하게 둔다.
        readonly HashSet<Vector2Int> rubble = new HashSet<Vector2Int>();
        // 조각마다 짓는 중·문 열림 상태. 바뀌면 다시 맞춘다.
        readonly Dictionary<PlacedStructure, int> watched = new Dictionary<PlacedStructure, int>();
        Transform container;
        bool dirty = true;

        public IReadOnlyList<CastleKitPlacement> Placements => placements;
        public IReadOnlyDictionary<Vector2Int, int> Breaches => breaches;
        public IReadOnlyCollection<Vector2Int> Rubble => rubble;
        public Transform Container => container;
        public int BuiltCellCount
        {
            get
            {
                int count = 0;
                foreach (Built entry in built.Values) if (entry.Root != null) count++;
                return count;
            }
        }

        public void Configure(ConstructionWorld owner, CastleKitSettings kitSettings)
        {
            Unsubscribe();
            world = owner;
            settings = kitSettings;
            enclosedReady = false;
            Subscribe();
            dirty = true;
        }

        void OnEnable() { Subscribe(); dirty = true; }
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            if (world == null || !isActiveAndEnabled) return;
            world.Changed -= MarkDirty; world.Changed += MarkDirty;
            world.PieceCollapsing -= RememberBreach; world.PieceCollapsing += RememberBreach;
        }

        void Unsubscribe()
        {
            if (world == null) return;
            world.Changed -= MarkDirty;
            world.PieceCollapsing -= RememberBreach;
        }

        void MarkDirty() { dirty = true; Refresh(); }

        // 전투로 무너진 조각의 칸과 층. 철거(직접 뺀 것)는 무너짐이 아니라 기억하지 않는다.
        void RememberBreach(PlacedStructure structure)
        {
            BuildPlacement placement = structure != null ? structure.Placement : null;
            if (placement == null || !CastleKitRules.IsWallLike(placement.Definition.Kind)) return;
            bool bastion = placement.Definition.Kind == BuildPieceKind.Bastion;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    // 보루는 성벽 바깥에 덧붙은 조각이라 성벽 틈(V자)이 아니라 돌무더기만 남긴다.
                    if (bastion) { rubble.Add(cell); continue; }
                    breaches[cell] = breaches.TryGetValue(cell, out int level) ? Mathf.Min(level, placement.Level) : placement.Level;
                }
            dirty = true;
        }

        static int State(PlacedStructure structure) => (structure.IsUnderConstruction ? 1 : 0) | (structure.IsDoorOpen ? 2 : 0);

        void Update()
        {
            if (!dirty)
                foreach (KeyValuePair<PlacedStructure, int> entry in watched)
                    if (entry.Key == null || State(entry.Key) != entry.Value) { dirty = true; break; }
            if (dirty) Refresh();
            AnimateGate();
        }

        // 성문 바깥 틀의 쇠창살(올림·내림)과 문짝(열림·닫힘)을 천천히 움직인다. 부서진 성문은 둘 다 숨긴다.
        void AnimateGate()
        {
            PlacedStructure gate = null;
            if (world != null)
                foreach (PlacedStructure structure in world.Structures)
                    if (structure != null && structure.Placement != null && structure.Placement.Definition.Kind == BuildPieceKind.Gate) { gate = structure; break; }
            if (gate == null || !built.TryGetValue(gate.Placement.Cell, out Built entry) || entry.Root == null) return;
            Transform outer = entry.Root.transform.Find(CastleKitPiece.GateOuter.ToString());
            if (outer == null) return;
            if (gatePartsOwner != outer.gameObject) CacheGateParts(outer);

            bool broken = gate.IsDoorOpen && !gate.IsHeldOpen;
            float step = Time.deltaTime / gateMotionSeconds;
            portcullisBlend = Mathf.MoveTowards(portcullisBlend, portcullisRaised ? 1f : 0f, step);
            doorBlend = Mathf.MoveTowards(doorBlend, gate.IsHeldOpen ? 1f : 0f, step);
            float eased = Mathf.SmoothStep(0f, 1f, portcullisBlend);
            // 성문 칸이 2층 이상이면 쇠창살 모델을 그대로 위로 올린다(위층 벽판 속에 숨는다).
            bool housed = world.Layout.ColumnHeight(gate.Placement.Cell) >= 2;
            foreach (GatePart part in portcullis)
            {
                part.Transform.gameObject.SetActive(!broken);
                Vector3 scale = part.Scale;
                Vector3 position = part.Position;
                if (housed) position.y += portcullisLift * eased;
                else
                {
                    // 위층이 없으면 윗변을 고정하고 아래에서부터 접혀 올라간다.
                    scale.y *= Mathf.Lerp(1f, raisedPortcullisFraction, eased);
                    position.y += (part.Scale.y - scale.y) * part.MeshTop;
                }
                part.Transform.localScale = scale;
                part.Transform.localPosition = position;
            }
            float swing = Mathf.SmoothStep(0f, 1f, doorBlend);
            foreach (GatePart part in doors)
            {
                part.Transform.gameObject.SetActive(!broken);
                Quaternion turn = Quaternion.Euler(0f, part.Swing * swing, 0f);
                part.Transform.localRotation = turn * part.Rotation;
                part.Transform.localPosition = part.Hinge + turn * (part.Position - part.Hinge);
            }
        }

        // 바깥 틀이 새로 만들어지면 쇠창살·문짝의 처음 자세와 경첩을 다시 잡는다.
        void CacheGateParts(Transform outer)
        {
            gatePartsOwner = outer.gameObject;
            portcullis.Clear();
            doors.Clear();
            foreach (MeshFilter filter in outer.GetComponentsInChildren<MeshFilter>(true))
            {
                Transform part = filter.transform;
                if (part == outer || part.parent != outer || filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                var cached = new GatePart
                {
                    Transform = part, Position = part.localPosition, Rotation = part.localRotation, Scale = part.localScale,
                    MeshTop = bounds.max.y,
                };
                if (part.name.Contains("Portcullis")) portcullis.Add(cached);
                else if (part.name.Contains("Door"))
                {
                    // 왼쪽 문짝은 왼쪽(−x) 끝, 오른쪽 문짝은 오른쪽(+x) 끝이 경첩이다. 둘 다 성 안쪽(−z)으로 열린다.
                    bool left = part.name.Contains("_L_");
                    Vector3 hingeLocal = new Vector3(left ? bounds.min.x : bounds.max.x, 0f, bounds.center.z);
                    cached.Hinge = part.localPosition + part.localRotation * Vector3.Scale(hingeLocal, part.localScale);
                    cached.Swing = left ? openDoorAngle : -openDoorAngle;
                    doors.Add(cached);
                }
            }
        }

        public void Refresh()
        {
            if (world == null || world.Layout == null || settings == null) return;
            dirty = false;
            EnsureEnclosed();
            BattlementRules.CollectOutside(world.Layout, world.Grid, outside, enclosed);
            ForgetRebuiltBreaches();
            unfinished.Clear();
            watched.Clear();
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null || structure.Placement == null) continue;
                watched[structure] = State(structure);
                if (structure.IsUnderConstruction) unfinished.Add(structure.Placement.Id);
            }
            CastleKitRules.Collect(world.Layout, world.Grid, outside, unfinished, breaches, settings, placements, rubble);
            Group();

            bool navigation = false;
            stale.Clear();
            foreach (Vector2Int cell in built.Keys) if (!byCell.ContainsKey(cell)) stale.Add(cell);
            foreach (Vector2Int cell in stale) { navigation |= Discard(built[cell]); built.Remove(cell); }
            foreach (KeyValuePair<Vector2Int, List<CastleKitPlacement>> entry in byCell)
            {
                int signature = Signature(entry.Value);
                if (built.TryGetValue(entry.Key, out Built existing) && existing.Root != null && existing.Signature == signature) continue;
                if (existing == null) built[entry.Key] = existing = new Built();
                navigation |= Discard(existing);
                existing.Signature = signature;
                existing.Root = Build(entry.Key, entry.Value, out existing.HasColliders);
                navigation |= existing.HasColliders;
            }
            UpdateStructures();
            // 기둥·계단 충돌체가 바뀌면 길찾기 메시를 다시 만든다(성가퀴와 같은 방식).
            if (navigation) world.RequestNavigationRebuild();
        }

        void EnsureEnclosed()
        {
            if (enclosedReady) return;
            enclosedReady = true;
            var start = new BuildLayout(world.Grid);
            // 보루는 성벽 바깥에 덧붙은 조각이라 뺀다. 넣으면 무너진 보루 칸이 늘 안뜰로 남아,
            // 뒤 성벽의 바깥 면이 안쪽 장식(성가퀴 없음)으로 바뀐다.
            foreach (InitialBuildPiece seed in world.InitialPieces)
                if (seed.definition != null && seed.definition.Kind != BuildPieceKind.Bastion)
                    start.TryPlace(seed.definition, seed.cell, seed.rotation, out _, seed.fixedGate);
            BattlementRules.CollectEnclosed(start, world.Grid, enclosed);
        }

        void ForgetRebuiltBreaches()
        {
            stale.Clear();
            foreach (KeyValuePair<Vector2Int, int> entry in breaches)
                if (world.Layout.At(entry.Key, entry.Value) != null) stale.Add(entry.Key);
            foreach (Vector2Int cell in stale) breaches.Remove(cell);
            stale.Clear();
            foreach (Vector2Int cell in rubble) if (world.Layout.Top(cell) != null) stale.Add(cell);
            foreach (Vector2Int cell in stale) rubble.Remove(cell);
        }

        void Group()
        {
            foreach (List<CastleKitPlacement> list in byCell.Values) { list.Clear(); pool.Push(list); }
            byCell.Clear();
            foreach (CastleKitPlacement placement in placements)
            {
                if (!byCell.TryGetValue(placement.Cell, out List<CastleKitPlacement> list))
                    byCell[placement.Cell] = list = pool.Count > 0 ? pool.Pop() : new List<CastleKitPlacement>();
                list.Add(placement);
            }
        }

        // 다 지은 성벽류의 임시 외형은 숨긴다(짓는 중이거나 계단 등은 그대로). 성문 문짝·쇠창살은 AnimateGate가 맡는다.
        void UpdateStructures()
        {
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null || structure.Placement == null) continue;
                BuildPlacement placement = structure.Placement;
                bool dressed = CastleKitRules.IsWallLike(placement.Definition.Kind) && !structure.IsUnderConstruction;
                Transform visual = structure.transform.Find("Visual");
                if (visual != null)
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = !dressed;
            }
        }

        static int Signature(List<CastleKitPlacement> list)
        {
            unchecked
            {
                int hash = 17;
                foreach (CastleKitPlacement placement in list)
                {
                    hash = hash * 31 + (int)placement.Piece;
                    hash = hash * 31 + Mathf.RoundToInt(placement.Position.x * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(placement.Position.y * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(placement.Position.z * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(placement.Yaw);
                    hash = hash * 31 + (placement.MirrorZ ? 1 : 0);
                    hash = hash * 31 + Mathf.RoundToInt(placement.ScaleY * 100f);
                }
                return hash;
            }
        }

        Transform EnsureContainer()
        {
            if (container != null) return container;
            container = new GameObject(RootName).transform;
            // 길찾기 메시는 Geometry 아래만 모은다. 튀어나온 기둥·계단 충돌체가 메시에 들어가도록 그 아래에 둔다.
            // 조각 좌표는 성 좌표이므로 Geometry가 성과 같은 자리·방향이어야 한다(기본 구성).
            Transform parent = world.GeometryRoot != null ? world.GeometryRoot : world.transform;
            container.SetParent(parent, false);
            container.SetPositionAndRotation(world.transform.position, world.transform.rotation);
            return container;
        }

        GameObject Build(Vector2Int cell, List<CastleKitPlacement> list, out bool hasColliders)
        {
            hasColliders = false;
            var root = new GameObject($"{RootName} {cell.x},{cell.y}");
            root.transform.SetParent(EnsureContainer(), false);
            float scale = world.Grid.CellSize / settings.ModuleSize;
            foreach (CastleKitPlacement placement in list)
            {
                GameObject prefab = settings.Prefab(placement.Piece);
                if (prefab == null) continue;
                GameObject piece = Instantiate(prefab, root.transform);
                piece.name = placement.Piece.ToString();
                piece.transform.localPosition = placement.Position;
                piece.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                float stretch = placement.Piece == CastleKitPiece.CornerStairs ? settings.CornerStairStretch : 1f;
                bool ruin = placement.Piece == CastleKitPiece.BrokenTop || placement.Piece == CastleKitPiece.RubbleTop;
                float stretchX = ruin ? settings.BreachHalfWidth : stretch;
                piece.transform.localScale = new Vector3(scale * stretchX, scale * placement.ScaleY, (placement.MirrorZ ? -scale : scale) * stretch);
                hasColliders |= piece.GetComponentInChildren<Collider>(true) != null;
            }
            return root;
        }

        // 걷어 낸 칸에 충돌체가 있었으면 true(길찾기 메시를 다시 만들어야 한다).
        static bool Discard(Built entry)
        {
            if (entry.Root == null) return false;
            entry.Root.SetActive(false);
            Destroy(entry.Root);
            entry.Root = null;
            return entry.HasColliders;
        }

        void OnDestroy()
        {
            Unsubscribe();
            foreach (Built entry in built.Values) Discard(entry);
            built.Clear();
        }
    }
}
