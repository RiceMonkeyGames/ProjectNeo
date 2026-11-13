using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace ProjectNeo.Walls
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class ConstructionCameraController : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] BuildCameraSettings settings;
        float targetYaw;
        float renderedYaw;
        float startYaw;
        float elapsed;
        float duration;
        Camera view;
        Vector2 focus;
        float zoom;
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        public float RenderedYaw => Mathf.Repeat(renderedYaw, 360f);
        public bool IsTransitioning { get; private set; }
        public Vector2 Focus => focus;
        public float ZoomSize => zoom;
        public float MinimumGridLinePixels => settings.MinimumGridLinePixels;
        public void Configure(ConstructionWorld owner, BuildCameraSettings cameraSettings)
        { world = owner; settings = cameraSettings; }
        void Awake()
        {
            view = GetComponent<Camera>();
            targetYaw = renderedYaw = settings.InitialYaw;
            ResetNavigation();
        }
        public void ResetNavigation()
        {
            focus = new Vector2(world.Grid.Width * world.Grid.CellSize * settings.InitialFocus.x,
                world.Grid.Depth * world.Grid.CellSize * settings.InitialFocus.y);
            zoom = settings.OrthographicSize;
            Apply();
        }
        public void Pan(Vector2 localDelta)
        {
            if (!settings.NavigationEnabled) return;
            focus.x = Mathf.Clamp(focus.x + localDelta.x, 0f, world.Grid.Width * world.Grid.CellSize);
            focus.y = Mathf.Clamp(focus.y + localDelta.y, 0f, world.Grid.Depth * world.Grid.CellSize);
            Apply();
        }
        public void Zoom(float steps)
        {
            if (!settings.NavigationEnabled) return;
            zoom = Mathf.Clamp(zoom * Mathf.Pow(1f - settings.ZoomFraction, steps), settings.MinZoom, settings.MaxZoom);
            Apply();
        }
        // 화면 속 판이 도는 방향을 기준으로 한다. 카메라의 공전 방향과 반대다.
        public void RotateLeft() => Rotate(settings.RotationStep);
        public void RotateRight() => Rotate(-settings.RotationStep);
        void Rotate(float amount)
        {
            startYaw = renderedYaw;
            targetYaw += amount;
            elapsed = 0f;
            duration = settings.TransitionDuration * Mathf.Abs(targetYaw - startYaw) / settings.RotationStep;
            IsTransitioning = duration > 0f;
            if (!IsTransitioning)
            {
                renderedYaw = targetYaw;
                Apply();
            }
        }
        void Apply()
        {
            view.orthographic = true;
            view.orthographicSize = zoom;
            Quaternion rotation = Quaternion.Euler(settings.Pitch, renderedYaw, 0f);
            Vector3 center = world.transform.TransformPoint(new Vector3(focus.x, 0f, focus.y));
            transform.SetPositionAndRotation(center - rotation * Vector3.forward * settings.Distance, rotation);
        }
        void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) RotateLeft();
                if (Keyboard.current.eKey.wasPressedThisFrame) RotateRight();
                if (settings.NavigationEnabled && Keyboard.current.homeKey.wasPressedThisFrame) ResetNavigation();
            }
            if (settings.NavigationEnabled && Mouse.current != null
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                // 정규화된 휠 입력은 이미 한 눈금이 1이다. Windows 원시 입력만 120으로 나눈다.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                    scroll /= 120f;
#endif
                if (scroll != 0f) Zoom(scroll);
                if (Mouse.current.middleButton.isPressed)
                {
                    Vector2 drag = Mouse.current.delta.ReadValue();
                    Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                    Vector3 up = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
                    Vector3 delta = (-right * drag.x - up * drag.y)
                        * (zoom * 2f / Mathf.Max(1, view.pixelHeight) * settings.DragSensitivity);
                    Vector3 local = world.transform.InverseTransformDirection(delta);
                    Pan(new Vector2(local.x, local.z));
                }
            }
            if (!IsTransitioning) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            renderedYaw = Mathf.Lerp(startYaw, targetYaw, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) IsTransitioning = false;
            Apply();
        }
    }
}
