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
        // 칸이 킷 모듈보다 넓을 때 면을 덮는 방식(SIEGE-35 시험). 바꾸면 모두 다시 꾸민다.
        [SerializeField] CastleKitLook look = CastleKitLook.Stretch;
        // false면 흉벽 조각(성가퀴·모서리 기둥)의 충돌체·엄폐를 떼고 외형만 둔다. Game은 CastleBattlements가 충돌·엄폐 규칙을 맡는다
        // (SIEGE-19, 사용자 결정 2026-11-23; 규칙을 킷 모양에 맞추는 일은 SIEGE-20).
        [SerializeField] bool battlementColliders = true;
        // 칸마다 킷 조각을 정적 배칭으로 묶는다(SIEGE-19). 쇠창살·문짝이 움직이는 성문 칸은 묶지 않는다.
        // 측정(Game 성 48칸, 렌더러 약 420개, 재질 2종): 성 전체 시점 카메라 렌더 CPU 약 1.29ms → 킷 없을 때와 오차 안(1.05~1.31ms).
        [SerializeField] bool staticBatching = true;

        /// <summary>흉벽 킷 조각이 충돌체·엄폐를 가지는지(SIEGE-19).</summary>
        public bool BattlementColliders => battlementColliders;

        float portcullisBlend = 1f;

        /// <summary>칸이 모듈보다 넓을 때 늘릴지 짜맞출지(SIEGE-35). 바꾸면 다음 프레임에 다시 꾸민다.</summary>
        public CastleKitLook Look
        {
            get => look;
            set { if (look == value) return; look = value; dirty = true; }
        }
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
            /// <summary>정적 배칭이 만든 합친 메쉬. 칸을 걷을 때 함께 지운다.</summary>
            public Mesh Combined;
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
        // 무너진 보루·망루 칸 → 무너지기 전 윗면 높이. 돌무더기·밑동과 맞붙은 성벽 면의 찢긴 흔적을 둔다(SIEGE-30).
        readonly Dictionary<Vector2Int, float> rubble = new Dictionary<Vector2Int, float>();
        // 무너진 성벽 칸 → 무너지기 전 윗면 높이. 그 칸을 향한 계단 방 문을 트인 문으로 남긴다(SIEGE-27).
        readonly Dictionary<Vector2Int, float> fallenTops = new Dictionary<Vector2Int, float>();
        readonly Dictionary<Vector2Int, StairRoom> rooms = new Dictionary<Vector2Int, StairRoom>();
        // 계단 방 층이라 상자 충돌을 끈 조각.
        // 값: 0 꽉 찬 층, 1 방 층, 2 이상 지붕 판을 둔 맨 윗 방 층(계단 짜임별).
        readonly Dictionary<PlacedStructure, int> hollowRooms = new Dictionary<PlacedStructure, int>();
        readonly List<PlacedStructure> goneStructures = new List<PlacedStructure>();
        // 조각마다 짓는 중·문 열림 상태. 바뀌면 다시 맞춘다.
        readonly Dictionary<PlacedStructure, int> watched = new Dictionary<PlacedStructure, int>();
        Transform container;
        bool dirty = true;

        public IReadOnlyList<CastleKitPlacement> Placements => placements;
        public IReadOnlyDictionary<Vector2Int, int> Breaches => breaches;
        public IReadOnlyCollection<Vector2Int> Rubble => rubble.Keys;
        public IReadOnlyDictionary<Vector2Int, StairRoom> Rooms => rooms;
        public Transform Container => container;
        /// <summary>이 칸을 꾸민 킷 조각들의 부모. 꾸미지 않은 칸이면 null(UI-13 해체 미리보기가 잠깐 숨긴다).</summary>
        public GameObject CellRoot(Vector2Int cell) => built.TryGetValue(cell, out Built entry) ? entry.Root : null;
        public int BuiltCellCount
        {
            get
            {
                int count = 0;
                foreach (Built entry in built.Values) if (entry.Root != null) count++;
                return count;
            }
        }

        public void Configure(ConstructionWorld owner, CastleKitSettings kitSettings, bool withBattlementColliders = true)
        {
            Unsubscribe();
            world = owner;
            settings = kitSettings;
            battlementColliders = withBattlementColliders;
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
            bool bastion = placement.Definition.Kind == BuildPieceKind.Bastion || placement.Definition.Kind == BuildPieceKind.Tower;
            for (int x = 0; x < placement.Size.x; x++)
                for (int z = 0; z < placement.Size.y; z++)
                {
                    Vector2Int cell = placement.Cell + new Vector2Int(x, z);
                    // 위층부터 함께 무너지므로 가장 높은 층의 윗면이 무너지기 전 통로 높이다.
                    float top = placement.Elevation + placement.Definition.Height;
                    // 보루·망루는 성벽 바깥에 덧붙은 조각이라 성벽 틈(V자)이 아니라 돌무더기·밑동과 성벽 면의 찢긴 흔적을 남긴다(SIEGE-30).
                    if (bastion) { rubble[cell] = rubble.TryGetValue(cell, out float was) ? Mathf.Max(was, top) : top; continue; }
                    breaches[cell] = breaches.TryGetValue(cell, out int level) ? Mathf.Min(level, placement.Level) : placement.Level;
                    fallenTops[cell] = fallenTops.TryGetValue(cell, out float known) ? Mathf.Max(known, top) : top;
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

        /// <summary>
        /// 성이 첫 길찾기 메시를 만들기 직전에 부른다(<see cref="ConstructionWorld"/> Awake, SIEGE-19).
        /// 킷 충돌체가 곧 만들 메시에 담기므로 다시 만들기를 요청하지 않는다(시작하자마자 메시를 한 번 더 만들면 그동안 전투를 시작할 수 없다).
        /// </summary>
        public void BuildBeforeNavigation(ConstructionWorld owner)
        {
            if (world == null) world = owner;
            if (world != owner) return;
            Refresh(false);
        }

        public void Refresh() => Refresh(true);

        void Refresh(bool requestNavigation)
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
            CastleKitRules.Collect(world.Layout, world.Grid, outside, unfinished, breaches, settings, placements, rubble, fallenTops, rooms, look);
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
                existing.Combined = Batch(existing.Root);
                navigation |= existing.HasColliders;
            }
            navigation |= UpdateStructures();
            // 기둥·계단 충돌체가 바뀌면 길찾기 메시를 다시 만든다(성가퀴와 같은 방식).
            if (navigation && requestNavigation) world.RequestNavigationRebuild();
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
            foreach (Vector2Int cell in rubble.Keys) if (world.Layout.Top(cell) != null) stale.Add(cell);
            foreach (Vector2Int cell in stale) rubble.Remove(cell);
            stale.Clear();
            foreach (KeyValuePair<Vector2Int, float> entry in fallenTops)
                if (!breaches.ContainsKey(entry.Key) || world.Layout.WallSurface(entry.Key) >= entry.Value - 0.01f) stale.Add(entry.Key);
            foreach (Vector2Int cell in stale) fallenTops.Remove(cell);
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
        // 망루 맨윗층의 방 충돌 상태가 바뀌면 true(길찾기 메시를 다시 만든다).
        bool UpdateStructures()
        {
            bool changed = false;
            // 없어진 조각은 계단 방 기록에서 뺀다.
            goneStructures.Clear();
            foreach (PlacedStructure structure in hollowRooms.Keys) if (structure == null) goneStructures.Add(structure);
            foreach (PlacedStructure structure in goneStructures) hollowRooms.Remove(structure);
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null || structure.Placement == null) continue;
                BuildPlacement placement = structure.Placement;
                bool dressed = CastleKitRules.IsWallLike(placement.Definition.Kind) && !structure.IsUnderConstruction;
                Transform visual = structure.transform.Find("Visual");
                if (visual != null)
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = !dressed;
                if (placement.Definition.Kind == BuildPieceKind.Tower) changed |= UpdateTowerRoom(structure, dressed);
                else if (placement.Definition.Kind == BuildPieceKind.Wall) changed |= UpdateStairRoom(structure);
            }
            return changed;
        }

        /// <summary>
        /// 계단 방 층(SIEGE-27)이면 조각 상자 충돌을 끈다. 벽·바닥·층참·경사는 킷 충돌체(벽판, <see cref="CastleKitPiece.RoomCollider"/>)가 맡는다.
        /// 방이 아니게 되면(이웃이 바뀜, 짓는 중) 상자를 되살린다. 바뀌면 true(길찾기 메시를 다시 만든다).
        /// </summary>
        bool UpdateStairRoom(PlacedStructure structure)
        {
            BuildPlacement placement = structure.Placement;
            bool hollow = rooms.TryGetValue(placement.Cell, out StairRoom room) && room.Contains(placement.Level);
            bool roof = hollow && placement.Level == room.TopLevel;
            int key = !hollow ? 0 : !roof ? 1 : 2 + room.FootFace * 4 + room.FirstFlightFace;
            Transform hollowTop = structure.transform.Find(HollowTopName);
            hollowRooms.TryGetValue(structure, out int current);
            if (current == key && roof == (hollowTop != null)) return false;
            hollowRooms[structure] = key;
            foreach (Collider collider in structure.GetComponents<Collider>())
                if (!collider.isTrigger && collider != structure.DoorCollider) collider.enabled = !hollow;
            if (hollowTop != null) { hollowTop.SetParent(null); Destroy(hollowTop.gameObject); }
            if (!roof) return true;

            // 건설 화면에서 맨 윗층을 가리켜 고를 수 있게 지붕 판을 조각 아래에도 둔다(망루와 같다). 계단 구멍은 비운다.
            var slab = new GameObject(HollowTopName);
            slab.layer = structure.gameObject.layer;
            slab.transform.SetParent(structure.transform, false);
            StairWell well = StairWell.Create(placement.Cell, world.Grid.CellSize, placement.Elevation, placement.Definition.Height,
                room.FootFace, room.FirstFlightFace, settings.RoomWallInset, settings.RoomLandingDepth, settings.RoomHoleOverhang,
                settings.RoomFlightWidth);
            var ceiling = new List<Rect>();
            well.CeilingAreas(ceiling);
            float thickness = Mathf.Min(0.5f, 0.25f / Mathf.Max(0.1f, placement.Definition.Height));
            foreach (Rect area in ceiling)
            {
                Vector3 a = Normalized(structure, new Vector3(area.xMin, 0f, area.yMin));
                Vector3 b = Normalized(structure, new Vector3(area.xMax, 0f, area.yMax));
                AddSlab(slab, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z), thickness);
            }
            return true;
        }

        const string HollowTopName = "HollowTop";
        // 망루 조각마다 지금 둔 방 짜임(0이면 방 없음, 상자 충돌).
        readonly Dictionary<PlacedStructure, int> towerRooms = new Dictionary<PlacedStructure, int>();

        /// <summary>
        /// 망루 맨윗층이 방이면(킷이 벽판·계단 충돌체를 둔다) 조각 상자 충돌을 끄고, 지붕 판만 계단 위를 뚫어 둔다.
        /// 판은 조각 아래에 두어 지붕을 가리킨 무기 배치가 그 망루를 찾는다. 방이 아니면(아래층, 짓는 중) 상자를 되살린다.
        /// </summary>
        bool UpdateTowerRoom(PlacedStructure structure, bool dressed)
        {
            BuildPlacement placement = structure.Placement;
            // 가장 낮은 통로 바닥부터 위로 모든 층이 방이다(SIEGE-29).
            TowerRules.Interior room = dressed ? TowerRules.RoomOf(world.Layout, world.Grid, placement) : default;
            int key = room.HasRoom ? 1 + room.InnerFace * 7 + (room.StairCell.x * 31 + room.StairCell.y) * 17 + room.Doors * 1009 : 0;
            Transform hollow = structure.transform.Find(HollowTopName);
            towerRooms.TryGetValue(structure, out int current);
            if (current == key && (key == 0) == (hollow == null)) return false;
            towerRooms[structure] = key;
            foreach (Collider collider in structure.GetComponents<Collider>())
                if (!collider.isTrigger && collider != structure.DoorCollider) collider.enabled = !room.HasRoom;
            if (hollow != null) { hollow.SetParent(null); Destroy(hollow.gameObject); }
            if (!room.HasRoom) return true;

            var slab = new GameObject(HollowTopName);
            slab.layer = structure.gameObject.layer;
            slab.transform.SetParent(structure.transform, false);
            // 조각 좌표는 바닥 가운데가 원점, 가로·세로 −0.5~0.5, 높이 0~1(크기는 조각 transform이 늘린다).
            float size = world.Grid.CellSize;
            Vector2Int inner = BattlementRules.Directions[room.InnerFace];
            var d = new Vector3(inner.x, 0f, inner.y);
            var center = new Vector3((room.StairCell.x + 0.5f) * size, 0f, (room.StairCell.y + 0.5f) * size);
            // 구멍은 통로 쪽 벽에서 계단 폭까지만(킷 천장 조각과 같은 깊이, SIEGE-29).
            float depth = Mathf.Min(size, settings.TowerOpeningDepth);
            // 오르는 쪽으로는 계단 발치부터 꼭대기 뒤 여유까지만 뚫는다(킷 천장 조각과 같은 범위).
            Vector2Int rise = BattlementRules.Directions[room.RiseFace];
            var up = new Vector3(rise.x, 0f, rise.y);
            Vector3 a = Normalized(structure, center + d * (size * 0.5f - depth) + up * settings.TowerHoleStart(size));
            Vector3 b = Normalized(structure, center + d * (size * 0.5f) + up * settings.TowerHoleEnd(size));
            float hx0 = Mathf.Min(a.x, b.x), hx1 = Mathf.Max(a.x, b.x), hz0 = Mathf.Min(a.z, b.z), hz1 = Mathf.Max(a.z, b.z);
            float thickness = Mathf.Min(0.5f, 0.25f / Mathf.Max(0.1f, placement.Definition.Height));
            AddSlab(slab, -0.5f, hx0, -0.5f, 0.5f, thickness);
            AddSlab(slab, hx1, 0.5f, -0.5f, 0.5f, thickness);
            AddSlab(slab, hx0, hx1, -0.5f, hz0, thickness);
            AddSlab(slab, hx0, hx1, hz1, 0.5f, thickness);
            return true;
        }

        // 성 좌표 점을 조각의 정규화 좌표로.
        Vector3 Normalized(PlacedStructure structure, Vector3 castlePoint) =>
            structure.transform.InverseTransformPoint(world.transform.TransformPoint(castlePoint));

        static void AddSlab(GameObject owner, float x0, float x1, float z0, float z1, float thickness)
        {
            if (x1 - x0 < 0.001f || z1 - z0 < 0.001f) return;
            var box = owner.AddComponent<BoxCollider>();
            box.center = new Vector3((x0 + x1) * 0.5f, 1f - thickness * 0.5f, (z0 + z1) * 0.5f);
            box.size = new Vector3(x1 - x0, thickness, z1 - z0);
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
                    hash = hash * 31 + Mathf.RoundToInt(placement.StretchX * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(placement.StretchZ * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(placement.Tilt);
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
            // 조각은 모듈 크기 그대로다. 칸에 맞춘 늘림은 규칙이 자리마다 준다(SIEGE-35).
            foreach (CastleKitPlacement placement in list)
            {
                if (placement.Piece == CastleKitPiece.RoomCollider)
                {
                    AddRoomCollider(root.transform, placement);
                    hasColliders = true;
                    continue;
                }
                if (placement.Piece == CastleKitPiece.RoomLink)
                {
                    AddRoomLink(root.transform, placement);
                    continue;
                }
                GameObject prefab = settings.Prefab(placement.Piece);
                if (prefab == null) continue;
                GameObject piece = Instantiate(prefab, root.transform);
                piece.name = placement.Piece.ToString();
                piece.transform.localPosition = placement.Position;
                piece.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                bool corner = placement.Piece == CastleKitPiece.CornerStairs || placement.Piece == CastleKitPiece.CornerStairsOpen;
                float stretch = corner ? settings.CornerStairStretch : 1f;
                bool ruin = placement.Piece == CastleKitPiece.BrokenTop || placement.Piece == CastleKitPiece.RubbleTop;
                float stretchX = ruin ? settings.BreachHalfWidth : stretch;
                float uniform = placement.Piece == CastleKitPiece.TowerCap ? settings.TowerCapScale : 1f;
                piece.transform.localScale = uniform * new Vector3(stretchX * placement.StretchX, placement.ScaleY,
                    (placement.MirrorZ ? -1f : 1f) * stretch * placement.StretchZ);
                if (!battlementColliders && IsBattlement(placement.Piece)) StripCollision(piece);
                hasColliders |= piece.GetComponentInChildren<Collider>(true) != null;
            }
            return root;
        }

        // 칸 조각을 정적 배칭으로 묶고 합친 메쉬를 돌려준다(성문 칸·에디터 모드는 묶지 않는다).
        Mesh Batch(GameObject root)
        {
            if (!staticBatching || !Application.isPlaying || root.transform.Find(CastleKitPiece.GateOuter.ToString()) != null) return null;
            StaticBatchingUtility.Combine(root);
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.isPartOfStaticBatch) return renderer.GetComponent<MeshFilter>().sharedMesh;
            return null;
        }

        static bool IsBattlement(CastleKitPiece piece) =>
            piece == CastleKitPiece.Battlement || piece == CastleKitPiece.BattlementCap || piece == CastleKitPiece.BattlementHalf;

        // 외형 전용으로 만든다. 같은 프레임의 길찾기 수집·충돌에서 빠지도록 바로 지운다(엄폐가 충돌체를 요구하므로 엄폐부터).
        static void StripCollision(GameObject piece)
        {
            foreach (ProjectNeo.Combat.ShotCover cover in piece.GetComponentsInChildren<ProjectNeo.Combat.ShotCover>(true)) DestroyImmediate(cover);
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        }

        // 계단 방의 바닥·층참·경사 판. 칸 루트는 늘리지 않으므로 자기 크기로 늘려도 비틀리지 않는다.
        static void AddRoomCollider(Transform parent, CastleKitPlacement placement)
        {
            var box = new GameObject(CastleKitPiece.RoomCollider.ToString(), typeof(BoxCollider));
            box.transform.SetParent(parent, false);
            box.transform.localPosition = placement.Position;
            box.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f) * Quaternion.Euler(0f, 0f, placement.Tilt);
            box.transform.localScale = new Vector3(placement.StretchX, placement.ScaleY, placement.StretchZ);
        }

        // 계단 방의 꺾이는 곳을 가로지르는 양방향 길찾기 연결. 길찾기 메시를 다시 만들어도 끝점이 새 메시에 다시 붙는다.
        static void AddRoomLink(Transform parent, CastleKitPlacement placement)
        {
            var owner = new GameObject(CastleKitPiece.RoomLink.ToString());
            owner.transform.SetParent(parent, false);
            owner.transform.localPosition = placement.Position;
            owner.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
            var link = owner.AddComponent<Unity.AI.Navigation.NavMeshLink>();
            link.startPoint = new Vector3(-placement.StretchX * 0.5f, 0f, 0f);
            link.endPoint = new Vector3(placement.StretchX * 0.5f, 0f, 0f);
            link.width = placement.StretchZ;
            link.bidirectional = true;
        }

        // 걷어 낸 칸에 충돌체가 있었으면 true(길찾기 메시를 다시 만들어야 한다).
        static bool Discard(Built entry)
        {
            if (entry.Combined != null) { Destroy(entry.Combined); entry.Combined = null; }
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
