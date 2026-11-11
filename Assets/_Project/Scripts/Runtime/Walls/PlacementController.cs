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
        CursorLockMode previousLock;
        bool previousVisible;
        public BuildPieceDefinition Selected { get; private set; }
        public int Rotation { get; private set; }
        public Vector2Int PreviewCell { get; private set; }
        public PlacementIssue Issue { get; private set; } = PlacementIssue.NoPiece;
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
            if (definition == null) return;
            ghost = Instantiate(definition.Prefab, world.transform);
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
        }

        public void Rotate() => Rotation = (Rotation + 1) % 4;

        public void UpdatePreview(Vector3 worldPosition)
        {
            if (Selected == null || ghost == null) return;
            PreviewCell = world.Layout.WorldToCell(world.LocalPoint(worldPosition));
            Issue = world.CheckPlacement(Selected, PreviewCell, Rotation);
            world.SetPose(ghost.transform, Selected, PreviewCell, Rotation);
            properties.SetColor("_BaseColor", Issue == PlacementIssue.None ? settings.ValidColor : settings.InvalidColor);
            foreach (Renderer renderer in ghostRenderers) renderer.SetPropertyBlock(properties);
            ghost.SetActive(true);
        }

        public bool Confirm()
        {
            if (!IsPreviewVisible || Selected == null) return false;
            bool placed = world.TryPlace(Selected, PreviewCell, Rotation, out _);
            Issue = world.CheckPlacement(Selected, PreviewCell, Rotation);
            properties.SetColor("_BaseColor", Issue == PlacementIssue.None ? settings.ValidColor : settings.InvalidColor);
            foreach (Renderer renderer in ghostRenderers) renderer.SetPropertyBlock(properties);
            return placed;
        }

        bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        void Update()
        {
            if (Mouse.current == null) return;
            if (Mouse.current.rightButton.wasPressedThisFrame) Cancel();
            if (Selected == null) return;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Rotate();
            Vector2 pointer = Mouse.current.position.ReadValue();
            var plane = new Plane(world.transform.up, world.transform.position);
            Ray ray = view.ScreenPointToRay(pointer);
            if (OverUI(pointer) || !plane.Raycast(ray, out float distance))
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }
            UpdatePreview(ray.GetPoint(distance));
            if (Mouse.current.leftButton.wasPressedThisFrame) Confirm();
        }

        void OnDestroy() { if (ghost != null) Destroy(ghost); }
    }
}
