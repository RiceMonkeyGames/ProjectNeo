using ProjectNeo.Characters;
using ProjectNeo.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Walls
{
    [RequireComponent(typeof(CharacterActor))]
    public sealed class UnitFallController : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] FallSettings settings;
        CharacterActor actor;
        NavMeshAgent agent;
        CharacterController motor;
        PlayerInputController input;
        float fallStart;
        float velocity;
        bool resumeAgent;
        bool resumeMotor;
        bool landed;
        public bool IsFalling { get; private set; }
        public float LastFallDamage { get; private set; }

        public void Configure(ConstructionWorld owner, FallSettings fallSettings) { world = owner; settings = fallSettings; }
        void Awake()
        {
            actor = GetComponent<CharacterActor>(); agent = GetComponent<NavMeshAgent>();
            motor = GetComponent<CharacterController>(); input = GetComponent<PlayerInputController>();
        }
        void OnEnable() => world.PieceCollapsing += OnCollapsing;
        void OnDisable() => world.PieceCollapsing -= OnCollapsing;

        void OnCollapsing(PlacedStructure structure)
        {
            if (IsFalling || actor.Health == null || actor.Health.IsDead) return;
            if (FindGround(transform.position + Vector3.up * settings.SupportProbe,
                settings.SupportProbe * 2f, out RaycastHit hit)
                && hit.collider.GetComponentInParent<PlacedStructure>() == structure) BeginFall();
        }

        bool FindGround(Vector3 start, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(start, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != gameObject.scene
                    || hit.collider.GetComponentInParent<CharacterActor>() != null) continue;
                if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
            }
            return found;
        }

        /// <summary>성벽 붕괴 외의 이유(사다리가 밀림 등)로 지금 높이에서 떨어뜨린다.</summary>
        public void BeginFall(bool resumeAgentAfterLanding)
        {
            if (IsFalling) return;
            BeginFall();
            resumeAgent = resumeAgentAfterLanding && agent != null;
        }

        void BeginFall()
        {
            IsFalling = true; landed = false; fallStart = transform.position.y; velocity = 0f;
            resumeAgent = agent != null && agent.enabled;
            resumeMotor = motor != null && motor.enabled;
            if (resumeAgent) agent.enabled = false;
            if (resumeMotor) motor.enabled = false;
            if (input != null) input.SetMovementSuspended(true);
        }

        void Update()
        {
            if (!IsFalling) return;
            if (!landed)
            {
                velocity += settings.Gravity * Time.deltaTime;
                float step = velocity * Time.deltaTime;
                if (FindGround(transform.position + Vector3.up * 0.05f, step + 0.05f, out var ground))
                {
                    transform.position = ground.point;
                    landed = true;
                    LastFallDamage = FallDamageRules.Calculate(fallStart - ground.point.y, settings.SafeHeight, settings.DamagePerMeter);
                    actor.ReceiveDamage(LastFallDamage);
                }
                else transform.position += Vector3.down * step;
            }
            if (!landed) return;
            if (actor.Health.IsDead) { IsFalling = false; return; }
            if (resumeAgent)
            {
                if (world.IsRebuilding || world.RebuildPending) return;
                if (!NavMesh.SamplePosition(transform.position, out var hit, settings.NavMeshSnapDistance, NavMesh.AllAreas)
                    || Mathf.Abs(hit.position.y - transform.position.y) > settings.NavMeshSnapDistance) return;
                transform.position = hit.position;
                agent.enabled = true;
            }
            if (resumeMotor) motor.enabled = true;
            if (input != null) input.SetMovementSuspended(false);
            IsFalling = false;
        }
    }
}
