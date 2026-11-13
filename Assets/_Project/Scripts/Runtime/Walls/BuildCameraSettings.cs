using UnityEngine;

namespace ProjectNeo.Walls
{
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Camera Settings")]
    public sealed class BuildCameraSettings : ScriptableObject
    {
        [SerializeField, Range(5f, 85f)] float pitch = 30f;
        [SerializeField] float initialYaw = 45f;
        [SerializeField, Min(0.1f)] float orthographicSize = 20f;
        [SerializeField, Min(1f)] float distance = 45f;
        [SerializeField, Range(1f, 180f)] float rotationStep = 90f;
        [SerializeField, Min(0f)] float transitionDuration = 0.3f;
        [Header("넓은 맵 탐색 (게임 씬에서 사용)")]
        [SerializeField] bool navigationEnabled;
        [SerializeField] Vector2 initialFocus = new Vector2(0.5f, 0.5f);
        [SerializeField, Min(0.01f)] float dragSensitivity = 1f;
        [SerializeField, Range(0.01f, 0.5f)] float zoomFraction = 0.15f;
        [SerializeField, Min(1f)] float minZoom = 20f;
        [SerializeField, Min(1f)] float maxZoom = 140f;
        [SerializeField, Min(1f)] float minimumGridLinePixels = 1.5f;
        public float Pitch => pitch;
        public float InitialYaw => initialYaw;
        public float OrthographicSize => orthographicSize;
        public float Distance => distance;
        public float RotationStep => rotationStep;
        public float TransitionDuration => transitionDuration;
        public bool NavigationEnabled => navigationEnabled;
        public Vector2 InitialFocus => initialFocus;
        public float DragSensitivity => dragSensitivity;
        public float ZoomFraction => zoomFraction;
        public float MinZoom => minZoom;
        public float MaxZoom => Mathf.Max(minZoom, maxZoom);
        public float MinimumGridLinePixels => minimumGridLinePixels;
        public static BuildCameraSettings Create() => CreateInstance<BuildCameraSettings>();
    }
}
