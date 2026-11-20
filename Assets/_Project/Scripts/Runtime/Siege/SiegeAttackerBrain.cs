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
    public sealed class SiegeAttackerBrain : SiegeAttacker, IAnimationCueSource
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
        Mode mode = Mode.Deciding;
        SiegeLadder ladder;
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
        public override bool IsAssaulting => mode == Mode.Assault;
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
        }

        /// <summary>프리팹으로 만든 적을 씬의 성과 사다리에 연결한다. 활성화하기 전에 부른다.</summary>
        public override void Bind(ConstructionWorld owner, SiegeLadder[] ladderSpots, Vector2Int outwardDirection,
            SiegeSettings battleSettings = null)
        {
            world = owner; ladders = ladderSpots; outward = outwardDirection;
            if (battleSettings != null) settings = battleSettings;
            GetComponent<WallBreakerAttack>()?.Bind(owner);
        }

        void Awake()
        {
            Actor = GetComponent<CharacterActor>();
            ai = GetComponent<UnitCombatAI>();
            agent = GetComponent<NavMeshAgent>();
            fall = GetComponent<UnitFallController>();
            path = new NavMeshPath();
            wallBreaker = GetComponent<WallBreakerAttack>();
            wallBreaker?.Bind(world);
        }

        void OnEnable() => Actor.Died += OnDied;

        void OnDisable()
        {
            Actor.Died -= OnDied;
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
            if (think <= 0f)
            {
                think = settings.AttackerDecisionInterval;
                Decide();
            }
            if (mode == Mode.ToCannon && (cannon == null || cannon.IsRemoved)) think = 0f;
            if (mode == Mode.ToGate) StrikeGate();
            else if (mode == Mode.ToCannon) StrikeCannon();
            else if (mode == Mode.ToWall) wallBreaker?.Tick();
            else if (mode == Mode.Raising && ladder != null) Face(-ladder.OutwardWorld);
        }

        void Decide()
        {
            // 지형을 반영하는 동안에는 길 판단이 틀릴 수 있으므로 하던 일을 계속한다.
            if (world.IsRebuilding || world.RebuildPending) return;
            if (AnyReachableEnemy(out CharacterActor nearest))
            {
                if (mode != Mode.Assault)
                {
                    LeaveLadder();
                    gate = null;
                    cannon = null;
                    wallBreaker?.Clear();
                    ai.SetRetaliationSuppressed(false);
                    mode = Mode.Assault;
                    ai.SetDirective(UnitDirective.Engage, nearest.transform.position);
                }
                // 감지 범위 밖이면 Engage는 제자리에서 기다리므로, 싸울 대상이 없는 동안 가장 가까운
                // 닿는 아군 쪽으로 계속 나아간다. 성문이 부서진 채 시작한 다음 스테이지에서 먼 들판의 적이 서 있던 문제.
                else if (ai.Target == null) ai.SetDirective(UnitDirective.Engage, nearest.transform.position);
                return;
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

        public override bool CanReachAnyEnemy() => AnyReachableEnemy(out _);

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

        /// <summary>걸어서 닿는 적 중 직선거리로 가장 가까운 것. 더 가까운 후보만 경로를 구한다.</summary>
        bool AnyReachableEnemy(out CharacterActor nearest)
        {
            nearest = null;
            float best = float.PositiveInfinity;
            Vector3 here = transform.position;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                float distance = (candidate.transform.position - here).sqrMagnitude;
                if (distance >= best) continue;
                if (agent.CalculatePath(candidate.transform.position, path)
                    && path.status == NavMeshPathStatus.PathComplete) { nearest = candidate; best = distance; }
            }
            return nearest != null;
        }

        // ── 사다리 ──────────────────────────────────────────────

        void DecideLadder()
        {
            SiegeLadder best = ChooseLadder();
            if (best == null)
            {
                // 쓸 수 있는 사다리가 하나도 없으면 멈춰 서지 않고 성문으로 간다.
                LeaveLadder();
                DecideGate();
                return;
            }
            if (best != ladder)
            {
                LeaveLadder();
                ladder = best;
                ladder.Join(this);
                ai.SetDirective(UnitDirective.Move, ladder.BasePoint);
                mode = Mode.ToLadder;
                return;
            }
            if (mode == Mode.Raising && ladder.IsRaiser(this) && ladder.State.Phase != LadderPhase.Up) return;
            Vector3 offset = transform.position - ladder.BasePoint;
            offset.y = 0f;
            if (offset.magnitude > settings.LadderUseRadius)
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

        SiegeLadder ChooseLadder()
        {
            SiegeLadder best = null;
            float bestScore = float.PositiveInfinity;
            foreach (SiegeLadder candidate in ladders)
            {
                if (candidate == null || !candidate.HasWall) continue;
                int others = candidate.Crowd - (candidate == ladder ? 1 : 0);
                float score = Vector3.Distance(transform.position, candidate.BasePoint)
                    + others * settings.LadderCrowdPenalty;
                // 이미 고른 사다리는 조금 더 붙들어 이리저리 옮겨 다니지 않게 한다.
                if (candidate == ladder) score -= settings.LadderCrowdPenalty;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
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
            // 돌격·후퇴 등 다른 일을 하다 돌아왔으면 자리 판단을 처음부터 한다.
            if (mode != Mode.Shooting) cover.Clear();
            mode = Mode.Shooting;
            archerHasShot = false;
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                // 지금 자리에서 쏠 수 있는 적이 있으면 그대로 서서 쏜다.
                if (CombatPhysics.IsInRange(Actor, candidate) && CombatPhysics.HasClearShot(Actor, candidate))
                {
                    cover.Shooting();
                    archerHasShot = true;
                    if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                    return;
                }
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            if (nearest == null) return;
            // 가장 가까운 적을 사거리 안에 두는 성 바깥쪽 정면 자리.
            float range = Actor.Stats.GetValue(StatType.AttackRange) * settings.ArcherApproachRatio;
            Vector3 point = nearest.transform.position + OutwardWorld * range;
            point.y = world.transform.position.y;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return;
            float now = Time.time;

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
            if (cover.ShouldSearch(now))
            {
                bool found = TryFindFiringPosition(hit.position, out Vector3 spot);
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
        bool TryFindFiringPosition(Vector3 front, out Vector3 position)
        {
            position = front;
            Vector3 along = Vector3.Cross(Vector3.up, OutwardWorld).normalized;
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

        bool CanShootFrom(Vector3 standPoint)
        {
            Vector3 aim = standPoint + Vector3.up * (Actor.Profile.Height * 0.5f);
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate) || candidate.Body == null) continue;
                float reach = CombatPhysics.AttackRange(Actor, candidate);
                if ((candidate.Body.ClosestPoint(aim) - aim).sqrMagnitude > reach * reach) continue;
                if (CombatPhysics.HasClearShotFrom(standPoint, Actor, candidate)) return true;
            }
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
