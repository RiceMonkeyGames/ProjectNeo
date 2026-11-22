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
        float targetLockRemaining;
        CharacterActor retaliatingTarget;
        bool suppressRetaliation;
        float coverCheckRemaining;
        bool behindCover;
        ShotOpening opening;
        RangedBlockAction blockAction = RangedBlockAction.Attack;
        // 사선 캐시(PERF-ai-query-budget): 판단 주기마다, 대상이 바뀔 때, 또는 실제 공격 직전에만 갱신한다.
        bool hasClearShotCached = true;
        bool clearShotDirty = true;
        readonly TargetCandidateBuffer<CharacterActor> topCandidates = new TargetCandidateBuffer<CharacterActor>(4);
        readonly System.Collections.Generic.List<CharacterActor> spatialCandidates =
            new System.Collections.Generic.List<CharacterActor>(64);
        public static readonly AIDecisionScheduler Scheduler = new AIDecisionScheduler();
        // 대규모 전투 3단계 AI LOD(PERF-ai-lod).
        AILodTier lodTier = AILodTier.Lod0;
        bool inViewCone = true;
        float lodCheckRemaining;
        // 분대 단위 대기 슬립(PERF-siege-ai-scale): 분대 전체 바운딩 범위 안에 적이 없으면 Hold 탐색을 건너뛴다.
        bool squadNeighborhoodClear;
        public void SetSquadNeighborhoodClear(bool clear) => squadNeighborhoodClear = clear;
        public bool IsSquadNeighborhoodClear => squadNeighborhoodClear;
        // 자리 방향(COMBAT-14). 대형이 바라볼 방향이나 자리를 받은 때의 방향(도). 없으면 돌지 않는다.
        float? postFacing;
        // 근접 위협 후퇴(COMBAT-14).
        bool fallingBack;
        float fallbackRemaining;
        float fallbackCooldownRemaining;
        float threatRecheckRemaining;
        public CharacterActor Target { get; private set; }
        /// <summary>지금 유닛의 AI LOD 등급(PERF-ai-lod).</summary>
        public AILodTier LodTier => lodTier;
        /// <summary>유닛이 카메라 시야 원뿔 안에 있는지(PERF-ai-lod).</summary>
        public bool InViewCone => inViewCone;
        /// <summary>자리를 지킬 때 돌아볼 방향(y축 각도, 도). 없으면 null(COMBAT-14).</summary>
        public float? HoldFacing => postFacing;
        /// <summary>근접 적을 피해 짧게 물러나는 중인지(COMBAT-14, 검증용).</summary>
        public bool IsFallingBack => fallingBack;
        // 지시를 받지 않은 유닛(적, 관전 전투)은 기존처럼 알아서 싸운다.
        public UnitDirective Directive { get; private set; } = UnitDirective.Engage;
        public Vector3 MoveDestination { get; private set; }
        public CharacterActor Actor => actor;
        public float DecisionInterval => settings.DecisionInterval;
        public bool IsRetaliationSuppressed => suppressRetaliation;

        public void Configure(UnitAISettings aiSettings) => settings = aiSettings;

        /// <summary>공성 임무 등 특정 목표를 수행하는 동안 피격 반격·타겟 전환을 막는다.</summary>
        public void SetRetaliationSuppressed(bool suppressed) => suppressRetaliation = suppressed;

        /// <summary>
        /// 대상을 고르고 바꿀 때 제곱 거리에 곱하는 배율(SIEGE-09 결투형 보스). 1보다 작으면 그 대상을 더 가깝게 본다.
        /// 비워 두면(기본) 예전처럼 거리만으로 고른다. 탐지 거리와 닿는지 검사는 실제 거리로 한다.
        /// </summary>
        public System.Func<CharacterActor, float> TargetDistanceScale { get; set; }

        // Engage에 목적지가 있으면 그 지점으로 가면서 시야에 들어온 적과 싸운다.
        public bool HasAdvancePoint { get; private set; }

        /// <summary>중지 상태에서 지키는 자리. 중지를 받은 곳이나 이동이 끝난 곳이다.</summary>
        public Vector3 HoldPost { get; private set; }

        /// <summary>
        /// 동시에 켜지거나 부대 지시를 받은 여러 유닛의 판단 주기를 여러 프레임으로 나눈다(PERF-ai-query-budget).
        /// </summary>
        public void SetDecisionOffset(float offsetSeconds)
        {
            staggerIndex = 0;
            decisionRemaining = Mathf.Max(0f, offsetSeconds);
        }

        public void SetDirective(UnitDirective directive)
        {
            if (GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Defer(directive, false, default) == true) return;
            Directive = directive;
            HasAdvancePoint = false;
            destinationPending = false;
            advancePathRetryPending = false;
            Target = null;
            retaliatingTarget = null;
            targetLockRemaining = 0f;
            decisionRemaining = 0f;
            stall.Reset();
            returningToPost = false;
            ReleaseOpening();
            meleeSlotBoard.Release(this);
            blockAction = RangedBlockAction.Attack;
            hasClearShotCached = true;
            clearShotDirty = true;
            squadNeighborhoodClear = false;
            fallingBack = false;
            // 방향은 부대 대형 방향을 함께 받은 지시(SetDirective(지시, 목적지, 방향))만 지킨다(COMBAT-14).
            // 공성 적 판단처럼 중지 중에 스스로 방향을 돌리는 쪽과 다투지 않게 여기서는 비운다.
            postFacing = null;
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

        /// <summary>
        /// 목적지와 그곳에서 바라볼 방향(대형 방향, y축 각도)을 함께 준다(COMBAT-14). 이동이 끝나 자리를 지키거나
        /// 그 자리에서 중지하면, 싸울 대상이 없을 때 이 방향으로 돌아선다.
        /// </summary>
        public void SetDirective(UnitDirective directive, Vector3 destination, float facing)
        {
            if (GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Defer(directive, true, destination, facing) == true) return;
            SetDirective(directive, destination);
            postFacing = facing;
        }

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
        }

        void OnEnable()
        {
            actor.Died += OnDied;
            actor.HitReceivedFrom += OnHitReceived;
        }

        static int nextStaggerIndex;
        int staggerIndex = -1;

        public static void ResetStaggerCounter() => nextStaggerIndex = 0;

        float NextDecisionInterval()
        {
            float baseInterval = settings.DecisionInterval * AILodRules.IntervalMultiplier(lodTier);
            if (staggerIndex < 0 && settings != null && settings.DecisionStaggerBuckets > 1)
            {
                // 첫 1명은 원래 판단 주기를 지키고, 같은 장면에 이어서 생성되는 유닛들만 버킷별로 판단 시점을 나눈다.
                staggerIndex = CharacterActor.ActiveActors.Count <= 2 ? 0 : nextStaggerIndex++;
                float offset = AIDecisionScheduler.InitialOffset(staggerIndex, settings.DecisionStaggerBuckets, baseInterval);
                if (offset > 0f) return offset;
            }
            return baseInterval;
        }

        int CurrentCandidateLimit() => AILodRules.PathCandidateLimit(lodTier,
            settings != null ? settings.MaxPathCandidatesPerTick : 4);

        void UpdateLodTier()
        {
            if (settings == null || !settings.EnableAiLod)
            {
                lodTier = AILodTier.Lod0;
                inViewCone = true;
                return;
            }
            lodCheckRemaining -= Time.deltaTime;
            if (lodCheckRemaining > 0f) return;
            lodCheckRemaining = settings.LodEvaluationInterval;

            if (!AILodContext.TrySampleView(transform.position, out float sqrDist, out bool inView, out bool topDown))
            {
                lodTier = AILodTier.Lod0;
                inViewCone = true;
                return;
            }
            inViewCone = inView;
            bool highPriority = actor.Character != null || (Target != null && Target.Character != null);
            AILodTier next = AILodRules.Evaluate(true, sqrDist, settings.Lod0Distance, settings.Lod1Distance,
                inView, highPriority, Target != null, topDown);
            if (next != lodTier)
            {
                lodTier = next;
                ApplyLodAvoidance();
            }
        }

        void ApplyLodAvoidance()
        {
            if (agent == null || !agent.enabled) return;
            agent.obstacleAvoidanceType = lodTier switch
            {
                AILodTier.Lod0 => ObstacleAvoidanceType.HighQualityObstacleAvoidance,
                AILodTier.Lod1 => ObstacleAvoidanceType.MedQualityObstacleAvoidance,
                _ => Directive == UnitDirective.Hold && Target == null
                    ? ObstacleAvoidanceType.NoObstacleAvoidance
                    : ObstacleAvoidanceType.LowQualityObstacleAvoidance,
            };
        }

        void Update()
        {
            if (actor.Health != null && !actor.Health.IsDead && ProjectNeo.Siege.LadderTraveller.HandleAI(this)) return;
            if (actor.Health == null || actor.Health.IsDead || !agent.enabled || !agent.isOnNavMesh) return;
            UpdateLodTier();
            agent.speed = actor.Stats.GetValue(StatType.MoveSpeed);
            agent.acceleration = settings.Acceleration;
            agent.angularSpeed = settings.TurnSpeed;
            decisionRemaining -= Time.deltaTime;
            targetLockRemaining = Mathf.Max(0f, targetLockRemaining - Time.deltaTime);
            fallbackCooldownRemaining = Mathf.Max(0f, fallbackCooldownRemaining - Time.deltaTime);
            threatRecheckRemaining = Mathf.Max(0f, threatRecheckRemaining - Time.deltaTime);
            switch (Directive)
            {
                case UnitDirective.Move: UpdateMove(); break;
                case UnitDirective.Hold: UpdateHold(); break;
                default: UpdateEngage(); break;
            }
            UpdateCoverCrouch();
        }

        /// <summary>
        /// 성가퀴 뒤 원거리 유닛은 재장전 동안 웅크리고, 다음 발사 직전에 일어나 쏜다(COMBAT-10).
        /// 엄폐물 검사는 멈춰 있을 때 판단 주기마다 한 번만 한다.
        /// </summary>
        void UpdateCoverCrouch()
        {
            CombatProfile profile = actor.Profile;
            bool moving = agent.velocity.sqrMagnitude > 0.04f;
            // 움직이는 동안은 엄폐물을 찾지 않고, 멈추면 바로 다시 찾는다.
            if (profile.AttackKind != AttackKind.Projectile || profile.CoverSearchRadius <= 0f || moving)
                coverCheckRemaining = 0f;
            else
            {
                coverCheckRemaining -= Time.deltaTime;
                if (coverCheckRemaining <= 0f)
                {
                    coverCheckRemaining = settings.DecisionInterval * AILodRules.IntervalMultiplier(lodTier);
                    behindCover = CombatPhysics.IsBehindOwnCover(actor, profile.CoverSearchRadius);
                }
            }
            bool covered = behindCover && profile.AttackKind == AttackKind.Projectile && profile.CoverSearchRadius > 0f;
            actor.SetCrouched(CoverCrouchRules.ShouldCrouch(covered, actor.IsAttacking, moving,
                actor.CooldownRemaining, profile.CoverRiseLead));
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
                decisionRemaining = NextDecisionInterval();
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
            if (ContinueFallback()) return;
            bool chases = HoldChaseRadius > 0f;
            bool refreshed = false;
            // 기존 대상의 무효화는 즉시 처리하되, 대상이 없는 동안에도 탐색 주기를 지킨다.
            // null을 무효 대상으로 매 프레임 처리하면 대기 중인 전 부대가 매번 전체 목록을 훑는다.
            if (!ReferenceEquals(Target, null) && !IsHoldTarget(Target, chases))
            {
                Target = null;
                retaliatingTarget = null;
                decisionRemaining = 0f;
            }
            if (decisionRemaining <= 0f)
            {
                decisionRemaining = NextDecisionInterval();
                refreshed = true;
                if (Target == null && !squadNeighborhoodClear)
                {
                    Target = FindTargetInReach();
                    if (Target == null && chases) Target = FindTargetNearPost();
                    if (Target != null) targetLockRemaining = settings.TargetSwitchCooldown;
                }
            }
            if (UpdateMeleeThreat(refreshed && !squadNeighborhoodClear, true)) return;
            if (refreshed && Target != null) TryUpgradeThreatTarget();
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
                    if (CanReachInHold(Target)) ApplyApproachPath(true);
                    else
                    {
                        Target = null;
                        retaliatingTarget = null;
                    }
                }
                if (Target != null) return;
            }
            if (!squadNeighborhoodClear && UpdateOpening(refreshed)) return;
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
                RestorePostFacing();
                return;
            }
            if (refreshed || !agent.hasPath) agent.SetDestination(HoldPost);
        }

        // 싸울 대상 없이 자리에 서 있으면 대형 방향으로 돌아선다(COMBAT-14).
        void RestorePostFacing()
        {
            if (!settings.HoldFacingRestore || !postFacing.HasValue || actor.IsAttacking) return;
            float yaw = transform.eulerAngles.y;
            if (!HoldFacingRules.NeedsTurn(yaw, postFacing.Value)) return;
            transform.rotation = Quaternion.Euler(0f,
                HoldFacingRules.Step(yaw, postFacing.Value, settings.TurnSpeed * Time.deltaTime), 0f);
        }

        // ── 근접 위협 대응과 위협 우선순위(COMBAT-14) ─────────────────

        bool IsRangedUnit => actor.Profile.AttackKind == AttackKind.Projectile;

        // 위협 우선순위를 쓰는 원거리 유닛이면 대상의 위협 배율, 아니면 1.
        float ThreatFactorOf(CharacterActor other) =>
            IsRangedUnit && settings.RangedThreatPriority ? settings.ThreatDistanceFactor(other.Threat) : 1f;

        bool IsMeleeThreat(CharacterActor other) => other != null && other.Profile != null
            && RangedMeleeThreatRules.IsMeleeThreat(IsRangedUnit, other.Profile.AttackKind == AttackKind.Melee,
                SqrDistanceTo(other), settings.RangedMeleeThreatRadius);

        // 원거리 유닛: 위협 반경 안의 근접 적을 바로 대상으로 삼고, 물러날 공간이 있으면 짧게 물러난다.
        // 물러나는 동안 true를 돌려준다(이번 프레임의 나머지 판단을 건너뛴다).
        bool UpdateMeleeThreat(bool refreshed, bool holding)
        {
            if (!IsRangedUnit || suppressRetaliation || settings.RangedMeleeThreatRadius <= 0f) return false;
            if (!refreshed || fallingBack) return false;
            CharacterActor threat = FindMeleeThreat();
            if (threat == null) return false;
            if (!ReferenceEquals(threat, Target)) SwitchTarget(threat, false);
            if (TryBeginFallback(threat, holding)) return true;
            // 물러날 곳을 못 찾았으면 잠시 뒤에 다시 찾는다(판단 주기마다 길찾기를 하지 않게).
            if (settings.RangedFallbackDistance > 0f && fallbackCooldownRemaining <= 0f)
                fallbackCooldownRemaining = settings.DecisionInterval * 2f;
            return false;
        }

        // 진행 중인 후퇴를 이어 간다. 판단 주기의 대상 갱신(접근 경로 적용)보다 먼저 불러야 후퇴 경로를 덮어쓰지 않는다.
        // 후퇴 중이면 true(이번 프레임의 나머지 판단을 건너뛴다).
        bool ContinueFallback()
        {
            if (!fallingBack) return false;
            if (!IsRangedUnit || suppressRetaliation || settings.RangedMeleeThreatRadius <= 0f)
            {
                EndFallback();
                return false;
            }
            fallbackRemaining -= Time.deltaTime;
            bool arrived = !agent.pathPending && agent.remainingDistance <= settings.ArrivalDistance;
            if (fallbackRemaining > 0f && agent.hasPath && !arrived)
            {
                agent.stoppingDistance = 0f;
                agent.isStopped = false;
                agent.updateRotation = true;
                return true;
            }
            EndFallback();
            // 후퇴를 마치면 바로 대상과 경로를 다시 본다.
            decisionRemaining = 0f;
            return false;
        }

        CharacterActor FindMeleeThreat()
        {
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            CombatSpatialIndex.QueryEnemies(actor.Team, transform.position, settings.RangedMeleeThreatRadius, spatialCandidates);
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                if (!IsMeleeThreat(candidate)) continue;
                float distance = SqrDistanceTo(candidate);
                if (distance >= nearestDistance || !actor.IsEnemy(candidate) || !CombatPhysics.CanTarget(actor, candidate)
                    || !CanStrikeFromHere(candidate)) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        bool TryBeginFallback(CharacterActor threat, bool holding)
        {
            if (settings.RangedFallbackDistance <= 0f || fallbackCooldownRemaining > 0f || actor.IsAttacking
                || actor.IsOnLadder) return false;
            Vector3 self = transform.position;
            Vector3 threatPosition = threat.transform.position;
            Vector3 point = RangedMeleeThreatRules.FallbackPoint(self, threatPosition, -transform.forward,
                settings.RangedFallbackDistance);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, filter)) return false;
            if (!RangedMeleeThreatRules.AllowsFallback(self, hit.position, settings.MeleeSlotLevelTolerance, holding,
                    HoldPost, settings.RangedFallbackHoldLeash)) return false;
            // 위협에서 실제로 더 멀어지는 지점만 쓴다.
            Vector3 before = self - threatPosition, after = hit.position - threatPosition;
            before.y = 0f; after.y = 0f;
            if (after.sqrMagnitude <= before.sqrMagnitude) return false;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!RangedMeleeThreatRules.IsShortPath(CalculatePathLength(path), Vector3.Distance(self, hit.position), 2f))
                return false;
            float floor = (holding ? HoldPost.y : self.y) - settings.HoldMaxDescent;
            if (settings.HoldMaxDescent > 0f && !HoldDescentRule.StaysAbove(path.corners, floor)) return false;
            agent.stoppingDistance = 0f;
            agent.SetPath(path);
            agent.isStopped = false;
            agent.updateRotation = true;
            fallingBack = true;
            fallbackRemaining = settings.RangedFallbackMaxSeconds;
            returningToPost = false;
            return true;
        }

        void EndFallback()
        {
            fallingBack = false;
            fallbackCooldownRemaining = settings.RangedFallbackCooldown;
            if (agent.enabled && agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        }

        // 원거리 유닛이 쏘고 있는 대상보다 위협 배율이 확실히 작은(더 위협적인) 대상이 지금 자리에서 쏠 수 있으면 바꾼다.
        // 대상 유지 시간이 지난 뒤에만, 그리고 TargetSwitchCooldown마다 한 번만 찾는다. 붙은 근접 적은 놓지 않는다.
        void TryUpgradeThreatTarget()
        {
            if (!IsRangedUnit || !settings.RangedThreatPriority || suppressRetaliation || Target == null
                || targetLockRemaining > 0f || threatRecheckRemaining > 0f || IsMeleeThreat(Target)) return;
            threatRecheckRemaining = settings.TargetSwitchCooldown;
            float current = ThreatFactorOf(Target);
            if (current <= RangedTargetPriorityRules.MinimumFactor) return;
            CharacterActor better = FindTargetInReach(current);
            if (better != null && !ReferenceEquals(better, Target)) SwitchTarget(better, false);
        }

        // 마치콜레이션(SIEGE-12): 중지 중인 원거리 유닛이 쏠 대상이 없을 때, 근처 빈 구멍 아래에 적이 있으면 그 구멍으로 가서 선다.
        // 서면 아래 적이 보여 위의 공격 분기에서 쏜다. 아래에 적이 없어지면 구멍을 놓고 자리로 돌아간다.
        bool UpdateOpening(bool refreshed)
        {
            if (opening != null && refreshed
                && MachicolationRules.ShouldRelease(opening.isActiveAndEnabled, opening.HasEnemyBelow(actor)))
                ReleaseOpening();
            if (opening != null && !opening.isActiveAndEnabled) ReleaseOpening();
            // 원경(LOD 2)에서 대기 중인 유닛은 새 마치콜레이션 구멍 길찾기를 생략한다(PERF-ai-lod).
            if (opening == null && refreshed && lodTier != AILodTier.Lod2) ClaimOpening();
            if (opening == null) return false;
            if (opening.IsAtStand(transform.position))
            {
                if (agent.hasPath) agent.ResetPath();
                agent.isStopped = true;
                agent.updateRotation = false;
                returningToPost = false;
                return true;
            }
            agent.updateRotation = true;
            agent.isStopped = actor.IsAttacking;
            if (refreshed || !agent.hasPath) agent.SetDestination(opening.StandPoint);
            return true;
        }

        readonly System.Collections.Generic.List<MachicolationCandidate> openingCandidates =
            new System.Collections.Generic.List<MachicolationCandidate>();
        readonly System.Collections.Generic.List<int> openingOrder = new System.Collections.Generic.List<int>();
        const float OpeningLevelTolerance = 1.5f;

        void ClaimOpening()
        {
            var all = ShotOpening.Active;
            if (all.Count == 0) return;
            openingCandidates.Clear();
            float radius = 0f;
            foreach (ShotOpening candidate in all)
            {
                bool usable = candidate.CanBeUsedBy(actor);
                Vector3 offset = candidate.StandPoint - HoldPost;
                bool sameLevel = Mathf.Abs(offset.y) <= OpeningLevelTolerance;
                offset.y = 0f;
                radius = Mathf.Max(radius, candidate.SearchRadius);
                // 반경 밖이거나 쓸 수 없는 구멍은 아래 적을 세지 않는다(비싼 검사를 줄인다).
                bool near = usable && sameLevel && offset.sqrMagnitude <= candidate.SearchRadius * candidate.SearchRadius;
                openingCandidates.Add(new MachicolationCandidate(offset.sqrMagnitude, sameLevel,
                    near && candidate.IsFreeFor(this), near && candidate.HasEnemyBelow(actor)));
            }
            MachicolationRules.Rank(openingCandidates, radius, openingOrder);
            foreach (int index in openingOrder)
            {
                ShotOpening candidate = all[index];
                if (!agent.CalculatePath(candidate.StandPoint, path) || path.status != NavMeshPathStatus.PathComplete
                    || (settings.HoldMaxDescent > 0f
                        && !HoldDescentRule.StaysAbove(path.corners, HoldPost.y - settings.HoldMaxDescent))) continue;
                opening = candidate;
                opening.Claim(this);
                agent.SetPath(path);
                return;
            }
        }

        void ReleaseOpening()
        {
            if (opening != null) opening.Release(this);
            opening = null;
        }

        /// <summary>지금 쓰러 가거나 쓰고 있는 마치콜레이션 구멍(SIEGE-12). 없으면 null.</summary>
        public ShotOpening Opening => opening;

        bool HasArrivedAtPost(float away)
        {
            if (actor.IsAttacking) { stall.Reset(); return false; }
            return stall.IsStuck(away, Time.deltaTime, settings.MoveStallDistance,
                settings.MoveStallProgress, settings.MoveStallSeconds);
        }

        bool IsHoldTarget(CharacterActor target, bool chases) => actor.IsEnemy(target)
            && CombatPhysics.CanTarget(actor, target)
            && (CanStrikeFromHere(target) || (chases && IsNearPost(target))
                || (ReferenceEquals(target, retaliatingTarget) && IsWithinRetaliationLeash(target)));

        /// <summary>중지 중에 자리에서 이 거리 안의 적을 쫓는다. 0이면 쫓지 않는다.</summary>
        public float HoldChaseRadius => suppressHoldChase ? 0f
            : actor.Profile.AttackKind == AttackKind.Melee ? settings.HoldEngageRadius
            : settings.HoldRangedChaseExtra > 0f ? actor.Stats.GetValue(StatType.AttackRange) + settings.HoldRangedChaseExtra : 0f;

        bool suppressHoldChase;
        /// <summary>
        /// 중지 중에 자리 주변 적을 쫓지 않고 그 자리에서 닿는 적만 친다(SIEGE-15). 성 밖에서 지원 사격하는 적 궁수가
        /// 가려진 수비병을 쫓아 뚫린 곳으로 들어가지 않게 한다.
        /// </summary>
        public void SetHoldChaseSuppressed(bool suppressed) => suppressHoldChase = suppressed;
        public bool IsHoldChaseSuppressed => suppressHoldChase;

        bool IsNearPost(CharacterActor target) =>
            (target.transform.position - HoldPost).sqrMagnitude <= HoldChaseRadius * HoldChaseRadius;

        bool IsWithinRetaliationLeash(CharacterActor target)
        {
            float limit = settings.HoldRetaliationRadius;
            if (limit <= 0f) return false;
            if ((target.transform.position - HoldPost).sqrMagnitude > limit * limit) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 post = NavMesh.SamplePosition(HoldPost, out NavMeshHit postHit, 1f, filter) ? postHit.position : HoldPost;
            if (!NavMesh.CalculatePath(post, CombatPhysics.ReachPoint(target), filter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            return CalculatePathLength(path) <= limit;
        }

        static float CalculatePathLength(NavMeshPath navPath)
        {
            Vector3[] corners = navPath.corners;
            float total = 0f;
            for (int i = 1; i < corners.Length; i++) total += Vector3.Distance(corners[i - 1], corners[i]);
            return total;
        }

        // 자리 반경 안에서 걸어서 닿는 적 중 지금 위치에서 가장 가까운 적.
        // 공간 해시 그리드로 주변 적만 조회한 뒤 상위 K명에게만 길찾기를 시험한다(PERF-ai-query-budget, PERF-ai-spatial-grid).
        CharacterActor FindTargetNearPost()
        {
            topCandidates.Reset(CurrentCandidateLimit());
            CombatSpatialIndex.QueryEnemies(actor.Team, HoldPost, HoldChaseRadius, spatialCandidates);
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                if (!IsNearPost(candidate) || !actor.IsEnemy(candidate)
                    || !CombatPhysics.CanTarget(actor, candidate)) continue;
                float distance = RangedTargetPriorityRules.Score((candidate.transform.position - transform.position).sqrMagnitude,
                    1f, ThreatFactorOf(candidate));
                if (distance >= topCandidates.WorstScore) continue;
                topCandidates.TryAdd(candidate, distance);
            }
            for (int i = 0; i < topCandidates.Count; i++)
            {
                CharacterActor candidate = topCandidates[i];
                if (CanReachInHold(candidate)) return candidate;
            }
            return null;
        }

        void UpdateEngage()
        {
            agent.stoppingDistance = actor.Stats.GetValue(StatType.AttackRange) * settings.StoppingRangeRatio;
            if (ContinueFallback()) return;
            if (!ReferenceEquals(Target, null) && (!actor.IsEnemy(Target) || !CombatPhysics.CanTarget(actor, Target)))
            {
                Target = null;
                clearShotDirty = true;
                decisionRemaining = 0f;
            }
            bool refreshed = false;
            if (decisionRemaining <= 0f)
            {
                if (Target == null && settings != null && settings.MaxPathQueriesPerFrame > 0
                    && !Scheduler.HasPathBudget(Time.frameCount, settings.MaxPathQueriesPerFrame))
                {
                    decisionRemaining = 0f;
                }
                else
                {
                    decisionRemaining = NextDecisionInterval();
                    refreshed = true;
                    clearShotDirty = true;
                    if (Target != null && CanReach(Target))
                    {
                        if (!suppressRetaliation && targetLockRemaining <= 0f && !CanStrikeFromHere(Target))
                        {
                            CharacterActor closer = FindTarget();
                            if (closer != null && !ReferenceEquals(closer, Target)
                                && (CanStrikeFromHere(closer) || PriorityOf(closer) < PriorityOf(Target)))
                                SwitchTarget(closer, false);
                        }
                        if (CanReach(Target)) ApplyApproachPath(false);
                    }
                    else
                    {
                        Target = FindTarget();
                        clearShotDirty = true;
                        if (Target != null) targetLockRemaining = settings.TargetSwitchCooldown;
                        // 대상을 고르는 동안 path가 바뀔 수 있으므로 선택된 대상의 경로를 다시 얻는다.
                        if (Target != null && CanReach(Target)) ApplyApproachPath(false);
                    }
                }
            }
            if (UpdateMeleeThreat(refreshed, false)) return;
            if (refreshed && Target != null) TryUpgradeThreatTarget();
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
            // 판단 주기마다, 대상이 바뀔 때, 또는 실제로 공격할 준비가 되었을 때만 사선(Raycast)을 다시 검사한다(PERF-ai-query-budget).
            if (refreshed || clearShotDirty || canAttack)
            {
                hasClearShotCached = CombatPhysics.HasClearShot(actor, Target);
                clearShotDirty = false;
            }
            if (!hasClearShotCached)
            {
                HandleBlockedShot(refreshed);
                // 막은 적으로 대상을 바꿨으면 새 대상 기준으로 사거리와 사선을 바로 다시 반영한다.
                if (clearShotDirty && Target != null)
                {
                    inRange = CombatPhysics.IsInRange(actor, Target);
                    if (inRange) Face(Target);
                    canAttack = actor.CanAttack(Target);
                    hasClearShotCached = CombatPhysics.HasClearShot(actor, Target);
                    clearShotDirty = false;
                }
            }
            else blockAction = RangedBlockAction.Attack;
            // 근접 자리를 맡은 유닛은 닿는 거리에 들어와도 자리까지 간다(COMBAT-12). 가는 길에도 닿으면 친다(치는 동안은 멈춘다).
            // 원거리 유닛이 사거리 안에서 사선이 뚫려 있으면 재장전 동안에도 걸어 들어가지 않고 제자리에서 쏜다.
            bool toSlot = IsWalkingToMeleeSlot();
            if (toSlot) agent.stoppingDistance = 0f;
            bool holdRangedRange = IsRangedUnit && inRange && hasClearShotCached;
            agent.isStopped = ((canAttack || holdRangedRange) && !toSlot) || actor.IsAttacking;
            agent.updateRotation = !inRange;
            if (canAttack) actor.TryAttack(Target);
        }

        // 교전 중 사선이 막혔을 때(COMBAT-12). 원거리는 무엇이 막는지 판단 주기마다 보고, 막은 적으로 바꾸거나
        // 사거리를 지켜 기다린다. 구조물이 막았을 때와 근접 유닛은 예전처럼 다가간다(정지 거리 0).
        void HandleBlockedShot(bool refreshed)
        {
            if (actor.Profile.AttackKind != AttackKind.Projectile)
            {
                agent.stoppingDistance = 0f;
                return;
            }
            if (refreshed || blockAction == RangedBlockAction.Attack)
            {
                ShotBlock block = CombatPhysics.ClassifyShot(actor, Target, out CharacterActor blocker);
                blockAction = RangedOcclusionRules.Decide(block, blocker != null && CanStrikeFromHere(blocker));
                if (blockAction == RangedBlockAction.SwitchToBlocker)
                {
                    SwitchTarget(blocker, false);
                    blockAction = RangedBlockAction.Attack;
                    return;
                }
            }
            if (blockAction == RangedBlockAction.CloseIn) agent.stoppingDistance = 0f;
        }

        /// <summary>지금 사선 막힘에 따라 하고 있는 일(COMBAT-12, 검증용).</summary>
        public RangedBlockAction BlockAction => blockAction;

        static readonly MeleeSlotBoard<UnitCombatAI> meleeSlotBoard = new MeleeSlotBoard<UnitCombatAI>();
        static readonly System.Func<UnitCombatAI, object, bool> ClaimStillValid = (unit, target) =>
            unit != null && unit.isActiveAndEnabled && ReferenceEquals(unit.Target, target)
            && unit.actor != null && unit.actor.Health != null && !unit.actor.Health.IsDead;
        readonly System.Collections.Generic.List<int> slotOrder = new System.Collections.Generic.List<int>();
        NavMeshPath slotPath;
        const int SlotPathTries = 3;

        /// <summary>근접 자리 표에서 이 유닛이 지금 대상에 맡은 자리(링, 번호). 없으면 false(검증용).</summary>
        public bool TryGetMeleeSlot(out int ring, out int index) => meleeSlotBoard.TryGet(this, Target, out ring, out index);

        // 대상에게 가는 길을 적용한다. path에는 대상 중심까지의 길이 이미 들어 있다(CanReach/CanReachInHold).
        // 근접 유닛은 대상 둘레의 자리로 가고(COMBAT-12), 쓸 자리가 없으면 예전처럼 중심으로 간다.
        void ApplyApproachPath(bool holdLimited)
        {
            int maxSlotTries = AILodRules.MeleeSlotTries(lodTier, SlotPathTries);
            if (maxSlotTries > 0 && settings.MeleeSlots && actor.Profile.AttackKind == AttackKind.Melee && Target != null
                && TryPathToSlot(holdLimited, maxSlotTries)) return;
            meleeSlotBoard.Release(this);
            agent.SetPath(path);
        }

        bool TryPathToSlot(bool holdLimited, int maxSlotTries)
        {
            Vector3 center = CombatPhysics.ReachPoint(Target);
            float inner = MeleeRingRules.InnerRadius(Target.Profile.Radius, actor.Profile.Radius, settings.MeleeSlotGap,
                actor.Stats.GetValue(StatType.AttackRange));
            bool held = meleeSlotBoard.TryGet(this, Target, out int heldRing, out int heldIndex);
            // 판단 주기마다 링 하나에 길찾기는 몇 번만 한다(여러 근접 유닛이 동시에 판단해도 부담이 작게).
            for (int ring = MeleeRingRules.InnerRing; ring <= MeleeRingRules.OuterRing; ring++)
            {
                int tries = 0;
                float radius = MeleeRingRules.RingRadius(ring, inner, settings.MeleeOuterRingOffset);
                int count = MeleeRingRules.SlotCount(radius, settings.MeleeSlotSpacing);
                // 맡은 안쪽 자리는 그대로 쓴다. 바깥 자리를 맡았으면 안쪽 자리가 났는지 먼저 본다.
                if (held && heldRing == ring && heldIndex < count && TrySlot(center, radius, ring, heldIndex, count, holdLimited))
                    return true;
                MeleeRingRules.OrderByDistance(transform.position, center, radius, count, slotOrder);
                foreach (int index in slotOrder)
                {
                    if (tries >= maxSlotTries) break;
                    if (held && heldRing == ring && heldIndex == index) continue;
                    if (meleeSlotBoard.IsTaken(Target, ring, index, this, ClaimStillValid)) continue;
                    tries++;
                    if (TrySlot(center, radius, ring, index, count, holdLimited)) return true;
                }
            }
            return false;
        }

        // 맡은 근접 자리로 아직 가는 중인지. 길이 없거나 도착 거리 안이면 아니다.
        bool IsWalkingToMeleeSlot() =>
            settings.MeleeSlots && actor.Profile.AttackKind == AttackKind.Melee
            && meleeSlotBoard.TryGet(this, Target, out _, out _)
            && agent.hasPath && !agent.pathPending && agent.remainingDistance > settings.ArrivalDistance;

        bool TrySlot(Vector3 center, float radius, int ring, int index, int count, bool holdLimited)
        {
            Vector3 point = center + MeleeRingRules.SlotOffset(radius, index, count);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, settings.MeleeSlotSampleDistance, filter)
                || Mathf.Abs(hit.position.y - center.y) > settings.MeleeSlotLevelTolerance) return false;
            if (!AILodRules.UsesFullMeleeSlotPath(lodTier))
            {
                // 중경(LOD 1)에서는 대상까지의 경로(path)가 이미 확인되었으므로 추가 CalculatePath 없이 자리를 배정한다(PERF-ai-lod).
                if (holdLimited && settings.HoldMaxDescent > 0f && hit.position.y < HoldPost.y - settings.HoldMaxDescent)
                    return false;
                meleeSlotBoard.Take(this, Target, ring, index);
                agent.SetDestination(hit.position);
                return true;
            }
            if (slotPath == null) slotPath = new NavMeshPath();
            Scheduler.RecordPathQuery(Time.frameCount);
            if (!agent.CalculatePath(hit.position, slotPath) || slotPath.status != NavMeshPathStatus.PathComplete) return false;
            if (holdLimited && settings.HoldMaxDescent > 0f
                && !HoldDescentRule.StaysAbove(slotPath.corners, HoldPost.y - settings.HoldMaxDescent)) return false;
            meleeSlotBoard.Take(this, Target, ring, index);
            agent.SetPath(slotPath);
            return true;
        }

        void OnHitReceived(CharacterActor attacker)
        {
            squadNeighborhoodClear = false;
            if (suppressRetaliation || attacker == null || !actor.isActiveAndEnabled) return;
            if (actor.Health == null || actor.Health.IsDead || actor.IsOnLadder) return;
            if (!agent.enabled || !agent.isOnNavMesh || Directive == UnitDirective.Move) return;
            if (!actor.IsEnemy(attacker) || !CombatPhysics.CanTarget(actor, attacker)) return;
            if (attacker.Character != null && lodTier != AILodTier.Lod0)
            {
                lodTier = AILodTier.Lod0;
                ApplyLodAvoidance();
            }

            bool holdMode = Directive == UnitDirective.Hold;
            bool chases = holdMode && HoldChaseRadius > 0f;
            bool holdDefault = holdMode && (CanStrikeFromHere(attacker) || (chases && IsNearPost(attacker) && CanReachInHold(attacker)));
            bool holdRetaliation = holdMode && !holdDefault && IsWithinRetaliationLeash(attacker) && CanReachInHold(attacker);
            if (holdMode && !holdDefault && !holdRetaliation) return;
            if (!holdMode && !CanStrikeFromHere(attacker) && !CanReach(attacker)) return;

            if (!ShouldSwitchTarget(Target, attacker)) return;
            SwitchTarget(attacker, holdRetaliation);
            decisionRemaining = 0f;
        }

        bool ShouldSwitchTarget(CharacterActor current, CharacterActor candidate)
        {
            if (current == null) return true;
            if (ReferenceEquals(current, candidate)) return false;
            if (targetLockRemaining > 0f) return false;
            // 이미 사거리 안에서 교전 중일 때는 다른 원거리 피격에 타겟을 바꾸지 않는다. 근접 공격만 예외 허용.
            if (CanStrikeFromHere(current))
            {
                if (candidate.Profile.AttackKind != AttackKind.Melee) return false;
                return CanStrikeFromHere(candidate) || PriorityOf(candidate) < PriorityOf(current);
            }
            return CanStrikeFromHere(candidate) || PriorityOf(candidate) < PriorityOf(current);
        }

        // 대상 비교 값: 제곱 거리에 보스 선호 배율과 원거리 위협 배율(COMBAT-14)을 곱한다. 둘 다 없으면 제곱 거리 그대로다.
        float PriorityOf(CharacterActor other) => ScoreOf(other, SqrDistanceTo(other));

        float ScoreOf(CharacterActor other, float sqrDistance) => RangedTargetPriorityRules.Score(sqrDistance,
            TargetDistanceScale != null ? TargetDistanceScale(other) : 1f, ThreatFactorOf(other));

        void SwitchTarget(CharacterActor next, bool isHoldRetaliation)
        {
            Target = next;
            retaliatingTarget = isHoldRetaliation ? next : null;
            targetLockRemaining = settings.TargetSwitchCooldown;
            returningToPost = false;
            clearShotDirty = true;
        }

        float SqrDistanceTo(CharacterActor other) => (other.transform.position - transform.position).sqrMagnitude;

        void Face(CharacterActor target)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), settings.TurnSpeed * Time.deltaTime);
        }

        // 공중 대상은 몸통 아래 바닥 투영점까지의 길을 찾는다.
        bool CanReach(CharacterActor target)
        {
            Scheduler.RecordPathQuery(Time.frameCount);
            return agent.CalculatePath(CombatPhysics.ReachPoint(target), path) && path.status == NavMeshPathStatus.PathComplete;
        }

        // 중지 중 걸어서 닿는지. 자리보다 HoldMaxDescent 이상 아래로 내려가는 길은 닿지 않는 것으로 본다.
        // 성벽 위에서 중지한 부대가 계단과 무너진 틈으로 성 밖까지 쫓아 나가지 않게 한다(BUG-wall-archer-leaves-wall).
        bool CanReachInHold(CharacterActor target) => CanReach(target)
            && (settings.HoldMaxDescent <= 0f || HoldDescentRule.StaysAbove(path.corners, HoldPost.y - settings.HoldMaxDescent));

        // 움직이지 않고 지금 자리에서 닿는 적인지. 방향은 돌려서 맞추므로 따지지 않는다.
        bool CanStrikeFromHere(CharacterActor target) => actor.IsEnemy(target)
            && CombatPhysics.IsInRange(actor, target) && CombatPhysics.HasClearShot(actor, target);

        // 지금 자리에서 쏠/칠 수 있는 적 중 점수(거리 × 원거리 위협 배율)가 가장 낮은 적. 보스 선호 배율은 쓰지 않는다(예전과 같게).
        // belowFactor를 주면 위협 배율이 그보다 확실히 작은 적만 본다(COMBAT-14 더 위협적인 대상 찾기).
        CharacterActor FindTargetInReach(float belowFactor = float.PositiveInfinity)
        {
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            bool onlyHigher = !float.IsPositiveInfinity(belowFactor);
            Vector3 position = transform.position;
            Vector3 aim = actor.AimPoint;
            // 높은 곳 보너스까지 더한 가장 긴 사거리로 거른다. 실제 사거리는 뒤의 정밀 판정이 대상별로 다시 잰다.
            float range = CombatPhysics.MaximumAttackRange(actor);
            CombatSpatialIndex.QueryEnemies(actor.Team, position, range, spatialCandidates);
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                if (candidate.Body == null) continue;
                // AABB는 실제 콜라이더를 포함하므로 범위 밖 후보만 안전하게 제외한다.
                // 큰/오프셋 콜라이더는 통과시키고 기존 정밀 판정과 시야 검사를 그대로 적용한다.
                // 공중 대상의 근접 사거리는 몸통이 아니라 바닥 투영점 기준이므로 이 예비 검사를 건너뛴다.
                if (candidate.Airborne == null && candidate.Body.bounds.SqrDistance(aim) > range * range) continue;
                float factor = ThreatFactorOf(candidate);
                if (onlyHigher && !RangedTargetPriorityRules.IsHigherThreat(belowFactor, factor)) continue;
                float distance = RangedTargetPriorityRules.Score((candidate.transform.position - position).sqrMagnitude, 1f, factor);
                if (distance >= nearestDistance || !CanStrikeFromHere(candidate)) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        CharacterActor FindTarget()
        {
            float detection = settings.DetectionRange * settings.DetectionRange;
            topCandidates.Reset(CurrentCandidateLimit());
            CombatSpatialIndex.QueryEnemies(actor.Team, transform.position, settings.DetectionRange, spatialCandidates);
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= detection) continue;
                // 선호·위협 배율이 없으면 score는 거리 그대로라 예전과 같은 대상을 고른다.
                float score = ScoreOf(candidate, distance);
                if (score >= topCandidates.WorstScore || !actor.IsEnemy(candidate)
                    || !CombatPhysics.CanTarget(actor, candidate)) continue;
                topCandidates.TryAdd(candidate, score);
            }
            for (int i = 0; i < topCandidates.Count; i++)
            {
                CharacterActor candidate = topCandidates[i];
                if (CanReach(candidate)) return candidate;
            }
            // 상위 K명 모두가 섬이나 끊긴 벽 너머에 있을 때는 나머지 후보 중 가장 점수가 좋은 도달 가능 대상을 찾는다.
            if (topCandidates.Count == topCandidates.Capacity)
            {
                float floor = topCandidates.WorstScore;
                CharacterActor fallback = null;
                float best = float.PositiveInfinity;
                for (int i = 0; i < spatialCandidates.Count; i++)
                {
                    CharacterActor candidate = spatialCandidates[i];
                    float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                    if (distance >= detection) continue;
                    float score = ScoreOf(candidate, distance);
                    if (score < floor || score >= best || !actor.IsEnemy(candidate)
                        || !CombatPhysics.CanTarget(actor, candidate)) continue;
                    bool alreadyChecked = false;
                    for (int k = 0; k < topCandidates.Count; k++)
                        if (ReferenceEquals(topCandidates[k], candidate)) { alreadyChecked = true; break; }
                    if (alreadyChecked || !CanReach(candidate)) continue;
                    best = score;
                    fallback = candidate;
                }
                return fallback;
            }
            return null;
        }

        void OnDied()
        {
            fallingBack = false;
            ReleaseOpening();
            meleeSlotBoard.Release(this);
            Target = null;
            retaliatingTarget = null;
            agent.enabled = false;
        }

        void OnDisable()
        {
            GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Interrupt();
            actor.Died -= OnDied;
            actor.HitReceivedFrom -= OnHitReceived;
            fallingBack = false;
            ReleaseOpening();
            meleeSlotBoard.Release(this);
            Target = null;
            retaliatingTarget = null;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }
    }
}
