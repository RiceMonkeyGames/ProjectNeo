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
        PlayerInputController player;
        float eyeHeight, startEyeHeight, targetEyeHeight, transitionElapsed;
        float DesiredEyeHeight => player != null ? player.EyeHeight : settings.CameraHeight;
        public override float Yaw => yaw;
        public float Pitch => pitch;

        /// <summary>시점을 직접 정한다. 대포 조작처럼 회전 범위를 따로 자를 때 쓴다. 위아래 한계는 설정을 따른다.</summary>
        public void SetOrientation(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, settings.MinimumPitch, settings.MaximumPitch);
        }

        public void Configure(Transform followTarget, PlayerControlSettings controlSettings)
        { target = followTarget; settings = controlSettings; }

        void Awake()
        {
            yaw = target.eulerAngles.y;
            player = target.GetComponent<PlayerInputController>();
            pitch = settings.InitialPitch;
            Transform visual = target.Find("Visual");
            bodyRenderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            originalShadows = new ShadowCastingMode[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) originalShadows[i] = bodyRenderers[i].shadowCastingMode;
        }

        void OnEnable()
        {
            foreach (Renderer body in bodyRenderers) if (body != null) body.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            eyeHeight = startEyeHeight = targetEyeHeight = DesiredEyeHeight;
            transitionElapsed = settings.CrouchViewTransitionSeconds;
            Follow(0f);
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

        void LateUpdate() => Follow(Time.deltaTime);
        void Follow(float deltaTime)
        {
            if (target != null)
            {
                float desired = DesiredEyeHeight;
                if (!Mathf.Approximately(desired, targetEyeHeight))
                {
                    startEyeHeight = eyeHeight;
                    targetEyeHeight = desired;
                    transitionElapsed = 0f;
                }
                transitionElapsed += deltaTime;
                float duration = settings.CrouchViewTransitionSeconds;
                float progress = duration > 0f ? Mathf.Clamp01(transitionElapsed / duration) : 1f;
                eyeHeight = Mathf.Lerp(startEyeHeight, targetEyeHeight, Mathf.SmoothStep(0f, 1f, progress));
                // Smooth only posture height; walking, falls and teleports follow the target directly.
                transform.SetPositionAndRotation(target.position + Vector3.up * eyeHeight,
                    Quaternion.Euler(pitch, yaw, 0f));
            }
        }
    }
}
