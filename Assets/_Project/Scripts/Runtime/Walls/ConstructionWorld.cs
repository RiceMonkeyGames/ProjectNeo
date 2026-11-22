using System;
using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Characters;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

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
        readonly List<BuildPlacement> settleDropped = new List<BuildPlacement>();
        readonly List<BuildPlacement> settleUnsupported = new List<BuildPlacement>();
        Transform mountRoot;
        bool rebuildPending;
        public BuildLayout Layout { get; private set; }
        public MountLayout Mounts { get; private set; }
        public IReadOnlyCollection<PlacedSiegeWeapon> Weapons => weapons.Values;
        public event Action<PlacedSiegeWeapon, SiegeWeaponRemoval> WeaponRemoved;
        public BuildGridDefinition Grid => grid;
        /// <summary>런 시작 배치(기본 성). 성가퀴의 늘 안쪽 판정에 쓴다(BUG-battlement-inner-faces).</summary>
        public IReadOnlyList<InitialBuildPiece> InitialPieces => initialPieces ?? Array.Empty<InitialBuildPiece>();
        public IReadOnlyCollection<PlacedStructure> Structures => structures.Values;
        public bool IsRebuilding { get; private set; }
        public bool RebuildPending => rebuildPending;
        public int NavigationRevision { get; private set; }
        /// <summary>
        /// 준비 중 직접 바꾼 배치(놓기·철거·되살리기)마다 오른다. 전투 중 무너짐·성문 파괴로는 오르지 않는다(SIEGE-10 성 바깥 판정 기준).
        /// 전투 중 무너짐은 준비 단계에 들어설 때 <see cref="CastleBattlements.RecomputeOutside"/>로 반영한다.
        /// </summary>
        public int LayoutRevision { get; private set; }
        public event Action<PlacedStructure> PieceCollapsing;
        /// <summary>받침을 잃은 조각이 한 층 내려앉기 직전에 알린다. 아직 원래 자리에 있다.</summary>
        public event Action<PlacedStructure> PieceDropping;
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
            // 성가퀴는 첫 길찾기 메시에 함께 담는다. 따로 만들면 시작하자마자 메시를 한 번 더 만들게 된다(SIEGE-10).
            var battlements = GetComponent<CastleBattlements>();
            if (battlements != null) battlements.BuildBeforeNavigation(this);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            NavigationRevision = 1;
        }

        public Vector3 LocalPoint(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 WorldCenter(BuildPieceDefinition definition, Vector2Int cell, int rotation) =>
            transform.TransformPoint(Layout.Center(cell, BuildLayout.RotatedSize(definition.Footprint, rotation)));

        public void SetPose(Transform body, BuildPieceDefinition definition, Vector2Int cell, int rotation, float elevation = 0f)
        {
            body.SetPositionAndRotation(WorldCenter(definition, cell, rotation) + transform.up * elevation,
                transform.rotation * Quaternion.Euler(0f, rotation * 90f, 0f));
            body.localScale = new Vector3(definition.Footprint.x * grid.CellSize,
                definition.Height, definition.Footprint.y * grid.CellSize);
        }

        public PlacementIssue CheckPlacement(BuildPieceDefinition definition, Vector2Int cell, int rotation) =>
            CheckPlacement(definition, cell, rotation, out _, out _);

        /// <summary>놓일 층과 바닥 높이를 함께 돌려준다. 가리킨 칸에 조각이 있으면 그 위가 된다.</summary>
        public PlacementIssue CheckPlacement(BuildPieceDefinition definition, Vector2Int cell, int rotation,
            out int level, out float elevation)
        {
            PlacementIssue issue = Layout.Check(definition, cell, rotation, false, out level, out elevation);
            if (issue != PlacementIssue.None) return issue;
            if (level > 0)
            {
                // 수성 무기가 놓인 조각 위에는 쌓지 않는다.
                Vector2Int size = BuildLayout.RotatedSize(definition.Footprint, rotation);
                for (int x = 0; x < size.x; x++)
                    for (int z = 0; z < size.y; z++)
                        if (Mounts.HasWeaponOn(Layout.At(cell + new Vector2Int(x, z), level - 1).Id)) return PlacementIssue.Occupied;
                // 짓는 중인 조각 위에는 쌓지 않는다(SIEGE-08).
                for (int x = 0; x < size.x; x++)
                    for (int z = 0; z < size.y; z++)
                        if (structures.TryGetValue(Layout.At(cell + new Vector2Int(x, z), level - 1).Id, out PlacedStructure below)
                            && below != null && below.IsUnderConstruction) return PlacementIssue.UnfinishedSupport;
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
            LayoutRevision++;
            LiftOverlappingUnits(body);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 조각과 수성 무기를 모두 없애 빈 격자로 만든다(FLOW-02 저장된 성 되살리기). 무너짐·파괴 알림은 보내지 않고
        /// 고정 성문도 없앤다. 이어서 <see cref="TryPlaceRestored"/>로 기록대로 다시 놓는다.
        /// </summary>
        public void ClearForRestore()
        {
            foreach (PlacedSiegeWeapon weapon in new List<PlacedSiegeWeapon>(weapons.Values))
            {
                Mounts.Remove(weapon.Mount.Id);
                Discard(weapon, SiegeWeaponRemoval.Dismantled);
            }
            foreach (PlacedStructure body in new List<PlacedStructure>(structures.Values)) RemoveBody(body);
            // 격자는 새로 만든다. 쓰던 격자는 고정 성문을 이미 등록했다고 기억해 성문을 다시 놓지 못한다.
            Layout = new BuildLayout(grid);
            Mounts = new MountLayout(Layout);
            rebuildPending = true;
            LayoutRevision++;
            Changed?.Invoke();
        }

        /// <summary>
        /// 저장된 조각 하나를 놓는다(FLOW-02). 아래층부터 순서대로 불러야 한다. 고정 성문도 놓고, 유닛을 들어 올리지 않는다.
        /// </summary>
        public bool TryPlaceRestored(BuildPieceDefinition definition, Vector2Int cell, int rotation, bool fixedGate,
            out PlacedStructure body)
        {
            body = null;
            if (definition == null || !Layout.TryPlace(definition, cell, rotation, out var placement, fixedGate)) return false;
            body = CreateBody(placement);
            rebuildPending = true;
            LayoutRevision++;
            Changed?.Invoke();
            return true;
        }

        // 미리보기는 유닛을 움직이지 않고, 확정한 실제 조각과 겹친 살아 있는 몸통만 옮긴다.
        void LiftOverlappingUnits(PlacedStructure structure)
        {
            Physics.SyncTransforms();
            BuildPlacement placement = structure.Placement;
            Transform body = structure.transform;
            Vector3 center = body.TransformPoint(Vector3.up * 0.5f);
            Vector3 half = body.lossyScale * 0.5f;
            var lifted = new HashSet<CharacterActor>();
            foreach (Collider collider in Physics.OverlapBox(center, half, body.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                var actor = collider.GetComponentInParent<CharacterActor>();
                if (actor == null || actor.gameObject.scene != gameObject.scene || actor.Health == null
                    || actor.Health.IsDead || !lifted.Add(actor)) continue;
                Vector3 local = body.InverseTransformPoint(actor.transform.position);
                float marginX = Mathf.Min(0.49f, actor.Profile.Radius / body.lossyScale.x);
                float marginZ = Mathf.Min(0.49f, actor.Profile.Radius / body.lossyScale.z);
                local.x = Mathf.Clamp(local.x, -0.5f + marginX, 0.5f - marginX);
                local.z = Mathf.Clamp(local.z, -0.5f + marginZ, 0.5f - marginZ);
                local.y = 1f;
                Vector3 top = body.TransformPoint(local);
                // 계단은 최고 높이가 아니라 해당 위치의 실제 발판 위에 놓는다.
                float nearest = float.PositiveInfinity;
                foreach (RaycastHit hit in Physics.RaycastAll(top + transform.up, -transform.up,
                    placement.Definition.Height + 1f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<PlacedStructure>() == structure && hit.distance < nearest)
                    { top = hit.point; nearest = hit.distance; }
                var fall = actor.GetComponent<UnitFallController>();
                if (fall != null) fall.LiftForConstruction(top);
                else StartCoroutine(LiftWithoutFallController(actor, top));
            }
            Physics.SyncTransforms();
        }

        IEnumerator LiftWithoutFallController(CharacterActor actor, Vector3 position)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            var motor = actor.GetComponent<CharacterController>();
            var input = actor.GetComponent<PlayerInputController>();
            bool resumeAgent = agent != null && agent.enabled;
            bool resumeMotor = motor != null && motor.enabled;
            if (resumeAgent) agent.enabled = false;
            if (resumeMotor) motor.enabled = false;
            if (input != null) input.SetMovementSuspended(true);
            actor.transform.position = position;
            while (IsRebuilding || rebuildPending) yield return null;
            if (actor == null || actor.Health.IsDead) yield break;
            if (resumeAgent) agent.enabled = true;
            if (resumeMotor) motor.enabled = true;
            if (input != null) input.SetMovementSuspended(false);
        }

        PlacedStructure CreateBody(BuildPlacement placement)
        {
            GameObject body = Instantiate(placement.Definition.Prefab, geometry);
            body.name = placement.Definition.DisplayName + "_" + placement.Id;
            SetPose(body.transform, placement.Definition, placement.Cell, placement.Rotation, placement.Elevation);
            var structure = body.GetComponent<PlacedStructure>();
            structure.Initialize(this, placement);
            structures.Add(placement.Id, structure);
            return structure;
        }

        bool IsLive(PlacedStructure structure) => structure != null
            && structures.TryGetValue(structure.Placement.Id, out var known) && known == structure;

        public MountIssue CheckMount(SiegeWeaponDefinition definition, PlacedStructure support, string mountId)
        {
            MountIssue issue = Mounts.Check(definition, IsLive(support) ? support.Placement.Id : 0, mountId);
            // 짓는 중인 조각에는 무기를 올리지 않는다(SIEGE-08).
            if (issue == MountIssue.None && support.IsUnderConstruction) return MountIssue.UnfinishedSupport;
            return issue;
        }

        public void SetMountPose(Transform body, SiegeWeaponDefinition definition, Pose pose)
        {
            body.SetPositionAndRotation(pose.position, pose.rotation);
            body.localScale = definition.Size;
        }

        /// <summary>설치 지점 자세를 90도 단위로 더 돌린다. 무기의 정면(사격 기준 방향)이 된다.</summary>
        public static Pose RotateMountPose(Pose pose, int rotation) =>
            new Pose(pose.position, pose.rotation * Quaternion.Euler(0f, MountedWeapon.NormalizeRotation(rotation) * 90f, 0f));

        public bool TryMount(SiegeWeaponDefinition definition, PlacedStructure support, string mountId,
            out PlacedSiegeWeapon weapon, int rotation = 0)
        {
            weapon = null;
            if (!IsLive(support) || support.IsUnderConstruction
                || !Mounts.TryMount(definition, support.Placement.Id, mountId, out var mount, rotation))
                return false;
            support.TryGetMountPose(mountId, out Pose pose);
            GameObject body = Instantiate(definition.Prefab, mountRoot);
            body.name = definition.DisplayName + "_" + mount.Id;
            SetMountPose(body.transform, definition, RotateMountPose(pose, mount.Rotation));
            weapon = body.GetComponent<PlacedSiegeWeapon>();
            weapon.Initialize(mount, support);
            weapon.HealthDepleted += DestroyWeapon;
            weapons.Add(mount.Id, weapon);
            Changed?.Invoke();
            return true;
        }

        /// <summary>체력이 다한 무기를 파괴한다. 받침 성벽은 그대로 남는다.</summary>
        void DestroyWeapon(PlacedSiegeWeapon weapon)
        {
            if (weapon == null || weapon.Mount == null || !weapons.TryGetValue(weapon.Mount.Id, out var known)
                || known != weapon) return;
            Mounts.Remove(weapon.Mount.Id);
            Discard(weapon, SiegeWeaponRemoval.Destroyed);
            Changed?.Invoke();
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
            weapon.HealthDepleted -= DestroyWeapon;
            weapon.NotifyRemoved(reason);
            WeaponRemoved?.Invoke(weapon, reason);
            Destroy(weapon.gameObject);
        }

        public void BreachGate(PlacedStructure structure)
        {
            if (!IsLive(structure)) return;
            structure.ApplyDoorState(true);
            PieceCollapsing?.Invoke(structure);
            rebuildPending = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// 살아 있는 성문을 열어 두거나 닫고 길을 다시 만든다(PREP-05 준비 단계). 성문이 아니거나 문짝이 없거나 상태가 같으면 false.
        /// 부서짐 알림(PieceCollapsing)은 보내지 않는다.
        /// </summary>
        public bool SetGateHeldOpen(PlacedStructure structure, bool open)
        {
            if (!IsLive(structure) || structure.DoorCollider == null
                || structure.Placement.Definition.Kind != BuildPieceKind.Gate || structure.IsHeldOpen == open) return false;
            structure.SetHeldOpen(open);
            rebuildPending = true;
            Changed?.Invoke();
            return true;
        }

        public void NotifyGateRepaired(PlacedStructure structure)
        {
            if (!IsLive(structure)) return;
            structure.ApplyDoorState(false);
            rebuildPending = true;
            Changed?.Invoke();
        }

        public void Collapse(PlacedStructure structure)
        {
            if (structure == null || !structures.ContainsKey(structure.Placement.Id)) return;
            DestroyBody(structure);
            Layout.Remove(structure.Placement.Id, true);
            // 위에 쌓인 조각은 한 층 내려앉는다. 한쪽만 받쳐져 내려앉을 수 없는 조각은 함께 부서진다.
            settleDropped.Clear(); settleUnsupported.Clear();
            Layout.Settle(settleDropped, settleUnsupported);
            foreach (BuildPlacement lost in settleUnsupported) DestroyBody(structures[lost.Id]);
            foreach (BuildPlacement moved in settleDropped)
            {
                PlacedStructure body = structures[moved.Id];
                PieceDropping?.Invoke(body);
                SetPose(body.transform, moved.Definition, moved.Cell, moved.Rotation, moved.Elevation);
                foreach (PlacedSiegeWeapon weapon in weapons.Values)
                    if (weapon.Support == body && body.TryGetMountPose(weapon.Mount.MountId, out Pose pose))
                        SetMountPose(weapon.transform, weapon.Definition, RotateMountPose(pose, weapon.Mount.Rotation));
            }
            rebuildPending = true;
            Changed?.Invoke();
        }

        // 몸통과 그 위의 수성 무기를 없앤다. 배치 모델에서 빼는 것은 부르는 쪽이 한다.
        void DestroyBody(PlacedStructure structure)
        {
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
            if (Layout.IsCovered(placement)) return false;
            foreach (MountedWeapon mount in Mounts.Mounted)
                if (mount.SupportId == placement.Id) return false;
            float height = placement.Definition.Height + Mathf.Max(0f, clearance);
            Vector3 center = WorldCenter(placement.Definition, placement.Cell, placement.Rotation)
                + transform.up * (placement.Elevation + height * 0.5f);
            var half = new Vector3(placement.Definition.Footprint.x * grid.CellSize * 0.5f,
                height * 0.5f, placement.Definition.Footprint.y * grid.CellSize * 0.5f);
            foreach (Collider collider in Physics.OverlapBox(center, half * 0.98f,
                transform.rotation * Quaternion.Euler(0f, placement.Rotation * 90f, 0f), ~0, QueryTriggerInteraction.Ignore))
            {
                var actor = collider.GetComponentInParent<CharacterActor>();
                if (actor != null && actor.gameObject.scene == gameObject.scene
                    && actor.Health != null && !actor.Health.IsDead) return false;
            }
            Layout.Remove(placement.Id);
            RemoveBody(structure);
            rebuildPending = true;
            LayoutRevision++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 작업이 전혀 되지 않은 채 전투를 맞은 조각을 없앤다(SIEGE-08). 무너짐 알림 없이 배치에서 빼며 유닛 겹침은 보지 않는다.
        /// 짓는 중인 조각 위에는 아무것도 올릴 수 없으므로 위 조각·무기를 따로 다루지 않는다.
        /// </summary>
        public bool RemoveUnbuilt(PlacedStructure structure)
        {
            if (!IsLive(structure) || structure.Placement.IsFixed || !structure.IsUnderConstruction) return false;
            if (Layout.IsCovered(structure.Placement)) return false;
            foreach (MountedWeapon mount in Mounts.Mounted)
                if (mount.SupportId == structure.Placement.Id) return false;
            Layout.Remove(structure.Placement.Id);
            RemoveBody(structure);
            rebuildPending = true;
            LayoutRevision++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>조각 배치 밖의 이유(성가퀴 모양 바뀜 등)로 길찾기 메시를 다시 만들게 한다.</summary>
        public void RequestNavigationRebuild() => rebuildPending = true;

        void RemoveBody(PlacedStructure structure)
        {
            foreach (Collider collider in structure.GetComponentsInChildren<Collider>()) collider.enabled = false;
            structures.Remove(structure.Placement.Id);
            Destroy(structure.gameObject);
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
