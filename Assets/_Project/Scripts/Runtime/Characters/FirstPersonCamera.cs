using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectNeo.Characters
{
    public sealed class FirstPersonCamera : PlayerViewCamera
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerControlSettings settings;
        Renderer[] bodyRenderers;
        ShadowCastingMode[] originalShadows;
        float yaw, pitch;
        public override float Yaw => yaw;

        public void Configure(Transform followTarget, PlayerControlSettings controlSettings)
        { target = followTarget; settings = controlSettings; }

        void Awake()
        {
            yaw = target.eulerAngles.y;
            pitch = settings.InitialPitch;
            Transform visual = target.Find("Visual");
            bodyRenderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            originalShadows = new ShadowCastingMode[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) originalShadows[i] = bodyRenderers[i].shadowCastingMode;
        }

        void OnEnable()
        {
            foreach (Renderer body in bodyRenderers) if (body != null) body.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            Follow();
        }

        void OnDisable()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = originalShadows[i];
        }

        public override void Look(Vector2 delta)
        {
            yaw += delta.x * settings.MouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * settings.MouseSensitivity, settings.MinimumPitch, settings.MaximumPitch);
        }

        void LateUpdate() => Follow();
        void Follow()
        {
            if (target != null)
                transform.SetPositionAndRotation(target.position + Vector3.up * settings.CameraHeight,
                    Quaternion.Euler(pitch, yaw, 0f));
        }
    }
}
