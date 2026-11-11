using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>B-3 공통 추격·공격. 성벽별 행동 조합과 부대 지시는 후속 단계다.</summary>
    [RequireComponent(typeof(CharacterActor), typeof(NavMeshAgent))]
    public sealed class UnitCombatAI : MonoBehaviour
    {
        [SerializeField] UnitAISettings settings;
        CharacterActor actor;
        NavMeshAgent agent;
        NavMeshPath path;
        float decisionRemaining;
        public CharacterActor Target { get; private set; }

        public void Configure(UnitAISettings aiSettings) => settings = aiSettings;

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
        }

        void OnEnable() => actor.Died += OnDied;

        void Update()
        {
            if (actor.Health == null || actor.Health.IsDead || !agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = actor.Stats.GetValue(StatType.MoveSpeed);
            agent.acceleration = settings.Acceleration;
            agent.angularSpeed = settings.TurnSpeed;
            agent.stoppingDistance = actor.Stats.GetValue(StatType.AttackRange) * settings.StoppingRangeRatio;
            decisionRemaining -= Time.deltaTime;
            if (!actor.IsEnemy(Target) || decisionRemaining <= 0f)
            {
                decisionRemaining = settings.DecisionInterval;
                if (!actor.IsEnemy(Target) || !CanReach(Target)) Target = FindTarget();
                if (Target != null)
                {
                    // 대상을 고르는 동안 path가 바뀔 수 있으므로 선택된 대상의 경로를 다시 얻는다.
                    if (CanReach(Target)) agent.SetPath(path);
                }
            }
            if (Target == null)
            {
                agent.isStopped = true;
                return;
            }
            bool inRange = CombatPhysics.IsInRange(actor, Target);
            if (inRange)
            {
                Vector3 direction = Target.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(direction), settings.TurnSpeed * Time.deltaTime);
            }
            bool canAttack = actor.CanAttack(Target);
            if (!CombatPhysics.HasClearShot(actor, Target)) agent.stoppingDistance = 0f;
            agent.isStopped = canAttack || actor.IsAttacking;
            agent.updateRotation = !inRange;
            if (canAttack) actor.TryAttack(Target);
        }

        bool CanReach(CharacterActor target)
        {
            return agent.CalculatePath(target.transform.position, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        CharacterActor FindTarget()
        {
            CharacterActor nearest = null;
            float nearestDistance = settings.DetectionRange * settings.DetectionRange;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !CanReach(candidate)) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        void OnDied()
        {
            Target = null;
            agent.enabled = false;
        }

        void OnDisable()
        {
            actor.Died -= OnDied;
            Target = null;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }
    }
}
