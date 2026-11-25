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
            Renderer[] all = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            // SIEGE-36: 캐릭터 모델(Animator 아래)이 있으면 1인칭에서도 몸·팔·다리·무기가 보인다. 임시 몸체·체력 막대 같은
            // 나머지 표시만 숨긴다. 모델이 없으면 예전처럼 몸 전체를 그림자만 남긴다.
            bool hasModel = false;
            foreach (Renderer renderer in all) hasModel |= renderer.GetComponentInParent<Animator>(true) != null;
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in all)
                if (!hasModel || renderer.GetComponentInParent<Animator>(true) == null) hidden.Add(renderer);
            bodyRenderers = hidden.ToArray();
            hideCompletely = hasModel;
            // 모델의 머리·투구가 시야를 가리지 않게, 머리 없는 복사본을 화면에 그리고 원래 모델은 그림자만 그린다.
            if (hasModel && visual != null) firstPersonBody = FirstPersonBody.Attach(visual.GetComponentInChildren<Animator>(true));
            originalShadows = new ShadowCastingMode[bodyRenderers.Length];
            originalForceOff = new bool[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                originalShadows[i] = bodyRenderers[i].shadowCastingMode;
                originalForceOff[i] = bodyRenderers[i].forceRenderingOff;
            }
        }

        bool hideCompletely;
        bool[] originalForceOff;
        FirstPersonBody firstPersonBody;

        void OnEnable()
        {
            foreach (Renderer body in bodyRenderers)
            {
                if (body == null) continue;
                if (hideCompletely) body.forceRenderingOff = true;
                else body.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            if (firstPersonBody != null) firstPersonBody.SetFirstPerson(true);
            eyeHeight = startEyeHeight = targetEyeHeight = DesiredEyeHeight;
            transitionElapsed = settings.CrouchViewTransitionSeconds;
            Follow(0f);
        }

        void OnDisable()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                bodyRenderers[i].shadowCastingMode = originalShadows[i];
                bodyRenderers[i].forceRenderingOff = originalForceOff[i];
            }
            if (firstPersonBody != null) firstPersonBody.SetFirstPerson(false);
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
                // 얼굴 앞으로 조금 내밀어 내 모델의 머리 안쪽이 보이지 않게 한다(SIEGE-36).
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * settings.CameraForwardOffset;
                transform.SetPositionAndRotation(target.position + Vector3.up * eyeHeight + forward,
                    Quaternion.Euler(pitch, yaw, 0f));
            }
        }
    }
}
