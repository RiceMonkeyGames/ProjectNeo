using ProjectNeo.Combat;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Siege
{
    public enum SiegeAttackerRole
    {
        Ladder = 0,      // 들어갈 길이 없으면 사다리를 세우고 오른다.
        GateBreaker = 1, // 들어갈 길이 없으면 성문만 공격한다. 사다리는 타지 않는다.
        Archer = 2,      // 아래에서 성벽 위 아군을 쏜다.
        WallBreaker = 3, // 선택 전략으로 성벽 밑단을 공격하고 열린 길로 진입한다.
        Flyer = 4,       // 성벽을 넘어 날아가 아군 옆에 떠서 공격한다(FlyerBrain).
    }

    /// <summary>
    /// 성을 공격하는 적 하나의 판단. 걸어서 닿는 아군이 있으면(뚫린 곳, 무너진 성문, 이미 오른 성벽 위)
    /// 기존 자동 전투에 맡긴다. 아군은 없고 걸어서 닿는 대포가 있으면(성벽 위에 올라온 경우 등) 대포를 부순다(SIEGE-02).
    /// 둘 다 없으면 종류에 따라 사다리·성문·사격을 고른다.
    /// 이동과 공격 자체는 UnitCombatAI의 지시(Engage·Hold·Move)로 실행한다.
    /// </summary>
    [RequireComponent(typeof(UnitCombatAI), typeof(CharacterActor), typeof(NavMeshAgent))]
    public sealed class SiegeAttackerBrain : SiegeAttacker, IAnimationCueSource, ITargetThreatSource
    {
        enum Mode { Deciding, Assault, ToLadder, Raising, Climbing, Falling, ToGate, Shooting, Retreating, ToWall, ToCannon }

        [SerializeField] SiegeAttackerRole role;
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        [SerializeField] SiegeLadder[] ladders = new SiegeLadder[0];
        // 성 바깥을 향하는 격자 방향. 성문 앞과 사격 위치를 잡을 때 쓴다.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        UnitCombatAI ai;
        NavMeshAgent agent;
        UnitFallController fall;
        NavMeshPath path;
        WallBreakerAttack wallBreaker;
        readonly TargetCandidateBuffer<CharacterActor> reachableCandidates = new TargetCandidateBuffer<CharacterActor>(4);
        readonly System.Collections.Generic.List<CharacterActor> spatialCandidates =
            new System.Collections.Generic.List<CharacterActor>(64);
        public static readonly SiegeReachabilityCache ReachabilityCache = new SiegeReachabilityCache();
        static int nextAttackerStagger;
        bool attackerStaggerApplied;
        Mode mode = Mode.Deciding;

        // 성 안팎 판정(SIEGE-15). 모든 공격자가 같은 성을 보므로 함께 쓴다.
        static readonly CastleFootprint footprint = new CastleFootprint();
        static ConstructionWorld footprintWorld;
        // 근접 반경 때문에 돌격 중인지(풀림 반경을 쓴다), 궁수가 돌파구로 들어가기로 했는지(SIEGE-15).
        bool leashAssault;
        bool archerCommitted;
        // 궁수가 쏠 대상 없이 지낸 시작 시각과, 이 공격자가 돌파구가 열린 것을 처음 본 시각. 없으면 -1.
        float archerIdleSince = -1f;
        float breachSeenSince = -1f;
        // 궁수가 마지막으로 쏠 수 있던 수비병. 재장전하느라 웅크리면 잠깐 기다려 준다(SIEGE-15).
        CharacterActor lastShotTarget;
        // 판단을 바로 다시 하게 만드는 길찾기·사다리 변화(SIEGE-15, AI LOD로 판단 주기가 길어졌을 때).
        int seenNavigationRevision = -1;
        int seenLadderMask = -1;

        public static void ResetStaggerCounter()
        {
            nextAttackerStagger = 0;
            ReachabilityCache.Invalidate();
            footprintWorld = null;
        }
        SiegeLadder ladder;
        // 사다리 고르기(SIEGE-17): 후보별 점수·확인 여부, 막혀서 포기한 시각, 길찾기로 닿는지와 그 결과를 구한 길찾기 판.
        float[] ladderScores;
        bool[] ladderChecked;
        float[] ladderAvoidUntil;
        int[] ladderReachKeys;
        bool[] ladderReachable;
        readonly LadderStallTracker ladderStall = new LadderStallTracker();
        float lastRaiseProgress;
        int seenToppleCount;
        PlacedStructure gate;
        Collider[] gateBodies;
        PlacedSiegeWeapon cannon;
        // 이 높이 이상이면 성벽 위에 올라온 것으로 본다.
        const float WallTopMinimumHeight = 1f;
        Vector3 climbStart;
        float climbProgress;
        float think;
        // 성가퀴에 가려진 궁수의 자리 판단과 고른 옆 사격 자리(SIEGE-10).
        ArcherCoverPlan cover;
        Vector3 coverSpot;
        bool archerHasShot;
        public override SiegeAttackerRole Role => role;
        bool Shooting => role == SiegeAttackerRole.Archer && mode == Mode.Shooting;
        /// <summary>
        /// 궁수가 지금 서 있는 자리에서 쏠 대상이 있어 멈춰 쏘는 중인지. 애니메이션(조준·발사 자세)을 붙일 때 읽는다.
        /// </summary>
        public bool IsArcherFiring => Shooting && archerHasShot;
        /// <summary>궁수가 성가퀴에 가려 옆으로 비켜선 사격 자리로 가거나 그 자리에 있는지(SIEGE-10). 검증과 애니메이션용.</summary>
        public bool IsArcherRepositioning => Shooting && cover != null && cover.HasSpot;
        /// <summary>
        /// 궁수가 쏠 곳도 옮길 곳도 없어 제자리에서 겨눈 채 기다리는지(SIEGE-10). 애니메이션에서 활을 당긴 채 기다리는 자세(Hold)를 붙인다.
        /// </summary>
        public bool IsArcherWaiting => Shooting && cover != null && cover.IsWaiting;
        // 애니메이션 신호(ART-04): 겨누는 대상은 UnitCombatAI의 대상을 그대로 쓰고, 사격 중이거나 기다리는 궁수는 활을 당긴 채 둔다.
        CharacterActor IAnimationCueSource.AimTarget => null;
        bool IAnimationCueSource.HoldsDrawnBow => IsArcherFiring || IsArcherWaiting;
        public bool IsClimbing => mode == Mode.Climbing;
        /// <summary>
        /// 원거리 유닛의 위협 우선순위에 쓰는 종류(COMBAT-14). 사다리를 세우거나 오르면 사다리 위협,
        /// 성문·성벽·수성 무기를 부수러 가거나 부수면 공성 위협이다.
        /// </summary>
        public TargetThreat Threat => mode switch
        {
            Mode.Raising or Mode.Climbing => TargetThreat.LadderClimber,
            Mode.ToGate or Mode.ToWall or Mode.ToCannon => TargetThreat.SiegeBreaker,
            _ => TargetThreat.None,
        };
        public override bool IsAssaulting => mode == Mode.Assault;
        /// <summary>
        /// 돌격 중이거나, 돌파구가 열린 것을 보고 바깥에서 지원 사격하는 궁수(SIEGE-15). 공성 수단을 잃었는지(후퇴) 볼 때 쓴다.
        /// </summary>
        public override bool HasEntryRoute => IsAssaulting
            || (role == SiegeAttackerRole.Archer && mode == Mode.Shooting && breachSeenSince >= 0f);
        /// <summary>지금 부수러 가는 수성 무기. 없으면 null.</summary>
        public PlacedSiegeWeapon CannonTarget => mode == Mode.ToCannon ? cannon : null;
        public override bool HasDecided => mode != Mode.Deciding;
        public override bool IsRetreating => retreatRequested && !Fled;
        public override bool IsMidAction => agent == null || !agent.enabled || !agent.isOnNavMesh || (fall != null && fall.IsFalling);
        bool retreatRequested;
        float retreatRemaining;

        /// <summary>판 바깥으로 물러나게 한다. 오르거나 떨어지는 중이면 그 동작이 끝난 뒤 물러난다.</summary>
        public override void Retreat()
        {
            if (Fled || Actor.Health == null || Actor.Health.IsDead) return;
            retreatRequested = true;
        }

        public void Configure(SiegeAttackerRole attackerRole, ConstructionWorld owner, SiegeSettings siegeSettings,
            SiegeLadder[] ladderSpots, Vector2Int outwardDirection)
        {
            role = attackerRole; world = owner; settings = siegeSettings; ladders = ladderSpots; outward = outwardDirection;
            ladderScores = null;
        }

        /// <summary>프리팹으로 만든 적을 씬의 성과 사다리에 연결한다. 활성화하기 전에 부른다.</summary>
        public override void Bind(ConstructionWorld owner, SiegeLadder[] ladderSpots, Vector2Int outwardDirection,
            SiegeSettings battleSettings = null)
        {
            world = owner; ladders = ladderSpots; outward = outwardDirection;
            ladderScores = null;
            if (battleSettings != null) settings = battleSettings;
            GetComponent<WallBreakerAttack>()?.Bind(owner);
        }

        void Awake()
        {
            Actor = GetComponent<CharacterActor>();
            Actor.ThreatSource = this;
            ai = GetComponent<UnitCombatAI>();
            agent = GetComponent<NavMeshAgent>();
            fall = GetComponent<UnitFallController>();
            path = new NavMeshPath();
            wallBreaker = GetComponent<WallBreakerAttack>();
            wallBreaker?.Bind(world);
        }

        void OnEnable()
        {
            Actor.Died += OnDied;
            Actor.HitReceivedFrom += OnHitReceived;
        }

        void OnDisable()
        {
            Actor.Died -= OnDied;
            Actor.HitReceivedFrom -= OnHitReceived;
            ai?.SetHoldChaseSuppressed(false);
            LeaveLadder();
            wallBreaker?.Clear();
            cannon = null;
        }

        Vector3 OutwardWorld => world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;

        void Update()
        {
            if (Actor.Health == null || Actor.Health.IsDead) return;
            if (mode == Mode.Climbing) { UpdateClimb(); return; }
            if (mode == Mode.Falling)
            {
                if (fall != null && fall.IsFalling) return;
                mode = Mode.Deciding;
                think = 0f;
            }
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (retreatRequested) { UpdateRetreat(); return; }
            think -= Time.deltaTime;
            if (mode == Mode.ToWall && wallBreaker != null && wallBreaker.TargetInvalidated) think = 0f;
            if (mode == Mode.ToGate && (gate == null || gate.Health == null || gate.Health.IsDead)) think = 0f;
            WakeOnSiegeChange();
            if (think <= 0f)
            {
                // AI LOD(PERF-ai-lod)에 맞춰 먼 적은 공성 판단도 드물게 한다(SIEGE-15). 큰 변화는 WakeOnSiegeChange가 바로 깨운다.
                float interval = settings.AttackerDecisionInterval * AILodRules.IntervalMultiplier(ai.LodTier);
                if (!attackerStaggerApplied)
                {
                    attackerStaggerApplied = true;
                    int idx = CharacterActor.ActiveActors.Count <= 2 ? 0 : nextAttackerStagger++;
                    float offset = AIDecisionScheduler.InitialOffset(idx, 8, interval);
                    if (offset > 0f) interval = offset;
                }
                think = interval;
                Decide();
            }
            if (mode == Mode.ToCannon && (cannon == null || cannon.IsRemoved)) think = 0f;
            if (mode == Mode.ToGate) StrikeGate();
            else if (mode == Mode.ToCannon) StrikeCannon();
            else if (mode == Mode.ToWall) wallBreaker?.Tick();
            else if (mode == Mode.Raising && ladder != null) Face(-ladder.OutwardWorld);
        }

        // 길찾기 메시가 바뀌거나(성문·성벽 파괴) 사다리가 서거나 쓰러지면 판단 주기를 기다리지 않고 다시 판단한다(SIEGE-15).
        void WakeOnSiegeChange()
        {
            if (world == null) return;
            int navigation = world.NavigationRevision;
            int ladderMask = CurrentLadderMask();
            if (navigation == seenNavigationRevision && ladderMask == seenLadderMask) return;
            bool first = seenNavigationRevision < 0;
            seenNavigationRevision = navigation;
            seenLadderMask = ladderMask;
            if (!first) think = 0f;
        }

        // 맞으면 AI LOD로 길어진 판단 주기를 기본 주기까지 줄여, 근접 반경 안의 아군에게 늦지 않게 반응한다(SIEGE-15).
        // 화살 한 발마다 판단하지는 않는다(대규모 전투 비용).
        void OnHitReceived(CharacterActor attacker)
        {
            if (attacker != null && mode != Mode.Climbing && settings != null)
                think = Mathf.Min(think, settings.AttackerDecisionInterval);
        }

        void Decide()
        {
            // 지형을 반영하는 동안에는 길 판단이 틀릴 수 있으므로 하던 일을 계속한다.
            if (world.IsRebuilding || world.RebuildPending) return;
            if (EvaluateAssault(out CharacterActor nearest, out bool viaLeash))
            {
                leashAssault = viaLeash;
                if (mode != Mode.Assault)
                {
                    LeaveLadder();
                    gate = null;
                    cannon = null;
                    wallBreaker?.Clear();
                    ai.SetRetaliationSuppressed(false);
                    ai.SetHoldChaseSuppressed(false);
                    mode = Mode.Assault;
                    ai.SetDirective(UnitDirective.Engage, nearest.transform.position);
                }
                // 감지 범위 밖이면 Engage는 제자리에서 기다리므로, 싸울 대상이 없는 동안 가장 가까운
                // 닿는 아군 쪽으로 계속 나아간다. 성문이 부서진 채 시작한 다음 스테이지에서 먼 들판의 적이 서 있던 문제.
                else if (ai.Target == null) ai.SetDirective(UnitDirective.Engage, nearest.transform.position);
                return;
            }
            leashAssault = false;
            // 근접 반경의 아군이 멀어졌고 돌파구도 없으면 공성 임무로 돌아간다(SIEGE-15).
            // 돌격 때의 교전(Engage) 지시가 남아 계속 쫓지 않게 먼저 멈춘다. 아래 역할 판단이 새 지시를 준다.
            if (mode == Mode.Assault)
            {
                mode = Mode.Deciding;
                ai.SetDirective(UnitDirective.Hold);
            }
            // 걸어서 닿는 아군이 없을 때만 대포를 노린다(Q-21). 궁수는 기존 사격을 유지한다.
            if (role != SiegeAttackerRole.Archer && DecideCannon())
            {
                ai.SetRetaliationSuppressed(true);
                return;
            }
            if (mode == Mode.ToCannon) { cannon = null; mode = Mode.Deciding; }
            switch (role)
            {
                case SiegeAttackerRole.Ladder:
                    ai.SetRetaliationSuppressed(true);
                    DecideLadder();
                    break;
                case SiegeAttackerRole.GateBreaker:
                    ai.SetRetaliationSuppressed(true);
                    DecideGate();
                    break;
                case SiegeAttackerRole.WallBreaker:
                    ai.SetRetaliationSuppressed(true);
                    mode = Mode.ToWall;
                    if (wallBreaker != null) wallBreaker.Decide();
                    else if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                    break;
                default:
                    ai.SetRetaliationSuppressed(false);
                    DecideArcher();
                    break;
            }
        }

        /// <summary>
        /// 돌격 판단과 같은 기준으로 닿는 아군이 있는지(SIEGE-15). 성 안·성벽 위면 걸어서 닿는 아무 아군, 성 밖 땅이면
        /// 돌파구나 근접 반경 안의 성 밖 아군만 센다. 그래서 멀리 나온 아군 하나 때문에 후퇴도 돌격도 하지 않는 일이 없다.
        /// </summary>
        public override bool CanReachAnyEnemy()
        {
            CastleFootprint castle = Footprint();
            if (castle == null || !castle.HasCastle) return AnyReachableEnemy(out _);
            Vector3 here = transform.position;
            if (castle.IsInsideCastle(CellOf(here)) || HeightAboveGround(here) >= WallTopMinimumHeight)
                return AnyReachableEnemy(out _);
            float leash = SiegeAssaultRules.LeashRadius(mode == Mode.Assault && leashAssault,
                settings.AssaultLeashRadius, settings.AssaultLeashReleaseRadius);
            return TryReachableSortie(castle, here, leash, out _) || TryReachableBreach(castle, here, out _);
        }

        /// <summary>판단을 멈추고 걸어서 출구로 간다. 길이 없으면 제자리에 선다.</summary>
        public override Vector3 BeginExit(Vector3 exit, float searchRadius)
        {
            Vector3 target = transform.position;
            if (NavMesh.SamplePosition(exit, out NavMeshHit hit, searchRadius, NavMesh.AllAreas)
                && agent.CalculatePath(hit.position, path) && path.corners.Length > 0)
                target = path.corners[path.corners.Length - 1];
            enabled = false;
            ai.SetDirective(UnitDirective.Move, target);
            return target;
        }

        CastleFootprint Footprint()
        {
            if (world == null || world.Layout == null) return null;
            if (footprintWorld != world)
            {
                footprintWorld = world;
                footprint.Invalidate();
            }
            footprint.Refresh(world.Layout, world.Grid, world.LayoutRevision, outward);
            return footprint;
        }

        Vector2Int CellOf(Vector3 position) => world.Layout.WorldToCell(world.LocalPoint(position));

        float HeightAboveGround(Vector3 position) => position.y - world.transform.position.y;

        bool IsBreachTarget(CastleFootprint castle, CharacterActor candidate)
        {
            Vector3 position = candidate.transform.position;
            return SiegeAssaultRules.IsBreachTarget(castle.IsWithinCastle(CellOf(position)),
                HeightAboveGround(position), WallTopMinimumHeight);
        }

        /// <summary>
        /// 돌격할지와 그 대상(SIEGE-15). 성 안이나 성벽 위에 이미 있으면 걸어서 닿는 아무 아군에게나 돌격한다.
        /// 성 밖 땅이면 근접 반경 안의 걸어서 닿는 아군, 또는 돌파구(성 안·성벽 위 아군에게 닿는 길)가 있을 때만 돌격한다.
        /// <paramref name="viaLeash"/>는 근접 반경 때문에 돌격하는지다.
        /// </summary>
        bool EvaluateAssault(out CharacterActor target, out bool viaLeash)
        {
            target = null;
            viaLeash = false;
            CastleFootprint castle = Footprint();
            if (castle == null || !castle.HasCastle) return AnyReachableEnemy(out target);
            Vector3 here = transform.position;
            bool inside = castle.IsInsideCastle(CellOf(here)) || HeightAboveGround(here) >= WallTopMinimumHeight;
            if (inside)
            {
                archerCommitted = false;
                return AnyReachableEnemy(out target);
            }

            float leash = SiegeAssaultRules.LeashRadius(mode == Mode.Assault && leashAssault,
                settings.AssaultLeashRadius, settings.AssaultLeashReleaseRadius);
            if (TryReachableSortie(castle, here, leash, out target))
            {
                viaLeash = true;
                return true;
            }

            float now = Time.time;
            bool breach = TryReachableBreach(castle, here, out CharacterActor breachTarget);
            if (!breach)
            {
                breachSeenSince = -1f;
                archerCommitted = false;
                return false;
            }
            if (breachSeenSince < 0f) breachSeenSince = now;
            // 궁수가 쏠 대상 없이 지낸 시간은 돌파구가 열린 뒤부터만 센다.
            float idle = archerIdleSince < 0f ? 0f : now - Mathf.Max(archerIdleSince, breachSeenSince);
            if (!SiegeAssaultRules.ShouldAssault(role, false, false, true, false, archerCommitted, idle,
                    settings.ArcherBreachAdvanceDelay))
                return false;
            if (role == SiegeAttackerRole.Archer) archerCommitted = true;
            target = breachTarget;
            return true;
        }

        /// <summary>
        /// 근접 반경 안에서 걸어서 닿는, 성 밖 땅의 아군(성 밖으로 나온 아군). 돌파구 대상 아군은 <see cref="TryReachableBreach"/>가 본다.
        /// 길찾기는 가까운 몇 명만 한다(AI LOD 등급에 따라 4·2·1명).
        /// </summary>
        bool TryReachableSortie(CastleFootprint castle, Vector3 here, float radius, out CharacterActor target)
        {
            target = null;
            if (radius <= 0f) return false;
            CombatSpatialIndex.QueryEnemies(Actor.Team, here, radius, spatialCandidates);
            if (spatialCandidates.Count == 0) return false;
            reachableCandidates.Reset(AILodRules.PathCandidateLimit(ai.LodTier, 4));
            float sqrRadius = radius * radius;
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                if (!Actor.IsEnemy(candidate) || IsBreachTarget(castle, candidate)) continue;
                float distance = (candidate.transform.position - here).sqrMagnitude;
                if (distance > sqrRadius || distance >= reachableCandidates.WorstScore) continue;
                reachableCandidates.TryAdd(candidate, distance);
            }
            for (int i = 0; i < reachableCandidates.Count; i++)
            {
                CharacterActor candidate = reachableCandidates[i];
                UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                if (agent.CalculatePath(candidate.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    target = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 걸어서 닿는 돌파구 대상 아군(성 안·성벽 위) 중 가장 가까운 것. 가까운 상위 후보부터 길을 구하고,
        /// '돌파구 없음' 결과는 길찾기 메시·사다리·아군 변화가 없는 동안 성 밖 공격자끼리 함께 쓴다(PERF-siege-ai-scale).
        /// </summary>
        bool TryReachableBreach(CastleFootprint castle, Vector3 here, out CharacterActor target)
        {
            target = null;
            int navRev = world.NavigationRevision;
            int ladderMask = CurrentLadderMask();
            int actorRev = CharacterActor.RegistryRevision;
            float now = Time.time;
            float ttl = settings.ReachabilityCacheTtl;
            if (ttl > 0f && ReachabilityCache.HasValidNoBreach(navRev, ladderMask, actorRev, now)) return false;

            var enemies = CombatSpatialIndex.AllEnemiesOf(Actor.Team);
            reachableCandidates.Reset(AILodRules.PathCandidateLimit(ai.LodTier, 4));
            int breachTargets = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                CharacterActor candidate = enemies[i];
                if (!Actor.IsEnemy(candidate) || !IsBreachTarget(castle, candidate)) continue;
                breachTargets++;
                float distance = (candidate.transform.position - here).sqrMagnitude;
                if (distance >= reachableCandidates.WorstScore) continue;
                reachableCandidates.TryAdd(candidate, distance);
            }
            for (int i = 0; i < reachableCandidates.Count; i++)
            {
                CharacterActor candidate = reachableCandidates[i];
                UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                if (agent.CalculatePath(candidate.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    target = candidate;
                    return true;
                }
            }
            // 가까운 후보가 모두 막혔으면 나머지 중 닿는 것을 찾는다(예: 가까운 성벽 위 수비병은 막혔지만 무너진 틈 안쪽 수비병에게는 닿음).
            if (reachableCandidates.Count < breachTargets)
            {
                float floor = reachableCandidates.WorstScore;
                float best = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                {
                    CharacterActor candidate = enemies[i];
                    if (!Actor.IsEnemy(candidate) || !IsBreachTarget(castle, candidate)) continue;
                    float distance = (candidate.transform.position - here).sqrMagnitude;
                    if (distance < floor || distance >= best) continue;
                    bool alreadyChecked = false;
                    for (int k = 0; k < reachableCandidates.Count; k++)
                        if (ReferenceEquals(reachableCandidates[k], candidate)) { alreadyChecked = true; break; }
                    if (alreadyChecked) continue;
                    UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                    if (agent.CalculatePath(candidate.transform.position, path)
                        && path.status == NavMeshPathStatus.PathComplete) { target = candidate; best = distance; }
                }
            }
            if (target == null && ttl > 0f) ReachabilityCache.RecordNoBreach(navRev, ladderMask, actorRev, now, ttl);
            return target != null;
        }

        int CurrentLadderMask()
        {
            if (ladders == null) return 0;
            int mask = 0;
            for (int i = 0; i < ladders.Length && i < 31; i++)
                if (ladders[i] != null && ladders[i].HasWall && ladders[i].State.Phase == LadderPhase.Up)
                    mask |= 1 << i;
            return mask;
        }

        /// <summary>걸어서 닿는 적 중 직선거리로 가장 가까운 것. 먼저 가까운 상위 후보를 추린 뒤 경로를 구한다(PERF-ai-query-budget).</summary>
        bool AnyReachableEnemy(out CharacterActor nearest)
        {
            nearest = null;
            Vector3 here = transform.position;
            bool onGround = world != null && Mathf.Abs(here.y - world.transform.position.y) < WallTopMinimumHeight;
            int navRev = world != null ? world.NavigationRevision : 0;
            int ladderMask = CurrentLadderMask();
            int actorRev = CharacterActor.RegistryRevision;
            float now = Time.time;
            float ttl = settings != null ? settings.ReachabilityCacheTtl : 0f;
            if (onGround && ttl > 0f && ReachabilityCache.HasValidGroundUnreachable(navRev, ladderMask, actorRev, now))
            {
                // 성 안 수비대는 막혀 있더라도 공격자 주변(15m) 성 밖에 나와 있는 아군이 있으면 개별 확인한다.
                CombatSpatialIndex.QueryEnemies(Actor.Team, here, 15f, spatialCandidates);
                if (spatialCandidates.Count == 0) return false;
            }

            reachableCandidates.Reset(4);
            var enemies = CombatSpatialIndex.AllEnemiesOf(Actor.Team);
            for (int i = 0; i < enemies.Count; i++)
            {
                CharacterActor candidate = enemies[i];
                if (!Actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - here).sqrMagnitude;
                if (distance >= reachableCandidates.WorstScore) continue;
                reachableCandidates.TryAdd(candidate, distance);
            }
            for (int i = 0; i < reachableCandidates.Count; i++)
            {
                CharacterActor candidate = reachableCandidates[i];
                UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                if (agent.CalculatePath(candidate.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    nearest = candidate;
                    return true;
                }
            }
            if (reachableCandidates.Count == reachableCandidates.Capacity)
            {
                float floor = reachableCandidates.WorstScore;
                float best = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                {
                    CharacterActor candidate = enemies[i];
                    if (!Actor.IsEnemy(candidate)) continue;
                    float distance = (candidate.transform.position - here).sqrMagnitude;
                    if (distance < floor || distance >= best) continue;
                    bool alreadyChecked = false;
                    for (int k = 0; k < reachableCandidates.Count; k++)
                        if (ReferenceEquals(reachableCandidates[k], candidate)) { alreadyChecked = true; break; }
                    if (alreadyChecked) continue;
                    UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                    if (agent.CalculatePath(candidate.transform.position, path)
                        && path.status == NavMeshPathStatus.PathComplete) { nearest = candidate; best = distance; }
                }
            }
            if (onGround && ttl > 0f && nearest == null)
                ReachabilityCache.RecordGroundResult(navRev, ladderMask, actorRev, now, ttl, false);
            return nearest != null;
        }

        // ── 사다리 ──────────────────────────────────────────────

        void DecideLadder()
        {
            float now = Time.time;
            // 붙들던 사다리가 쓰러졌으면 놓고 처음부터 다시 고른다(SIEGE-17). 밑에 몰려 있던 인원이 다른 사다리로 흩어진다.
            if (ladder != null && ladder.State.ToppleCount != seenToppleCount) LeaveLadder();
            SiegeLadder best = ChooseLadder(now);
            if (best == null)
            {
                // 걸어서 닿는 사다리가 하나도 없으면 멈춰 서지 않고 성문으로 간다.
                LeaveLadder();
                DecideGate();
                return;
            }
            if (best != ladder)
            {
                LeaveLadder();
                ladder = best;
                ladder.Join(this);
                seenToppleCount = ladder.State.ToppleCount;
                lastRaiseProgress = ladder.State.RaiseProgress;
                ladderStall.Reset(now, FlatDistance(transform.position, ladder.BasePoint));
                ai.SetDirective(UnitDirective.Move, ladder.BasePoint);
                mode = Mode.ToLadder;
                return;
            }
            if (mode == Mode.Raising && ladder.IsRaiser(this) && ladder.State.Phase != LadderPhase.Up) return;
            float distance = FlatDistance(transform.position, ladder.BasePoint);
            // 다가가지도 못하고 사다리 일(세우기·오르기)도 진행되지 않으면 막힌 것으로 보고 잠시 피한다(SIEGE-17).
            bool advanced = ladder.State.HasClimber || ladder.State.RaiseProgress > lastRaiseProgress + 0.0001f;
            lastRaiseProgress = ladder.State.RaiseProgress;
            ladderStall.Observe(now, distance, advanced);
            if (ladderStall.IsStalled(now, settings.LadderStallSeconds))
            {
                int index = System.Array.IndexOf(ladders, ladder);
                if (index >= 0 && ladderAvoidUntil != null) ladderAvoidUntil[index] = now + settings.LadderAvoidSeconds;
                LeaveLadder();
                think = 0f;
                return;
            }
            if (distance > settings.LadderUseRadius)
            {
                // 밀려났거나 아직 가는 중이다.
                if (ai.Directive != UnitDirective.Move) ai.SetDirective(UnitDirective.Move, ladder.BasePoint);
                mode = Mode.ToLadder;
                return;
            }
            if (ladder.State.Phase == LadderPhase.Up)
            {
                if (ladder.TryBeginClimb(this)) BeginClimb();
                else Wait();
            }
            else if (ladder.TryRaise(this))
            {
                mode = Mode.Raising;
                if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
            }
            else Wait();
        }

        // 한 명이 다 오를 때까지 나머지는 사다리 밑에서 기다린다.
        void Wait()
        {
            mode = Mode.ToLadder;
            if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
        }

        /// <summary>
        /// 점수(거리·몰린 인원·이미 고른 사다리·막혀서 피하는 사다리)가 좋은 순서로, 걸어서 사다리 밑에 닿고
        /// 다 오른 뒤 내려설 성벽 위 자리가 있는 첫 사다리(SIEGE-17). 없으면 null.
        /// </summary>
        SiegeLadder ChooseLadder(float now)
        {
            if (ladders == null || ladders.Length == 0) return null;
            EnsureLadderBuffers();
            int count = ladders.Length;
            for (int i = 0; i < count; i++)
            {
                SiegeLadder candidate = ladders[i];
                ladderChecked[i] = false;
                if (candidate == null || !candidate.HasWall) { ladderScores[i] = float.PositiveInfinity; continue; }
                bool current = candidate == ladder;
                ladderScores[i] = LadderChoiceRules.Score(Vector3.Distance(transform.position, candidate.BasePoint),
                    candidate.Crowd - (current ? 1 : 0), current, ladderAvoidUntil[i] > now,
                    settings.LadderCrowdPenalty, settings.LadderAvoidPenalty);
            }
            int key = LadderReachKey();
            int index;
            while ((index = LadderChoiceRules.NextBest(ladderScores, ladderChecked, count)) >= 0)
            {
                ladderChecked[index] = true;
                if (IsLadderReachable(index, key)) return ladders[index];
            }
            return null;
        }

        void EnsureLadderBuffers()
        {
            int count = ladders.Length;
            if (ladderScores != null && ladderScores.Length == count) return;
            ladderScores = new float[count];
            ladderChecked = new bool[count];
            ladderAvoidUntil = new float[count];
            ladderReachKeys = new int[count];
            ladderReachable = new bool[count];
            for (int i = 0; i < count; i++) ladderReachKeys[i] = int.MinValue;
        }

        // 길찾기 메시가 바뀌거나 땅·성벽 위를 오가면 닿는지 다시 구한다. 그 밖에는 사다리마다 한 번만 구한다.
        int LadderReachKey() => world.NavigationRevision * 2 + (HeightAboveGround(transform.position) < WallTopMinimumHeight ? 1 : 0);

        bool IsLadderReachable(int index, int key)
        {
            if (ladderReachKeys[index] == key) return ladderReachable[index];
            SiegeLadder candidate = ladders[index];
            bool reachable = false;
            Vector3 basePoint = candidate.BasePoint, topPoint = candidate.TopPoint;
            if (NavMesh.SamplePosition(basePoint, out NavMeshHit foot, settings.LadderUseRadius, agent.areaMask)
                && Mathf.Abs(foot.position.y - basePoint.y) <= settings.LadderHeightTolerance
                // 다 오른 뒤 내려설 곳이 없으면 오르기만 하고 떨어진다(UpdateClimb과 같은 기준).
                && NavMesh.SamplePosition(topPoint, out NavMeshHit top, 1.5f, NavMesh.AllAreas)
                && Mathf.Abs(top.position.y - topPoint.y) <= settings.LadderHeightTolerance)
            {
                UnitCombatAI.Scheduler.RecordPathQuery(Time.frameCount);
                reachable = agent.CalculatePath(foot.position, path) && path.status == NavMeshPathStatus.PathComplete;
            }
            ladderReachKeys[index] = key;
            ladderReachable[index] = reachable;
            return reachable;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        void BeginClimb()
        {
            mode = Mode.Climbing;
            climbProgress = 0f;
            climbStart = transform.position;
            ai.enabled = false;
            agent.enabled = false;
        }

        void UpdateClimb()
        {
            if (ladder == null) { Drop(); return; }
            if (ladder.State.PushProgress > 0f) { FollowLadderTilt(); return; }
            climbProgress += Time.deltaTime / settings.LadderClimbSeconds;
            FollowLadderTilt();
            Face(-ladder.OutwardWorld);
            if (climbProgress < 1f) return;
            if (!NavMesh.SamplePosition(ladder.TopPoint, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                || Mathf.Abs(hit.position.y - ladder.TopPoint.y) > settings.LadderHeightTolerance)
            {
                Drop();
                return;
            }
            transform.position = hit.position;
            agent.enabled = true;
            ai.enabled = true;
            LeaveLadder();
            ai.SetRetaliationSuppressed(false);
            ai.SetDirective(UnitDirective.Engage);
            mode = Mode.Deciding;
            think = 0f;
        }

        /// <summary>등반 진행도는 유지하면서 현재 사다리 기울기를 따라간다.</summary>
        public void FollowLadderTilt()
        {
            if (ladder == null || mode != Mode.Climbing) return;
            // 대부분의 시간은 사다리를 오르고, 마지막에 성벽 위로 넘어간다.
            const float onLadder = 0.85f;
            Vector3 raisedPosition = climbProgress < onLadder
                ? Vector3.Lerp(climbStart, ladder.EdgePoint, climbProgress / onLadder)
                : Vector3.Lerp(ladder.EdgePoint, ladder.TopPoint, (climbProgress - onLadder) / (1f - onLadder));
            transform.position = ladder.FollowTilt(raisedPosition);
        }

        /// <summary>오르는 중에 사다리가 밀려 쓰러졌다. 그 높이에서 떨어져 낙하 피해를 받는다.</summary>
        public void OnLadderToppled()
        {
            if (mode == Mode.Climbing) Drop();
        }

        void Drop()
        {
            Vector3 fallVelocity = Vector3.zero;
            if (ladder != null && ladder.TryGetFallMotion(transform.position, out Vector3 separatedPosition, out Vector3 outwardVelocity))
            {
                transform.position = separatedPosition;
                fallVelocity = outwardVelocity;
            }
            LeaveLadder();
            ai.enabled = true;
            mode = Mode.Falling;
            if (fall != null) fall.BeginFall(true, fallVelocity);
            else
            {
                // 낙하 부품이 없으면 사다리 밑으로 되돌린다.
                transform.position = climbStart;
                agent.enabled = true;
            }
        }

        void LeaveLadder()
        {
            if (ladder != null) ladder.Leave(this);
            ladder = null;
            ladderStall.Stop();
        }

        // ── 성문 ────────────────────────────────────────────────

        void DecideGate()
        {
            PlacedStructure target = FindGate();
            if (target == null)
            {
                gate = null;
                mode = Mode.Deciding;
                if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                return;
            }
            if (mode != Mode.ToGate || gate != target)
            {
                gate = target;
                gateBodies = gate.GetComponents<Collider>();
                mode = Mode.ToGate;
                ai.SetDirective(UnitDirective.Move, GateFront());
            }
            else if (!GateInReach() && ai.Directive != UnitDirective.Move)
                ai.SetDirective(UnitDirective.Move, GateFront());
        }

        PlacedStructure FindGate()
        {
            foreach (PlacedStructure structure in world.Structures)
                if (structure != null && structure.Placement.Definition.Kind == BuildPieceKind.Gate
                    && structure.Health != null && !structure.Health.IsDead) return structure;
            // 성문 위에서 내려온 조각도 길을 막는 동안 성문 파괴병의 공격 대상이다.
            PlacedStructure nearest = null;
            float distance = float.PositiveInfinity;
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null || structure.Health == null || structure.Health.IsDead) continue;
                BuildPlacement placement = structure.Placement;
                if (placement.Level != 0 || placement.Definition.Kind == BuildPieceKind.Gate) continue;
                bool onGateSite = false;
                for (int x = 0; x < placement.Size.x; x++)
                    for (int z = 0; z < placement.Size.y; z++)
                        onGateSite |= world.Layout.IsGateCell(placement.Cell + new Vector2Int(x, z));
                if (!onGateSite) continue;
                float candidate = (structure.transform.position - transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate; nearest = structure;
            }
            return nearest;
        }

        // 성문 바깥쪽 앞. 한 점에 몰리지 않도록 유닛마다 옆으로 조금씩 벌린다.
        Vector3 GateFront()
        {
            Vector3 forward = OutwardWorld;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float depth = gate.Placement.Size.y * world.Grid.CellSize * Mathf.Abs(outward.y)
                + gate.Placement.Size.x * world.Grid.CellSize * Mathf.Abs(outward.x);
            float spread = ((transform.GetSiblingIndex() & 7) / 7f - 0.5f) * settings.GateSpread;
            Vector3 point = gate.transform.position + forward * (depth * 0.5f + settings.GateStandOffset) + side * spread;
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : point;
        }

        bool GateInReach()
        {
            if (gate == null || gate.Health == null || gate.Health.IsDead || gateBodies == null) return false;
            float range = Actor.Stats.GetValue(StatType.AttackRange);
            Vector3 origin = Actor.AimPoint;
            foreach (Collider body in gateBodies)
                if (body != null && body.enabled
                    && (body.ClosestPoint(origin) - origin).sqrMagnitude <= range * range) return true;
            return false;
        }

        void StrikeGate()
        {
            if (!GateInReach()) return;
            if (ai.Directive == UnitDirective.Move) ai.SetDirective(UnitDirective.Hold);
            Vector3 toward = gate.transform.position - transform.position;
            Face(toward);
            // 유닛 공격과 같은 공격 속도·선딜레이 신호를 쓴다. 성문에는 방어력이 없다.
            if (!Actor.TryAttack()) return;
            gate.ReceiveDamage(BossUnit.StructureDamage(Actor, DamageRules.Calculate(Actor.Stats.GetValue(StatType.Attack), 0f, Actor.Profile.MinimumDamage)));
        }

        // ── 대포(SIEGE-02) ──────────────────────────────────────

        /// <summary>걸어서 닿는 가장 가까운 대포로 간다. 그런 대포가 없으면 false.</summary>
        bool DecideCannon()
        {
            // 성벽 위에 올라온 적만 대포를 찾는다. 성 밖 땅의 적까지 매번 길을 찾지 않게 한다.
            if (world == null || transform.position.y - world.transform.position.y < WallTopMinimumHeight) return false;
            PlacedSiegeWeapon best = null;
            Vector3 bestPoint = default;
            float nearest = float.PositiveInfinity;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                if (weapon == null || weapon.IsRemoved || weapon.Health == null || weapon.Health.IsDead) continue;
                float distance = (weapon.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearest || !TryCannonStand(weapon, out Vector3 point)) continue;
                nearest = distance;
                best = weapon;
                bestPoint = point;
            }
            if (best == null) return false;
            if (mode != Mode.ToCannon || cannon != best)
            {
                LeaveLadder();
                gate = null;
                wallBreaker?.Clear();
                cannon = best;
                mode = Mode.ToCannon;
                if (!CannonInReach()) ai.SetDirective(UnitDirective.Move, bestPoint);
                else if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
            }
            else if (!CannonInReach() && ai.Directive != UnitDirective.Move)
                ai.SetDirective(UnitDirective.Move, bestPoint);
            return true;
        }

        // 대포 옆 길찾기 지점. 지금 자리에서 끝까지 이어지는 길이 있어야 한다.
        bool TryCannonStand(PlacedSiegeWeapon weapon, out Vector3 point)
        {
            point = default;
            if (!NavMesh.SamplePosition(weapon.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = hit.position;
            return true;
        }

        bool CannonInReach() => cannon != null && !cannon.IsRemoved && cannon.Definition != null
            && SiegeWeaponStrikeRules.InReach(transform.position, cannon.transform.position, cannon.Definition.Size,
                Actor.Stats.GetValue(StatType.AttackRange));

        void StrikeCannon()
        {
            if (!CannonInReach()) return;
            if (ai.Directive == UnitDirective.Move) ai.SetDirective(UnitDirective.Hold);
            Face(cannon.transform.position - transform.position);
            // 성문과 같은 공격 속도·선딜레이 신호를 쓴다. 대포에는 방어력이 없다.
            if (!Actor.TryAttack()) return;
            cannon.ReceiveDamage(BossUnit.StructureDamage(Actor, DamageRules.Calculate(Actor.Stats.GetValue(StatType.Attack), 0f, Actor.Profile.MinimumDamage)));
        }

        // ── 후퇴 ────────────────────────────────────────────────

        void UpdateRetreat()
        {
            if (mode != Mode.Retreating)
            {
                LeaveLadder();
                gate = null;
                cannon = null;
                wallBreaker?.Clear();
                mode = Mode.Retreating;
                retreatRemaining = settings.RetreatTimeout;
                if (!TryGetRetreatPoint(out Vector3 point)) { Flee(); return; }
                // 이동 지시는 적을 무시하고 끝까지 간다. 물러나는 동안 싸우지 않는다.
                ai.SetRetaliationSuppressed(true);
                ai.SetDirective(UnitDirective.Move, point);
                return;
            }
            retreatRemaining -= Time.deltaTime;
            // 도착하거나 더 갈 길이 없으면 이동 지시가 중지로 바뀐다. 길이 막혀 오래 걸려도 떠난 것으로 본다.
            if (retreatRemaining <= 0f || ai.Directive != UnitDirective.Move) Flee();
        }

        // 지금 자리에서 성 바깥 방향으로 판 끝까지 간 지점.
        bool TryGetRetreatPoint(out Vector3 point)
        {
            Vector3 local = world.LocalPoint(transform.position);
            float size = world.Grid.CellSize, margin = settings.RetreatEdgeMargin;
            if (outward.x > 0) local.x = world.Grid.Width * size - margin;
            else if (outward.x < 0) local.x = margin;
            else if (outward.y > 0) local.z = world.Grid.Depth * size - margin;
            else local.z = margin;
            local.y = 0f;
            point = world.transform.TransformPoint(local);
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
            point = hit.position;
            return true;
        }

        void Flee()
        {
            Fled = true;
            // 꺼지면 대상 후보 목록에서도 빠진다.
            gameObject.SetActive(false);
        }

        // ── 궁수 ────────────────────────────────────────────────

        void DecideArcher()
        {
            cover ??= settings.CreateArcherCoverPlan();
            float now = Time.time;
            // 돌격·후퇴 등 다른 일을 하다 돌아왔으면 자리 판단을 처음부터 한다.
            if (mode != Mode.Shooting)
            {
                cover.Clear();
                archerIdleSince = -1f;
                lastShotTarget = null;
            }
            mode = Mode.Shooting;
            archerHasShot = false;
            // 바깥에서 지원 사격하는 동안은 가려진 수비병을 쫓아 들어가지 않는다(SIEGE-15).
            ai.SetHoldChaseSuppressed(true);
            // 지금 자리에서 쏠 수 있는 적이 있으면 그대로 서서 쏜다. 사선 검사는 사거리 안의 가까운 몇 명만 한다(AI LOD 등급에 따라 4·2·1명).
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            reachableCandidates.Reset(AILodRules.PathCandidateLimit(ai.LodTier, 4));
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
                if (distance < reachableCandidates.WorstScore && CombatPhysics.IsInRange(Actor, candidate))
                    reachableCandidates.TryAdd(candidate, distance);
            }
            for (int i = 0; i < reachableCandidates.Count; i++)
            {
                CharacterActor candidate = reachableCandidates[i];
                if (!CombatPhysics.HasClearShot(Actor, candidate)) continue;
                cover.Shooting();
                archerHasShot = true;
                archerIdleSince = -1f;
                lastShotTarget = candidate;
                if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                return;
            }
            // 마지막으로 쏘던 수비병이 재장전하느라 성가퀴 뒤에 웅크렸으면(COMBAT-10) 자리를 옮기지 않고 잠깐 겨눈 채 기다린다.
            if (TargetDucked(lastShotTarget))
            {
                if (cover.TargetDucked(now))
                {
                    archerIdleSince = -1f;
                    if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                    return;
                }
            }
            else
            {
                cover.NoDuckedTarget();
                lastShotTarget = null;
            }
            if (archerIdleSince < 0f) archerIdleSince = now;
            if (nearest == null) return;
            // 가장 가까운 적을 사거리 안에 두는, 그 대상이 선 성벽 면의 바깥쪽 자리(SIEGE-15: 옆 성벽·보루도 정면에서 쏜다).
            Vector3 normal = ApproachNormal(nearest);
            float range = Actor.Stats.GetValue(StatType.AttackRange) * settings.ArcherApproachRatio;
            Vector3 point = nearest.transform.position + normal * range;
            point.y = world.transform.position.y;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return;

            // 아직 사거리 밖이면 다가간다. 가려진 시간은 사거리 안에서만 잰다.
            if (!cover.HasSpot && !CombatPhysics.IsInRange(Actor, nearest))
            {
                cover.Approaching();
                MoveArcher(hit.position);
                return;
            }
            // 고른 옆 자리는 지키는 시간 동안, 그 뒤에는 아직 쓸모 있을 때만 지킨다.
            if (cover.HasSpot && cover.KeepSpot(now, cover.IsCommitted(now) || CanShootFrom(coverSpot)))
            {
                MoveArcher(coverSpot);
                return;
            }
            // 사거리 안에서 가려진 채 충분히 기다렸으면 옆 자리를 한 번 찾는다(성가퀴 솟은 부분 뒤의 수비병 등).
            // 한 프레임에 너무 많은 궁수가 동시에 옆자리 탐색(Raycast 다발)을 하지 않도록 프레임 예산을 확인한다(PERF-siege-ai-scale).
            // 멀리 있는 궁수(AI LOD 2)는 옆자리 탐색을 하지 않고 정면 자리에서 기다린다(SIEGE-15).
            if (cover.ShouldSearch(now) && ai.LodTier != AILodTier.Lod2
                && ReachabilityCache.TryConsumeArcherSearchToken(Time.frameCount, settings.MaxArcherRepositionSearchesPerFrame))
            {
                bool found = TryFindFiringPosition(hit.position, normal, out Vector3 spot);
                cover.SearchResult(now, found);
                if (found)
                {
                    coverSpot = spot;
                    MoveArcher(spot);
                    return;
                }
            }
            // 쏠 곳도 옮길 곳도 없으면 좌우로 다니지 않고 제자리에서 겨눈 채 기다린다.
            cover.Wait();
            if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
        }

        void MoveArcher(Vector3 destination)
        {
            if (ai.Directive != UnitDirective.Move || (ai.MoveDestination - destination).sqrMagnitude > 4f)
                ai.SetDirective(UnitDirective.Move, destination);
        }

        /// <summary>
        /// 정면 자리에서 성벽과 나란히 가까운 쪽부터 비켜선 후보 자리를 시험해, 사거리 안의 적이 하나라도 보이는 첫 자리를 고른다.
        /// </summary>
        bool TryFindFiringPosition(Vector3 front, Vector3 normal, out Vector3 position)
        {
            position = front;
            Vector3 along = Vector3.Cross(Vector3.up, normal).normalized;
            for (int step = 1; step <= settings.ArcherSideStepCount; step++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 candidate = front + along * (side * step * settings.ArcherSideStep);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    // 성벽 위나 성 안쪽으로 비켜서지 않는다. 땅 높이의 바깥 자리만 쓴다.
                    if (Mathf.Abs(hit.position.y - world.transform.position.y) > WallTopMinimumHeight) continue;
                    if (!CanShootFrom(hit.position)) continue;
                    position = hit.position;
                    return true;
                }
            return false;
        }

        /// <summary>
        /// 마지막으로 쏘던 수비병이 살아 있고, 사거리 안에서 재장전하느라 웅크렸는지(COMBAT-10, SIEGE-15).
        /// </summary>
        bool TargetDucked(CharacterActor target) =>
            target != null && target.isActiveAndEnabled && target.Health != null && !target.Health.IsDead
            && Actor.IsEnemy(target) && target.IsCrouched && CombatPhysics.IsInRange(Actor, target);

        /// <summary>
        /// 궁수가 설 방향(대상에서 바깥쪽, 월드 단위 벡터). 대상 칸의 바깥으로 열린 면 중 궁수 쪽을 가장 많이 향한 면의 법선,
        /// 없으면 성 중심에서 대상 쪽(궁수 쪽일 때만), 아니면 대상에서 궁수 쪽(SIEGE-15).
        /// </summary>
        Vector3 ApproachNormal(CharacterActor target)
        {
            CastleFootprint castle = Footprint();
            if (castle == null) return OutwardWorld;
            Vector3 targetLocal = world.LocalPoint(target.transform.position);
            Vector3 archerLocal = world.LocalPoint(transform.position);
            int faces = castle.OutwardFaceMask(world.Layout, world.Layout.WorldToCell(targetLocal));
            Vector2 direction = ArcherApproachRules.ApproachDirection(faces,
                new Vector2(targetLocal.x, targetLocal.z), new Vector2(archerLocal.x, archerLocal.z),
                castle.Center, castle.HasCastle, new Vector2(outward.x, outward.y));
            Vector3 normal = world.transform.TransformDirection(new Vector3(direction.x, 0f, direction.y));
            normal.y = 0f;
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : OutwardWorld;
        }

        bool CanShootFrom(Vector3 standPoint)
        {
            Vector3 aim = standPoint + Vector3.up * (Actor.Profile.Height * 0.5f);
            float maxReach = CombatPhysics.MaximumAttackRange(Actor);
            CombatSpatialIndex.QueryEnemies(Actor.Team, standPoint, maxReach, spatialCandidates);
            reachableCandidates.Reset(4);
            for (int i = 0; i < spatialCandidates.Count; i++)
            {
                CharacterActor candidate = spatialCandidates[i];
                if (!Actor.IsEnemy(candidate) || candidate.Body == null) continue;
                float reach = CombatPhysics.AttackRange(Actor, candidate);
                float sqrDist = (candidate.Body.ClosestPoint(aim) - aim).sqrMagnitude;
                if (sqrDist > reach * reach || sqrDist >= reachableCandidates.WorstScore) continue;
                reachableCandidates.TryAdd(candidate, sqrDist);
            }
            for (int i = 0; i < reachableCandidates.Count; i++)
                if (CombatPhysics.HasClearShotFrom(standPoint, Actor, reachableCandidates[i])) return true;
            return false;
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        void OnDied()
        {
            bool climbing = mode == Mode.Climbing;
            LeaveLadder();
            mode = Mode.Deciding;
            // 오르다 죽으면 그 자리에 떠 있지 않고 떨어진다.
            if (climbing && fall != null) fall.BeginFall(false);
        }
    }
}
