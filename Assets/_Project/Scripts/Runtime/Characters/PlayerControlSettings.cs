using UnityEngine;

namespace ProjectNeo.Characters
{
    [CreateAssetMenu(menuName = "ProjectNeo/Player Control Settings")]
    public sealed class PlayerControlSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] float mouseSensitivity = 0.12f;
        [SerializeField, Min(0.1f)] float cameraDistance = 5f;
        [SerializeField, Min(0f)] float cameraHeight = 1.6f;
        [SerializeField] float minimumPitch = -25f;
        [SerializeField] float maximumPitch = 65f;
        [SerializeField] float initialPitch = 18f;
        [SerializeField, Min(0.01f)] float cameraCollisionRadius = 0.2f;
        [SerializeField, Min(0f)] float gravity = 25f;
        public float MouseSensitivity => mouseSensitivity;
        public float CameraDistance => cameraDistance;
        public float CameraHeight => cameraHeight;
        public float MinimumPitch => minimumPitch;
        public float MaximumPitch => maximumPitch;
        public float InitialPitch => initialPitch;
        public float CameraCollisionRadius => cameraCollisionRadius;
        public float Gravity => gravity;

        public static PlayerControlSettings Create() => CreateInstance<PlayerControlSettings>();
    }
}
