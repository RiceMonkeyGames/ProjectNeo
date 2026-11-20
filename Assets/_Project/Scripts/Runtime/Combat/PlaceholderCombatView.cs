using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 신호를 받아 색과 체력 막대만 바꾸는 교체용 임시 외형.
    /// 캐릭터 모델(<see cref="ConfigureModel"/>)이 있으면 몸 대신 모델 전체에 색을 입히고, 죽으면 모델을 눕힌다.
    /// 애니메이션이 붙기 전까지의 임시 표현이다.
    /// </summary>
    public sealed class PlaceholderCombatView : MonoBehaviour
    {
        [SerializeField] CharacterActor actor;
        [SerializeField] Renderer body;
        [SerializeField] Transform healthBar;
        [SerializeField] GameObject attackMarker;
        [SerializeField] Transform model;
        Color normalColor;
        MaterialPropertyBlock properties;
        float flashUntil;
        float attackUntil;
        ProjectNeo.Characters.PlayerInputController player;
        Vector3 standingScale;
        Renderer[] modelRenderers;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // 텍스처를 쓰는 모델은 흰색을 곱해도 그대로라 피격 번쩍임을 밝기로 표현한다.
        static readonly Color ModelFlashColor = new Color(2.2f, 2.2f, 2.2f, 1f);
        static readonly Color ModelDeadColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        public Transform Model => model;

        public void Configure(CharacterActor owner, Renderer bodyRenderer, Transform bar, GameObject marker)
        {
            actor = owner;
            body = bodyRenderer;
            healthBar = bar;
            attackMarker = marker;
        }

        /// <summary>캐릭터 모델을 연결한다. null이면 기존 임시 몸 표현으로 돌아간다.</summary>
        public void ConfigureModel(Transform characterModel) => model = characterModel;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            player = actor.GetComponent<ProjectNeo.Characters.PlayerInputController>();
            standingScale = transform.localScale;
            if (model != null)
            {
                modelRenderers = model.GetComponentsInChildren<Renderer>();
                normalColor = Color.white;
            }
            else normalColor = body.sharedMaterial.GetColor(BaseColor);
        }

        void OnEnable()
        {
            actor.AttackStarted += OnAttack;
            actor.HitReceived += OnHit;
            actor.Died += OnDeath;
        }

        void Update()
        {
            if (actor.Health == null) return;
            float ratio = actor.Health.Maximum > 0f ? actor.Health.Current / actor.Health.Maximum : 0f;
            healthBar.localScale = new Vector3(ratio, 0.12f, 0.12f);
            healthBar.localPosition = new Vector3((ratio - 1f) * 0.5f, actor.Profile.Height + 0.35f, 0f);
            bool dead = actor.Health.IsDead;
            bool flashing = Time.time < flashUntil;
            if (modelRenderers != null)
            {
                properties.SetColor(BaseColor, dead ? ModelDeadColor : flashing ? ModelFlashColor : normalColor);
                foreach (Renderer part in modelRenderers) part.SetPropertyBlock(properties);
            }
            else
            {
                properties.SetColor(BaseColor, dead ? Color.gray : flashing ? Color.white : normalColor);
                body.SetPropertyBlock(properties);
            }
            attackMarker.SetActive(!dead && Time.time < attackUntil);
        }

        void OnAttack() => attackUntil = Time.time + actor.Profile.ImpactDelay + 0.15f;
        void LateUpdate()
        {
            if (player == null || actor.Health == null || actor.Health.IsDead) return;
            transform.localScale = new Vector3(standingScale.x, standingScale.y * player.HeightRatio, standingScale.z);
        }

        void OnHit() => flashUntil = Time.time + 0.12f;
        void OnDeath()
        {
            // 길이가 0인 막대도 옆면이 얇게 보이므로 죽으면 숨긴다.
            healthBar.gameObject.SetActive(false);
            if (model != null)
            {
                // 사망 동작을 재생하는 모델(ART-04)은 그대로 둔다. 없으면 뒤로 눕혀 쓰러진 것을 표시한다.
                if (TryGetComponent(out CharacterModelAnimator animated) && animated.PlaysDeath) return;
                model.localRotation = Quaternion.Euler(-90f, 0f, 0f) * model.localRotation;
                model.localPosition += Vector3.up * (actor.Profile.Radius * 0.5f);
                return;
            }
            Vector3 scale = body.transform.localScale;
            scale.y *= 0.12f;
            body.transform.localScale = scale;
            body.transform.localPosition = Vector3.up * (actor.Profile.Height * 0.06f);
        }

        void OnDisable()
        {
            actor.AttackStarted -= OnAttack;
            actor.HitReceived -= OnHit;
            actor.Died -= OnDeath;
        }
    }
}
