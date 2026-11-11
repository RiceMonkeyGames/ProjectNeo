using UnityEngine;

namespace ProjectNeo.Characters
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerControlSettings settings;
        float pitch;
        public float Yaw { get; private set; }

        public void Configure(Transform followTarget, PlayerControlSettings controlSettings)
        {
            target = followTarget;
            settings = controlSettings;
        }

        void Awake()
        {
            Yaw = target.eulerAngles.y;
            pitch = settings.InitialPitch;
        }

        public void Look(Vector2 delta)
        {
            Yaw += delta.x * settings.MouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * settings.MouseSensitivity,
                settings.MinimumPitch, settings.MaximumPitch);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, Yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * settings.CameraHeight;
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
