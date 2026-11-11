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
        public PlacementIssue Issue { get; private set; } = PlacementIssue.NoPiece;
        // 수성 무기 배치. 바닥 칸이 아니라 조각의 설치 지점을 고른다.
        public SiegeWeaponDefinition SelectedWeapon { get; private set; }
        public PlacedStructure PreviewSupport { get; private set; }
        public string PreviewMountId { get; private set; }
        public MountIssue MountIssue { get; private set; } = MountIssue.NoWeapon;
        public bool HasSelection => Selected != null || SelectedWeapon != null;
        public bool IsPreviewVisible => ghost != null && ghost.activeSelf;
        public bool StairNeedsConnection => Selected != null && Selected.Kind == BuildPieceKind.Stairs
            && !world.Layout.HasLanding(new BuildPlacement(0, Selected, PreviewCell, Rotation, false));

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
            Issue = world.CheckPlacement(Selected, PreviewCell, Rotation);
            world.SetPose(ghost.transform, Selected, PreviewCell, Rotation);
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
            world.SetMountPose(ghost.transform, SelectedWeapon, pose);
            Tint(MountIssue == MountIssue.None);
            ghost.SetActive(true);
        }

        public bool Confirm()
        {
            if (!IsPreviewVisible) return false;
            if (SelectedWeapon != null)
            {
                bool mounted = world.TryMount(SelectedWeapon, PreviewSupport, PreviewMountId, out _);
                MountIssue = world.CheckMount(SelectedWeapon, PreviewSupport, PreviewMountId);
                Tint(MountIssue == MountIssue.None);
                return mounted;
            }
            if (Selected == null) return false;
            bool placed = world.TryPlace(Selected, PreviewCell, Rotation, out _);
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
            if (Selected != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Rotate();
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
            else UpdatePreview(ray.GetPoint(distance));
            if (Mouse.current.leftButton.wasPressedThisFrame) Confirm();
        }

        void OnDestroy() { if (ghost != null) Destroy(ghost); }
    }
}
