using System.Collections.Generic;
using ProjectNeo.Combat;
using Unity.AI.Navigation;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 성문 위 마치콜레이션(SIEGE-12, 사용자 결정 2026-11-22). 처음부터 있고 비용이 없으며 짓거나 헐 수 없다.
    /// 성문 칸마다 성 바깥을 향한 면 가운데에 구멍(<see cref="ShotOpening"/>)을 하나씩 두고, 배치가 바뀔 때마다 다시 맞춘다.
    /// 구멍 통로 안에서는 그 칸의 조각(성문과 그 위에 쌓은 조각)이 아래로 쏘는 화살을 막지 않는다.
    /// 외형은 바깥 면 위쪽의 돌출 받침과 구멍 표시뿐이며 충돌체와 길찾기 영향이 없다.
    /// </summary>
    public sealed class GateMachicolations : MonoBehaviour
    {
        const string RootName = "Machicolations";
        const float BelowGround = 0.5f;
        const float Above = 3f;

        [SerializeField] ConstructionWorld world;
        [SerializeField] MachicolationSettings settings;

        readonly HashSet<Vector2Int> outside = new HashSet<Vector2Int>();
        readonly List<MachicolationSpot> spots = new List<MachicolationSpot>();
        readonly List<ShotOpening> openings = new List<ShotOpening>();
        readonly Dictionary<PlacedStructure, bool> watched = new Dictionary<PlacedStructure, bool>();
        readonly List<Transform> roots = new List<Transform>();
        Transform root;
        int outsideRevision = -1;
        bool dirty = true;
        static Mesh cube;

        public IReadOnlyList<ShotOpening> Openings => openings;
        public MachicolationSettings Settings => settings;

        public void Configure(ConstructionWorld owner, MachicolationSettings machicolationSettings)
        {
            Unsubscribe();
            world = owner;
            settings = machicolationSettings;
            Subscribe();
            dirty = true;
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

        void MarkDirty() { dirty = true; Refresh(); }

        void Update()
        {
            if (!dirty)
                foreach (KeyValuePair<PlacedStructure, bool> entry in watched)
                    if (entry.Key == null || entry.Key.IsUnderConstruction != entry.Value) { dirty = true; break; }
            if (dirty) Refresh();
        }

        /// <summary>지금 배치에 맞춰 구멍을 다시 만든다.</summary>
        public void Refresh()
        {
            if (world == null || world.Layout == null || settings == null) return;
            dirty = false;
            Clear();
            if (outsideRevision != world.LayoutRevision)
            {
                BattlementRules.CollectOutside(world.Layout, world.Grid, outside);
                outsideRevision = world.LayoutRevision;
            }
            MachicolationLayout.Collect(world.Layout, world.Grid, outside, settings.StandInset, spots);
            if (spots.Count == 0) return;
            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            float inset = Mathf.Clamp(settings.StandInset, 0f, world.Grid.CellSize * 0.5f);
            foreach (MachicolationSpot spot in spots)
            {
                if (!CollectColumn(spot.Cell, out bool underConstruction) || underConstruction) continue;
                Build(spot, inset);
            }
        }

        // 그 칸을 차지한 조각(모든 층)을 roots에 모은다. 맨 위 조각이 짓는 중이면 구멍을 만들지 않는다.
        bool CollectColumn(Vector2Int cell, out bool underConstruction)
        {
            roots.Clear();
            underConstruction = false;
            BuildPlacement top = world.Layout.Top(cell);
            foreach (PlacedStructure structure in world.Structures)
            {
                BuildPlacement placement = structure != null ? structure.Placement : null;
                if (placement == null || !Covers(placement, cell)) continue;
                roots.Add(structure.transform);
                watched[structure] = structure.IsUnderConstruction;
                if (placement == top) underConstruction = structure.IsUnderConstruction;
            }
            return roots.Count > 0;
        }

        static bool Covers(BuildPlacement placement, Vector2Int cell) =>
            cell.x >= placement.Cell.x && cell.y >= placement.Cell.y
            && cell.x < placement.Cell.x + placement.Size.x && cell.y < placement.Cell.y + placement.Size.y;

        void Build(MachicolationSpot spot, float inset)
        {
            var body = new GameObject("Machicolation");
            body.transform.SetParent(root, false);
            Vector3 outward = world.transform.TransformDirection(spot.Outward);
            body.transform.SetPositionAndRotation(world.transform.TransformPoint(spot.StandPoint),
                Quaternion.LookRotation(outward, world.transform.up));
            var opening = body.AddComponent<ShotOpening>();
            opening.Configure(CombatTeam.Allied,
                MachicolationShaft.FromFace(settings.ShaftWidth, inset, settings.ShaftInward, settings.ShaftOutward,
                    spot.Drop, BelowGround, Above),
                settings.StandRadius, settings.SearchRadius, roots.ToArray(), settings.Attacks);
            openings.Add(opening);
            BuildVisual(body.transform, inset);
        }

        // 바깥 면 위쪽에 돌출 받침, 걷는 면에 구멍 표시. 충돌체 없음, 길찾기 메시에서 뺀다.
        void BuildVisual(Transform body, float inset)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(body, false);
            visual.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            Material material = settings.Material != null ? settings.Material : FirstMaterial();
            float depth = settings.OverhangDepth, height = settings.OverhangHeight;
            AddBox(visual.transform, "Overhang", new Vector3(0f, -height * 0.5f - 0.02f, inset + depth * 0.5f),
                new Vector3(settings.OverhangWidth, height, depth), material);
            if (settings.HoleMaterial != null)
                AddBox(visual.transform, "Hole", new Vector3(0f, 0.01f, inset * 0.5f),
                    new Vector3(Mathf.Min(0.75f, settings.OverhangWidth), 0.02f, Mathf.Max(0.1f, inset * 0.8f)),
                    settings.HoleMaterial);
        }

        static void AddBox(Transform parent, string name, Vector3 local, Vector3 size, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localScale = size;
            part.AddComponent<MeshFilter>().sharedMesh = Cube();
            var renderer = part.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            part.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        Material FirstMaterial()
        {
            foreach (Transform piece in roots)
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>())
                    if (renderer.sharedMaterial != null) return renderer.sharedMaterial;
            return null;
        }

        static Mesh Cube()
        {
            if (cube == null) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return cube;
        }

        void Clear()
        {
            foreach (ShotOpening opening in openings)
                if (opening != null) opening.gameObject.SetActive(false);
            openings.Clear();
            watched.Clear();
            if (root == null) return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            root = null;
        }

        void OnDestroy()
        {
            Unsubscribe();
            Clear();
        }
    }
}
