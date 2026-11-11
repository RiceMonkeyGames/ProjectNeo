using System;
using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using Unity.AI.Navigation;
using UnityEngine;

namespace ProjectNeo.Walls
{
    [Serializable]
    public struct InitialBuildPiece
    {
        public BuildPieceDefinition definition;
        public Vector2Int cell;
        public int rotation;
        public bool fixedGate;
    }

    [DefaultExecutionOrder(-200)]
    public sealed class ConstructionWorld : MonoBehaviour
    {
        [SerializeField] BuildGridDefinition grid;
        [SerializeField] Transform geometry;
        [SerializeField] NavMeshSurface surface;
        [SerializeField] InitialBuildPiece[] initialPieces;
        readonly Dictionary<int, PlacedStructure> structures = new Dictionary<int, PlacedStructure>();
        readonly Dictionary<int, PlacedSiegeWeapon> weapons = new Dictionary<int, PlacedSiegeWeapon>();
        Transform mountRoot;
        bool rebuildPending;
        public BuildLayout Layout { get; private set; }
        public MountLayout Mounts { get; private set; }
        public IReadOnlyCollection<PlacedSiegeWeapon> Weapons => weapons.Values;
        public event Action<PlacedSiegeWeapon, SiegeWeaponRemoval> WeaponRemoved;
        public BuildGridDefinition Grid => grid;
        public IReadOnlyCollection<PlacedStructure> Structures => structures.Values;
        public bool IsRebuilding { get; private set; }
        public bool RebuildPending => rebuildPending;
        public int NavigationRevision { get; private set; }
        public event Action<PlacedStructure> PieceCollapsing;
        public event Action Changed;

        public void Configure(BuildGridDefinition definition, Transform geometryRoot,
            NavMeshSurface navSurface, InitialBuildPiece[] seeds)
        {
            grid = definition; geometry = geometryRoot; surface = navSurface; initialPieces = seeds;
        }

        void Awake()
        {
            Layout = new BuildLayout(grid);
            Mounts = new MountLayout(Layout);
            // 수성 무기는 Geometry 밖에 둔다. 길찾기 수집 대상이 아니므로 설치해도 NavMesh가 바뀌지 않는다.
            mountRoot = new GameObject("Mounts").transform;
            mountRoot.SetParent(transform, false);
            if (initialPieces != null)
                foreach (InitialBuildPiece seed in initialPieces)
                {
                    if (Layout.TryPlace(seed.definition, seed.cell, seed.rotation, out var placement, seed.fixedGate))
                        CreateBody(placement);
                    else Debug.LogError("Invalid initial construction layout.", this);
                }
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            NavigationRevision = 1;
        }

        public Vector3 LocalPoint(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 WorldCenter(BuildPieceDefinition definition, Vector2Int cell, int rotation) =>
            transform.TransformPoint(Layout.Center(cell, BuildLayout.RotatedSize(definition.Footprint, rotation)));

        public void SetPose(Transform body, BuildPieceDefinition definition, Vector2Int cell, int rotation)
        {
            body.SetPositionAndRotation(WorldCenter(definition, cell, rotation),
                transform.rotation * Quaternion.Euler(0f, rotation * 90f, 0f));
            body.localScale = new Vector3(definition.Footprint.x * grid.CellSize,
                definition.Height, definition.Footprint.y * grid.CellSize);
        }

        public PlacementIssue CheckPlacement(BuildPieceDefinition definition, Vector2Int cell, int rotation)
        {
            PlacementIssue issue = Layout.Check(definition, cell, rotation);
            if (issue != PlacementIssue.None) return issue;
            Vector3 center = WorldCenter(definition, cell, rotation) + transform.up * (definition.Height * 0.5f);
            var half = new Vector3(definition.Footprint.x * grid.CellSize * 0.5f,
                definition.Height * 0.5f, definition.Footprint.y * grid.CellSize * 0.5f);
            foreach (Collider collider in Physics.OverlapBox(center, half * 0.98f,
                transform.rotation * Quaternion.Euler(0f, rotation * 90f, 0f), ~0, QueryTriggerInteraction.Ignore))
            {
                var actor = collider.GetComponentInParent<CharacterActor>();
                if (actor != null && actor.gameObject.scene == gameObject.scene
                    && actor.Health != null && !actor.Health.IsDead) return PlacementIssue.UnitInTheWay;
            }
            return PlacementIssue.None;
        }

        public bool TryPlace(BuildPieceDefinition definition, Vector2Int cell, int rotation, out PlacedStructure body)
        {
            body = null;
            if (CheckPlacement(definition, cell, rotation) != PlacementIssue.None) return false;
            if (!Layout.TryPlace(definition, cell, rotation, out var placement)) return false;
            body = CreateBody(placement);
            rebuildPending = true;
            Changed?.Invoke();
            return true;
        }

        PlacedStructure CreateBody(BuildPlacement placement)
        {
            GameObject body = Instantiate(placement.Definition.Prefab, geometry);
            body.name = placement.Definition.DisplayName + "_" + placement.Id;
            SetPose(body.transform, placement.Definition, placement.Cell, placement.Rotation);
            var structure = body.GetComponent<PlacedStructure>();
            structure.Initialize(this, placement);
            structures.Add(placement.Id, structure);
            return structure;
        }

        bool IsLive(PlacedStructure structure) => structure != null
            && structures.TryGetValue(structure.Placement.Id, out var known) && known == structure;

        public MountIssue CheckMount(SiegeWeaponDefinition definition, PlacedStructure support, string mountId) =>
            Mounts.Check(definition, IsLive(support) ? support.Placement.Id : 0, mountId);

        public void SetMountPose(Transform body, SiegeWeaponDefinition definition, Pose pose)
        {
            body.SetPositionAndRotation(pose.position, pose.rotation);
            body.localScale = definition.Size;
        }

        public bool TryMount(SiegeWeaponDefinition definition, PlacedStructure support, string mountId,
            out PlacedSiegeWeapon weapon)
        {
            weapon = null;
            if (!IsLive(support) || !Mounts.TryMount(definition, support.Placement.Id, mountId, out var mount)) return false;
            support.TryGetMountPose(mountId, out Pose pose);
            GameObject body = Instantiate(definition.Prefab, mountRoot);
            body.name = definition.DisplayName + "_" + mount.Id;
            SetMountPose(body.transform, definition, pose);
            weapon = body.GetComponent<PlacedSiegeWeapon>();
            weapon.Initialize(mount, support);
            weapons.Add(mount.Id, weapon);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveWeapon(PlacedSiegeWeapon weapon)
        {
            if (weapon == null || weapon.Mount == null || !weapons.TryGetValue(weapon.Mount.Id, out var known)
                || known != weapon) return false;
            Mounts.Remove(weapon.Mount.Id);
            Discard(weapon, SiegeWeaponRemoval.Dismantled);
            Changed?.Invoke();
            return true;
        }

        void Discard(PlacedSiegeWeapon weapon, SiegeWeaponRemoval reason)
        {
            weapons.Remove(weapon.Mount.Id);
            weapon.NotifyRemoved(reason);
            WeaponRemoved?.Invoke(weapon, reason);
            Destroy(weapon.gameObject);
        }

        public void Collapse(PlacedStructure structure)
        {
            if (structure == null || !structures.ContainsKey(structure.Placement.Id)) return;
            PieceCollapsing?.Invoke(structure);
            // 받치던 조각이 부서지면 그 위의 수성 무기도 함께 파괴된다.
            foreach (MountedWeapon mount in Mounts.RemoveSupportedBy(structure.Placement.Id))
                Discard(weapons[mount.Id], SiegeWeaponRemoval.SupportDestroyed);
            RemoveBody(structure);
        }

        /// <summary>
        /// 준비 중 철거. 무너뜨리지 않고 조각만 없앤다. 고정 성문, 무기가 올라간 조각,
        /// 유닛이 겹치거나 위에 서 있는 조각은 철거하지 않는다.
        /// </summary>
        public bool TryDismantle(PlacedStructure structure, float clearance = 2.5f)
        {
            if (!IsLive(structure) || structure.Placement.IsFixed) return false;
            BuildPlacement placement = structure.Placement;
            foreach (MountedWeapon mount in Mounts.Mounted)
                if (mount.SupportId == placement.Id) return false;
            float height = placement.Definition.Height + Mathf.Max(0f, clearance);
            Vector3 center = WorldCenter(placement.Definition, placement.Cell, placement.Rotation) + transform.up * (height * 0.5f);
            var half = new Vector3(placement.Definition.Footprint.x * grid.CellSize * 0.5f,
                height * 0.5f, placement.Definition.Footprint.y * grid.CellSize * 0.5f);
            foreach (Collider collider in Physics.OverlapBox(center, half * 0.98f,
                transform.rotation * Quaternion.Euler(0f, placement.Rotation * 90f, 0f), ~0, QueryTriggerInteraction.Ignore))
            {
                var actor = collider.GetComponentInParent<CharacterActor>();
                if (actor != null && actor.gameObject.scene == gameObject.scene
                    && actor.Health != null && !actor.Health.IsDead) return false;
            }
            RemoveBody(structure);
            return true;
        }

        void RemoveBody(PlacedStructure structure)
        {
            foreach (Collider collider in structure.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Layout.Remove(structure.Placement.Id, true);
            structures.Remove(structure.Placement.Id);
            Destroy(structure.gameObject);
            rebuildPending = true;
            Changed?.Invoke();
        }

        void Update()
        {
            if (rebuildPending && !IsRebuilding) StartCoroutine(Rebuild());
        }

        IEnumerator Rebuild()
        {
            IsRebuilding = true;
            rebuildPending = false;
            Physics.SyncTransforms();
            yield return surface.UpdateNavMesh(surface.navMeshData);
            NavigationRevision++;
            IsRebuilding = false;
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            if (surface != null && surface.navMeshData != null)
            {
                if (IsRebuilding) UnityEngine.AI.NavMeshBuilder.Cancel(surface.navMeshData);
                Destroy(surface.navMeshData);
            }
        }
    }
}
