using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>신호를 받아 색과 체력 막대만 바꾸는 교체용 임시 외형.</summary>
    public sealed class PlaceholderCombatView : MonoBehaviour
    {
        [SerializeField] CharacterActor actor;
        [SerializeField] Renderer body;
        [SerializeField] Transform healthBar;
        [SerializeField] GameObject attackMarker;
        Color normalColor;
        MaterialPropertyBlock properties;
        float flashUntil;
        float attackUntil;
        ProjectNeo.Characters.PlayerInputController player;
        Vector3 standingScale;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Configure(CharacterActor owner, Renderer bodyRenderer, Transform bar, GameObject marker)
        {
            actor = owner;
            body = bodyRenderer;
            healthBar = bar;
            attackMarker = marker;
        }

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            player = actor.GetComponent<ProjectNeo.Characters.PlayerInputController>();
            standingScale = transform.localScale;
            normalColor = body.sharedMaterial.GetColor(BaseColor);
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
            Color color = actor.Health.IsDead ? Color.gray : Time.time < flashUntil ? Color.white : normalColor;
            properties.SetColor(BaseColor, color);
            body.SetPropertyBlock(properties);
            attackMarker.SetActive(!actor.Health.IsDead && Time.time < attackUntil);
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
