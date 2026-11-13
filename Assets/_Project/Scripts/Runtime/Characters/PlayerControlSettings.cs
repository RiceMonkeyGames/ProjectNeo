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
        // One-cell stairs rise 3m over 3m. Keep a degree of tolerance at the 45-degree boundary.
        [SerializeField, Range(0f, 90f)] float maximumWalkSlope = 46f;
        [SerializeField, Range(0.1f, 1f)] float crouchHeightRatio = 0.6f;
        [SerializeField, Range(0f, 1f)] float crouchSpeedMultiplier = 0.5f;
        [SerializeField, Min(1f)] float sprintSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] float crouchViewTransitionSeconds = 0.18f;
        public float MouseSensitivity => mouseSensitivity;
        public float CameraDistance => cameraDistance;
        public float CameraHeight => cameraHeight;
        public float MinimumPitch => minimumPitch;
        public float MaximumPitch => maximumPitch;
        public float InitialPitch => initialPitch;
        public float CameraCollisionRadius => cameraCollisionRadius;
        public float Gravity => gravity;
        public float MaximumWalkSlope => maximumWalkSlope;
        public float CrouchHeightRatio => crouchHeightRatio;
        public float CrouchSpeedMultiplier => crouchSpeedMultiplier;
        public float SprintSpeedMultiplier => sprintSpeedMultiplier;
        public float CrouchViewTransitionSeconds => crouchViewTransitionSeconds;

        public static PlayerControlSettings Create(float maximumWalkSlope = 46f,
            float crouchHeightRatio = 0.6f, float crouchSpeedMultiplier = 0.5f, float sprintSpeedMultiplier = 1.5f,
            float crouchViewTransitionSeconds = 0.18f)
        {
            var value = CreateInstance<PlayerControlSettings>();
            value.maximumWalkSlope = Mathf.Clamp(maximumWalkSlope, 0f, 90f);
            value.crouchHeightRatio = Mathf.Clamp(crouchHeightRatio, 0.1f, 1f);
            value.crouchSpeedMultiplier = Mathf.Clamp01(crouchSpeedMultiplier);
            value.sprintSpeedMultiplier = Mathf.Max(1f, sprintSpeedMultiplier);
            value.crouchViewTransitionSeconds = Mathf.Max(0f, crouchViewTransitionSeconds);
            return value;
        }
    }
}
