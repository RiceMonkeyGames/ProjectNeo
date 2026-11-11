using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>유닛 하나가 지금 따르는 행동 방식. 부대 지시가 이 값을 정한다.</summary>
    public enum UnitDirective
    {
        Engage = 0, // 가까운 적을 찾아 쫓아가 싸운다. 적이 없으면 그 자리에서 기다린다.
        Hold = 1,   // 자리를 지킨다. 쫓지 않고 사거리 안의 적만 공격한다.
        Move = 2,   // 적을 무시하고 목적지로 간다. 도착하면 Hold가 된다.
    }

    /// <summary>공통 추격·공격과 지시별 행동. 성벽별 적 행동 조합은 후속 단계다.</summary>
    [RequireComponent(typeof(CharacterActor), typeof(NavMeshAgent))]
    public sealed class UnitCombatAI : MonoBehaviour
    {
        [SerializeField] UnitAISettings settings;
        CharacterActor actor;
        NavMeshAgent agent;
        NavMeshPath path;
        float decisionRemaining;
        bool destinationPending;
        float advanceRepathRemaining;
        public CharacterActor Target { get; private set; }
        // 지시를 받지 않은 유닛(적, 관전 전투)은 기존처럼 알아서 싸운다.
        public UnitDirective Directive { get; private set; } = UnitDirective.Engage;
        public Vector3 MoveDestination { get; private set; }
        public CharacterActor Actor => actor;

        public void Configure(UnitAISettings aiSettings) => settings = aiSettings;

        // Engage에 목적지가 있으면 그 지점으로 가면서 시야에 들어온 적과 싸운다.
        public bool HasAdvancePoint { get; private set; }

        public void SetDirective(UnitDirective directive)
        {
            Directive = directive;
            HasAdvancePoint = false;
            destinationPending = false;
            Target = null;
            decisionRemaining = 0f;
        }

        public void SetDirective(UnitDirective directive, Vector3 destination)
        {
            SetDirective(directive);
            MoveDestination = destination;
            HasAdvancePoint = directive == UnitDirective.Engage;
            destinationPending = directive != UnitDirective.Hold;
            advanceRepathRemaining = 0f;
        }

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
            decisionRemaining -= Time.deltaTime;
            switch (Directive)
            {
                case UnitDirective.Move: UpdateMove(); break;
                case UnitDirective.Hold: UpdateHold(); break;
                default: UpdateEngage(); break;
            }
        }

        void UpdateMove()
        {
            Target = null;
            agent.stoppingDistance = 0f;
            agent.updateRotation = true;
            // 시작한 공격의 판정이 끝난 뒤 움직인다.
            agent.isStopped = actor.IsAttacking;
            if (destinationPending || decisionRemaining <= 0f)
            {
                // 지형이 바뀌어도 따라가도록 판단 주기마다 경로를 다시 구한다.
                decisionRemaining = settings.DecisionInterval;
                destinationPending = false;
                // 길이 전혀 없으면 지금 자리에서 멈춘다. 일부만 이어진 길은 갈 수 있는 데까지 간다.
                if (agent.SetDestination(MoveDestination)) return;
            }
            else if (agent.pathPending || agent.remainingDistance > settings.ArrivalDistance) return;
            agent.ResetPath();
            Directive = UnitDirective.Hold;
        }

        // 싸울 적이 없을 때: 지정한 지점이 있으면 그곳으로 가고, 도착했거나 지점이 없으면 기다린다.
        void Advance()
        {
            if (!destinationPending)
            {
                agent.isStopped = true;
                return;
            }
            agent.stoppingDistance = 0f;
            agent.updateRotation = true;
            agent.isStopped = actor.IsAttacking;
            advanceRepathRemaining -= Time.deltaTime;
            if (advanceRepathRemaining <= 0f)
            {
                advanceRepathRemaining = settings.DecisionInterval;
                if (agent.SetDestination(MoveDestination)) return;
            }
            else if (agent.pathPending || agent.remainingDistance > settings.ArrivalDistance) return;
            agent.ResetPath();
            destinationPending = false;
        }

        void UpdateHold()
        {
            agent.isStopped = true;
            agent.updateRotation = false;
            if (!CanStrikeFromHere(Target) || decisionRemaining <= 0f)
            {
                decisionRemaining = settings.DecisionInterval;
                if (!CanStrikeFromHere(Target)) Target = FindTargetInReach();
            }
            if (Target == null) return;
            Face(Target);
            if (actor.CanAttack(Target)) actor.TryAttack(Target);
        }

        void UpdateEngage()
        {
            agent.stoppingDistance = actor.Stats.GetValue(StatType.AttackRange) * settings.StoppingRangeRatio;
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
                Advance();
                return;
            }
            // 적을 쫓느라 경로가 바뀌었으므로, 처리한 뒤에는 지점으로 가는 길을 바로 다시 구한다.
            advanceRepathRemaining = 0f;
            bool inRange = CombatPhysics.IsInRange(actor, Target);
            if (inRange) Face(Target);
            bool canAttack = actor.CanAttack(Target);
            if (!CombatPhysics.HasClearShot(actor, Target)) agent.stoppingDistance = 0f;
            agent.isStopped = canAttack || actor.IsAttacking;
            agent.updateRotation = !inRange;
            if (canAttack) actor.TryAttack(Target);
        }

        void Face(CharacterActor target)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), settings.TurnSpeed * Time.deltaTime);
        }

        bool CanReach(CharacterActor target)
        {
            return agent.CalculatePath(target.transform.position, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        // 움직이지 않고 지금 자리에서 닿는 적인지. 방향은 돌려서 맞추므로 따지지 않는다.
        bool CanStrikeFromHere(CharacterActor target) => actor.IsEnemy(target)
            && CombatPhysics.IsInRange(actor, target) && CombatPhysics.HasClearShot(actor, target);

        CharacterActor FindTargetInReach()
        {
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !CanStrikeFromHere(candidate)) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
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
