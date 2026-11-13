using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>유닛 하나가 지금 따르는 행동 방식. 부대 지시가 이 값을 정한다.</summary>
    public enum UnitDirective
    {
        Engage = 0, // 가까운 적을 찾아 쫓아가 싸운다. 적이 없으면 그 자리에서 기다린다.
        Hold = 1,   // 자리를 지킨다. 닿는 적은 공격하고, 근접은 자리 주변(반경 안)의 적에게 다가가 싸운 뒤 돌아온다.
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
        readonly MoveStallTracker stall = new MoveStallTracker();
        bool returningToPost;
        bool advancePathRetryPending;
        public CharacterActor Target { get; private set; }
        // 지시를 받지 않은 유닛(적, 관전 전투)은 기존처럼 알아서 싸운다.
        public UnitDirective Directive { get; private set; } = UnitDirective.Engage;
        public Vector3 MoveDestination { get; private set; }
        public CharacterActor Actor => actor;
        public float DecisionInterval => settings.DecisionInterval;

        public void Configure(UnitAISettings aiSettings) => settings = aiSettings;

        // Engage에 목적지가 있으면 그 지점으로 가면서 시야에 들어온 적과 싸운다.
        public bool HasAdvancePoint { get; private set; }

        /// <summary>중지 상태에서 지키는 자리. 중지를 받은 곳이나 이동이 끝난 곳이다.</summary>
        public Vector3 HoldPost { get; private set; }

        public void SetDirective(UnitDirective directive)
        {
            if (GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Defer(directive, false, default) == true) return;
            Directive = directive;
            HasAdvancePoint = false;
            destinationPending = false;
            advancePathRetryPending = false;
            Target = null;
            decisionRemaining = 0f;
            stall.Reset();
            returningToPost = false;
            if (directive == UnitDirective.Hold) HoldPost = transform.position;
        }

        public void SetDirective(UnitDirective directive, Vector3 destination)
        {
            if (GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Defer(directive, true, destination) == true) return;
            SetDirective(directive);
            if (directive == UnitDirective.Hold) HoldPost = destination;
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
            if (actor.Health != null && !actor.Health.IsDead && ProjectNeo.Siege.LadderTraveller.HandleAI(this)) return;
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
            if (destinationPending || advancePathRetryPending || decisionRemaining <= 0f)
            {
                // 지형이 바뀌어도 따라가도록 판단 주기마다 경로를 다시 구한다.
                decisionRemaining = settings.DecisionInterval;
                destinationPending = false;
                // 길이 전혀 없으면 지금 자리에서 멈춘다. 일부만 이어진 길은 갈 수 있는 데까지 간다.
                if (SetAdvancePath()) return;
            }
            else if (!HasArrived()) return;
            agent.ResetPath();
            Directive = UnitDirective.Hold;
            HoldPost = transform.position;
            stall.Reset();
            returningToPost = false;
        }

        // 목적지에 닿았거나, 목적지 근처에서 다른 유닛 등에 막혀 더 나아가지 못하면 도착으로 본다.
        bool HasArrived()
        {
            if (agent.pathPending) return false;
            float remaining = agent.remainingDistance;
            if (remaining <= settings.ArrivalDistance) return true;
            // 공격 판정을 기다리느라 멈춘 시간은 막힌 것으로 세지 않는다.
            if (actor.IsAttacking) { stall.Reset(); return false; }
            return stall.IsStuck(remaining, Time.deltaTime, settings.MoveStallDistance,
                settings.MoveStallProgress, settings.MoveStallSeconds);
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
            if (advancePathRetryPending || advanceRepathRemaining <= 0f)
            {
                advanceRepathRemaining = settings.DecisionInterval;
                if (SetAdvancePath()) return;
            }
            else if (!HasArrived()) return;
            agent.ResetPath();
            destinationPending = false;
            stall.Reset();
        }

        // SetDestination은 요청 접수 여부만 돌려준다. 갱신 후의 실제 경로가 무효인지,
        // 일부만 이어지는지 여기서 확인해 부분 경로의 끝에서도 지시를 마칠 수 있게 한다.
        bool SetAdvancePath()
        {
            bool retrying = advancePathRetryPending;
            advancePathRetryPending = false;
            if (!agent.CalculatePath(MoveDestination, path) || path.status == NavMeshPathStatus.PathInvalid)
            {
                // 목적지 성벽 자체가 사라지면 원래 점에는 NavMesh가 없다.
                // 주변에 남은 길까지 다시 계산하고, 연결이 끊겼으면 부분 경로를 따른다.
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(MoveDestination, out NavMeshHit nearby, settings.DestinationRecoveryDistance, filter)
                    || !agent.CalculatePath(nearby.position, path) || path.status == NavMeshPathStatus.PathInvalid)
                {
                    agent.ResetPath();
                    return false;
                }
            }
            bool accepted = agent.SetPath(path);
            if (!accepted)
            {
                agent.ResetPath();
                // NavMesh 교체 직후에는 유효한 경로도 한 프레임 적용이 거부될 수 있다.
                // 다음 갱신에서 한 번 다시 계산한 뒤에도 실패하면 기존처럼 멈춘다.
                if (!retrying)
                {
                    advancePathRetryPending = true;
                    stall.Reset();
                    return true;
                }
            }
            return accepted;
        }

        // 자리를 지키는 동안의 행동(배너로드식 "여기 서 있어").
        // 1) 지금 자리에서 닿는 적은 공격한다(원거리는 사거리 안의 적을 쏜다).
        // 2) 자리에서 추격 반경 안에 들어온, 걸어서 닿는 적에게 다가가 싸운다. 원거리는 사거리가 닿으면 멈춰서 쏜다.
        //    추격 반경: 근접은 HoldEngageRadius, 원거리는 공격 사거리 + HoldRangedChaseExtra.
        //    적이 반경 밖으로 나가거나 길이 끊기면 쫓지 않는다.
        // 3) 싸울 적이 없으면 자리로 돌아온다.
        void UpdateHold()
        {
            agent.stoppingDistance = 0f;
            bool chases = HoldChaseRadius > 0f;
            bool refreshed = false;
            // 기존 대상의 무효화는 즉시 처리하되, 대상이 없는 동안에도 탐색 주기를 지킨다.
            // null을 무효 대상으로 매 프레임 처리하면 대기 중인 전 부대가 매번 전체 목록을 훑는다.
            if (!ReferenceEquals(Target, null) && !IsHoldTarget(Target, chases))
            {
                Target = null;
                decisionRemaining = 0f;
            }
            if (decisionRemaining <= 0f)
            {
                decisionRemaining = settings.DecisionInterval;
                refreshed = true;
                if (Target == null)
                {
                    Target = FindTargetInReach();
                    if (Target == null && chases) Target = FindTargetNearPost();
                }
            }
            if (Target != null && CanStrikeFromHere(Target))
            {
                agent.isStopped = true;
                agent.updateRotation = false;
                Face(Target);
                if (actor.CanAttack(Target)) actor.TryAttack(Target);
                return;
            }
            agent.updateRotation = true;
            // 시작한 공격의 판정이 끝난 뒤 움직인다.
            agent.isStopped = actor.IsAttacking;
            if (Target != null)
            {
                stall.Reset();
                // 움직이는 적을 따라가도록 판단 주기마다 경로를 다시 구한다. 길이 끊겼으면 놓아준다.
                if (refreshed)
                {
                    if (CanReach(Target)) agent.SetPath(path);
                    else Target = null;
                }
                if (Target != null) return;
            }
            // 자리로 돌아가기. 여유 거리(HoldReturnDistance)보다 멀리 밀려났을 때만 출발하고, 출발하면 도착 거리까지 붙는다.
            // 자리가 다른 유닛에 막혀 더 못 가면 거기서 멈춘다. 살짝 밀린 정도로 자리 다툼을 하지 않는다.
            Vector3 offset = HoldPost - transform.position;
            offset.y = 0f;
            float away = offset.magnitude;
            if (!returningToPost && away > settings.HoldReturnDistance)
            {
                returningToPost = true;
                stall.Reset();
            }
            if (returningToPost && (away <= settings.ArrivalDistance || HasArrivedAtPost(away)))
                returningToPost = false;
            if (!returningToPost)
            {
                if (agent.hasPath) agent.ResetPath();
                agent.isStopped = true;
                agent.updateRotation = false;
                return;
            }
            if (refreshed || !agent.hasPath) agent.SetDestination(HoldPost);
        }

        bool HasArrivedAtPost(float away)
        {
            if (actor.IsAttacking) { stall.Reset(); return false; }
            return stall.IsStuck(away, Time.deltaTime, settings.MoveStallDistance,
                settings.MoveStallProgress, settings.MoveStallSeconds);
        }

        bool IsHoldTarget(CharacterActor target, bool chases) => actor.IsEnemy(target)
            && (CanStrikeFromHere(target) || (chases && IsNearPost(target)));

        /// <summary>중지 중에 자리에서 이 거리 안의 적을 쫓는다. 0이면 쫓지 않는다.</summary>
        public float HoldChaseRadius => actor.Profile.AttackKind == AttackKind.Melee ? settings.HoldEngageRadius
            : settings.HoldRangedChaseExtra > 0f ? actor.Stats.GetValue(StatType.AttackRange) + settings.HoldRangedChaseExtra : 0f;

        bool IsNearPost(CharacterActor target) =>
            (target.transform.position - HoldPost).sqrMagnitude <= HoldChaseRadius * HoldChaseRadius;

        // 자리 반경 안에서 걸어서 닿는 적 중 지금 위치에서 가장 가까운 적.
        CharacterActor FindTargetNearPost()
        {
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (candidate.Team == actor.Team || !IsNearPost(candidate) || !actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !CanReach(candidate)) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        void UpdateEngage()
        {
            agent.stoppingDistance = actor.Stats.GetValue(StatType.AttackRange) * settings.StoppingRangeRatio;
            if (!ReferenceEquals(Target, null) && !actor.IsEnemy(Target))
            {
                Target = null;
                decisionRemaining = 0f;
            }
            if (decisionRemaining <= 0f)
            {
                decisionRemaining = settings.DecisionInterval;
                if (Target != null && CanReach(Target)) agent.SetPath(path);
                else
                {
                    Target = FindTarget();
                    // 대상을 고르는 동안 path가 바뀔 수 있으므로 선택된 대상의 경로를 다시 얻는다.
                    if (Target != null && CanReach(Target)) agent.SetPath(path);
                }
            }
            if (Target == null)
            {
                Advance();
                return;
            }
            // 적을 쫓느라 경로가 바뀌었으므로, 처리한 뒤에는 지점으로 가는 길을 바로 다시 구한다.
            advanceRepathRemaining = 0f;
            stall.Reset();
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
            Vector3 position = transform.position;
            Vector3 aim = actor.AimPoint;
            float range = actor.Stats.GetValue(StatType.AttackRange);
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (candidate.Team == actor.Team || candidate.Body == null) continue;
                // AABB는 실제 콜라이더를 포함하므로 범위 밖 후보만 안전하게 제외한다.
                // 큰/오프셋 콜라이더는 통과시키고 기존 정밀 판정과 시야 검사를 그대로 적용한다.
                if (candidate.Body.bounds.SqrDistance(aim) > range * range) continue;
                float distance = (candidate.transform.position - position).sqrMagnitude;
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
                if (candidate.Team == actor.Team) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !actor.IsEnemy(candidate) || !CanReach(candidate)) continue;
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
            GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Interrupt();
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
