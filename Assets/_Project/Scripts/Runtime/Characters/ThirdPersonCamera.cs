using UnityEngine;

namespace ProjectNeo.Characters
{
    public sealed class ThirdPersonCamera : PlayerViewCamera
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerControlSettings settings;
        float pitch;
        float yaw;
        PlayerInputController player;
        public override float Yaw => yaw;

        public void Configure(Transform followTarget, PlayerControlSettings controlSettings)
        {
            target = followTarget;
            settings = controlSettings;
        }

        void Awake()
        {
            yaw = target.eulerAngles.y;
            player = target.GetComponent<PlayerInputController>();
            pitch = settings.InitialPitch;
        }

        public override void Look(Vector2 delta)
        {
            yaw += delta.x * settings.MouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * settings.MouseSensitivity,
                settings.MinimumPitch, settings.MaximumPitch);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, Yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * (player != null ? player.EyeHeight : settings.CameraHeight);
            Vector3 backward = rotation * Vector3.back;
            float distance = settings.CameraDistance;
            foreach (RaycastHit hit in Physics.SphereCastAll(pivot, settings.CameraCollisionRadius,
                backward, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(target)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
            }
            transform.SetPositionAndRotation(pivot + backward * distance, rotation);
        }
    }
}
