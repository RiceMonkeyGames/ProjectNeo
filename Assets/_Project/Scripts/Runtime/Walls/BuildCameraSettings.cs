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
        public float Pitch => pitch;
        public float InitialYaw => initialYaw;
        public float OrthographicSize => orthographicSize;
        public float Distance => distance;
        public float RotationStep => rotationStep;
        public float TransitionDuration => transitionDuration;
        public static BuildCameraSettings Create() => CreateInstance<BuildCameraSettings>();
    }
}
