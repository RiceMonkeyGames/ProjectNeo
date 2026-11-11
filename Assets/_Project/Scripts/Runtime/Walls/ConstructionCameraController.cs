using UnityEngine;
using UnityEngine.InputSystem;

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
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        public float RenderedYaw => Mathf.Repeat(renderedYaw, 360f);
        public bool IsTransitioning { get; private set; }
        public void Configure(ConstructionWorld owner, BuildCameraSettings cameraSettings)
        { world = owner; settings = cameraSettings; }
        void Awake()
        {
            view = GetComponent<Camera>();
            targetYaw = renderedYaw = settings.InitialYaw;
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
            view.orthographicSize = settings.OrthographicSize;
            Quaternion rotation = Quaternion.Euler(settings.Pitch, renderedYaw, 0f);
            Vector3 center = world.transform.TransformPoint(new Vector3(
                world.Grid.Width * world.Grid.CellSize * 0.5f, 0f,
                world.Grid.Depth * world.Grid.CellSize * 0.5f));
            transform.SetPositionAndRotation(center - rotation * Vector3.forward * settings.Distance, rotation);
        }
        void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) RotateLeft();
                if (Keyboard.current.eKey.wasPressedThisFrame) RotateRight();
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
