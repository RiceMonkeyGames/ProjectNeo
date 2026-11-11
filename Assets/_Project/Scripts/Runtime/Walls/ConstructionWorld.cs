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
        bool rebuildPending;
        public BuildLayout Layout { get; private set; }
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

        public void Collapse(PlacedStructure structure)
        {
            if (structure == null || !structures.ContainsKey(structure.Placement.Id)) return;
            PieceCollapsing?.Invoke(structure);
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
