using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectNeo.Walls
{
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] Camera view;
        [SerializeField] PlacementViewSettings settings;
        GameObject ghost;
        Renderer[] ghostRenderers;
        MaterialPropertyBlock properties;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly RaycastHit[] rayHits = new RaycastHit[32];
        CursorLockMode previousLock;
        bool previousVisible;
        public BuildPieceDefinition Selected { get; private set; }
        public int Rotation { get; private set; }
        public Vector2Int PreviewCell { get; private set; }
        /// <summary>미리보기가 놓일 층과 바닥 높이. 가리킨 칸에 조각이 있으면 그 위다.</summary>
        public int PreviewLevel { get; private set; }
        public float PreviewElevation { get; private set; }
        public PlacementIssue Issue { get; private set; } = PlacementIssue.NoPiece;
        // 수성 무기 배치. 바닥 칸이 아니라 조각의 설치 지점을 고른다.
        public SiegeWeaponDefinition SelectedWeapon { get; private set; }
        public PlacedStructure PreviewSupport { get; private set; }
        public string PreviewMountId { get; private set; }
        public MountIssue MountIssue { get; private set; } = MountIssue.NoWeapon;
        // 설치 직전에 묻는 조건(코스트 등)과 설치 결과 알림. 비어 있으면 기존처럼 항상 설치한다.
        public System.Func<bool> ConfirmGate;
        public event System.Action<PlacedStructure> PiecePlaced;
        public event System.Action<PlacedSiegeWeapon> WeaponMounted;
        public bool HasSelection => Selected != null || SelectedWeapon != null;
        public ConstructionWorld World => world;
        public bool IsPreviewVisible => ghost != null && ghost.activeSelf;
        public bool StairNeedsConnection => Selected != null && Selected.Kind == BuildPieceKind.Stairs
            && !world.Layout.HasLanding(new BuildPlacement(0, Selected, PreviewCell, Rotation, false, PreviewLevel, PreviewElevation));

        public void Configure(ConstructionWorld owner, Camera cameraView, PlacementViewSettings viewSettings)
        { world = owner; view = cameraView; settings = viewSettings; }

        void Awake() => properties = new MaterialPropertyBlock();
        void OnEnable()
        {
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnDisable()
        {
            if (ghost != null) ghost.SetActive(false);
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        public void Select(BuildPieceDefinition definition)
        {
            Cancel();
            Selected = definition;
            Rotation = 0;
            if (definition != null) CreateGhost(definition.Prefab);
        }

        public void SelectWeapon(SiegeWeaponDefinition definition)
        {
            Cancel();
            SelectedWeapon = definition;
            Rotation = 0;
            if (definition != null) CreateGhost(definition.Prefab);
        }

        void CreateGhost(GameObject prefab)
        {
            ghost = Instantiate(prefab, world.transform);
            ghost.name = "PlacementPreview";
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in ghostRenderers)
            {
                renderer.sharedMaterial = settings.GhostMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            ghost.SetActive(false);
        }

        public void Cancel()
        {
            if (ghost != null) { ghost.SetActive(false); Destroy(ghost); }
            ghost = null; Selected = null; Issue = PlacementIssue.NoPiece;
            SelectedWeapon = null; PreviewSupport = null; PreviewMountId = null; MountIssue = MountIssue.NoWeapon;
        }

        public void Rotate() => Rotation = (Rotation + 1) % 4;

        void Tint(bool valid)
        {
            properties.SetColor("_BaseColor", valid ? settings.ValidColor : settings.InvalidColor);
            foreach (Renderer renderer in ghostRenderers) renderer.SetPropertyBlock(properties);
        }

        public void UpdatePreview(Vector3 worldPosition)
        {
            if (Selected == null || ghost == null) return;
            PreviewCell = world.Layout.WorldToCell(world.LocalPoint(worldPosition));
            Issue = world.CheckPlacement(Selected, PreviewCell, Rotation, out int level, out float elevation);
            PreviewLevel = level; PreviewElevation = elevation;
            world.SetPose(ghost.transform, Selected, PreviewCell, Rotation, elevation);
            Tint(Issue == PlacementIssue.None);
            ghost.SetActive(true);
        }

        /// <summary>가리킨 조각에서 가장 가까운 설치 지점에 미리보기를 둔다. 조각이 없으면 가리킨 곳에 설치 불가로 표시한다.</summary>
        public void UpdateWeaponPreview(PlacedStructure support, Vector3 worldPosition)
        {
            if (SelectedWeapon == null || ghost == null) return;
            PreviewSupport = support;
            if (support == null || !support.TryGetNearestMount(worldPosition, out string mountId, out Pose pose))
            {
                mountId = null;
                pose = new Pose(worldPosition, world.transform.rotation);
            }
            PreviewMountId = mountId;
            MountIssue = world.CheckMount(SelectedWeapon, support, mountId);
            world.SetMountPose(ghost.transform, SelectedWeapon, ConstructionWorld.RotateMountPose(pose, Rotation));
            Tint(MountIssue == MountIssue.None);
            ghost.SetActive(true);
        }

        public bool Confirm()
        {
            if (!IsPreviewVisible) return false;
            if (ConfirmGate != null && !ConfirmGate()) return false;
            if (SelectedWeapon != null)
            {
                bool mounted = world.TryMount(SelectedWeapon, PreviewSupport, PreviewMountId, out PlacedSiegeWeapon weapon,
                    Rotation);
                if (mounted) WeaponMounted?.Invoke(weapon);
                MountIssue = world.CheckMount(SelectedWeapon, PreviewSupport, PreviewMountId);
                Tint(MountIssue == MountIssue.None);
                return mounted;
            }
            if (Selected == null) return false;
            bool placed = world.TryPlace(Selected, PreviewCell, Rotation, out PlacedStructure body);
            if (placed) PiecePlaced?.Invoke(body);
            Issue = world.CheckPlacement(Selected, PreviewCell, Rotation);
            Tint(Issue == PlacementIssue.None);
            return placed;
        }

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        // 화면에서 가리킨 가장 가까운 조각. 유닛이나 다른 물체가 앞에 있어도 조각을 고른다.
        PlacedStructure StructureUnder(Ray ray, out Vector3 point)
        {
            PlacedStructure found = null;
            float nearest = float.PositiveInfinity;
            point = default;
            int count = Physics.RaycastNonAlloc(ray, rayHits, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].distance >= nearest) continue;
                var candidate = rayHits[i].collider.GetComponentInParent<PlacedStructure>();
                if (candidate == null) continue;
                found = candidate; nearest = rayHits[i].distance; point = rayHits[i].point;
            }
            return found;
        }

        void Update()
        {
            if (Mouse.current == null) return;
            if (Mouse.current.rightButton.wasPressedThisFrame) Cancel();
            if (!HasSelection) return;
            // 조각과 수성 무기 모두 R로 90도씩 돌린다. 무기는 이 방향이 사격 기준 정면이다(Q-21).
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Rotate();
            Vector2 pointer = Mouse.current.position.ReadValue();
            var plane = new Plane(world.transform.up, world.transform.position);
            Ray ray = view.ScreenPointToRay(pointer);
            if (OverUI(pointer) || !plane.Raycast(ray, out float distance))
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }
            if (SelectedWeapon != null)
            {
                PlacedStructure support = StructureUnder(ray, out Vector3 point);
                UpdateWeaponPreview(support, support != null ? point : ray.GetPoint(distance));
            }
            else
            {
                // 조각을 가리키면 그 조각이 선 칸을 고른다. 살짝 안쪽 점을 써서 여러 칸짜리 조각도 가리킨 칸이 잡힌다.
                PlacedStructure pointed = StructureUnder(ray, out Vector3 point);
                UpdatePreview(pointed != null ? point + ray.direction * 0.05f : ray.GetPoint(distance));
            }
            if (Mouse.current.leftButton.wasPressedThisFrame) Confirm();
        }

        void OnDestroy() { if (ghost != null) Destroy(ghost); }
    }
}
