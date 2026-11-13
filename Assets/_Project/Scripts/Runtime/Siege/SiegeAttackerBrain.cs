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
    }

    /// <summary>
    /// 성을 공격하는 적 하나의 판단. 걸어서 닿는 아군이 있으면(뚫린 곳, 무너진 성문, 이미 오른 성벽 위)
    /// 기존 자동 전투에 맡긴다. 아군은 없고 걸어서 닿는 대포가 있으면(성벽 위에 올라온 경우 등) 대포를 부순다(SIEGE-02).
    /// 둘 다 없으면 종류에 따라 사다리·성문·사격을 고른다.
    /// 이동과 공격 자체는 UnitCombatAI의 지시(Engage·Hold·Move)로 실행한다.
    /// </summary>
    [RequireComponent(typeof(UnitCombatAI), typeof(CharacterActor), typeof(NavMeshAgent))]
    public sealed class SiegeAttackerBrain : MonoBehaviour
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
        public CharacterActor Actor { get; private set; }
        public SiegeAttackerRole Role => role;
        public bool IsClimbing => mode == Mode.Climbing;
        public bool IsAssaulting => mode == Mode.Assault;
        /// <summary>지금 부수러 가는 수성 무기. 없으면 null.</summary>
        public PlacedSiegeWeapon CannonTarget => mode == Mode.ToCannon ? cannon : null;
        /// <summary>전투가 시작된 뒤 한 번이라도 할 일을 정했는지. 후퇴 판정은 모두 정한 뒤에 한다.</summary>
        public bool HasDecided => mode != Mode.Deciding;
        public bool IsRetreating => retreatRequested && !Fled;
        /// <summary>후퇴해 전장을 떠났다. 승패에서는 죽은 적과 같이 남은 적 수에서 뺀다.</summary>
        public bool Fled { get; private set; }
        bool retreatRequested;
        float retreatRemaining;

        /// <summary>판 바깥으로 물러나게 한다. 오르거나 떨어지는 중이면 그 동작이 끝난 뒤 물러난다.</summary>
        public void Retreat()
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
        public void Bind(ConstructionWorld owner, SiegeLadder[] ladderSpots, Vector2Int outwardDirection,
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
            if (AnyReachableEnemy())
            {
                if (mode != Mode.Assault)
                {
                    LeaveLadder();
                    gate = null;
                    cannon = null;
                    wallBreaker?.Clear();
                    ai.SetDirective(UnitDirective.Engage);
                    mode = Mode.Assault;
                }
                return;
            }
            // 걸어서 닿는 아군이 없을 때만 대포를 노린다(Q-21). 궁수는 기존 사격을 유지한다.
            if (role != SiegeAttackerRole.Archer && DecideCannon()) return;
            if (mode == Mode.ToCannon) { cannon = null; mode = Mode.Deciding; }
            switch (role)
            {
                case SiegeAttackerRole.Ladder: DecideLadder(); break;
                case SiegeAttackerRole.GateBreaker: DecideGate(); break;
                case SiegeAttackerRole.WallBreaker:
                    mode = Mode.ToWall;
                    if (wallBreaker != null) wallBreaker.Decide();
                    else if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                    break;
                default: DecideArcher(); break;
            }
        }

        bool AnyReachableEnemy()
        {
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                if (agent.CalculatePath(candidate.transform.position, path)
                    && path.status == NavMeshPathStatus.PathComplete) return true;
            }
            return false;
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
            climbProgress += Time.deltaTime / settings.LadderClimbSeconds;
            // 대부분의 시간은 사다리를 오르고, 마지막에 성벽 위로 넘어간다.
            const float onLadder = 0.85f;
            transform.position = climbProgress < onLadder
                ? Vector3.Lerp(climbStart, ladder.EdgePoint, climbProgress / onLadder)
                : Vector3.Lerp(ladder.EdgePoint, ladder.TopPoint, (climbProgress - onLadder) / (1f - onLadder));
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
            ai.SetDirective(UnitDirective.Engage);
            mode = Mode.Deciding;
            think = 0f;
        }

        /// <summary>오르는 중에 사다리가 밀려 쓰러졌다. 그 높이에서 떨어져 낙하 피해를 받는다.</summary>
        public void OnLadderToppled()
        {
            if (mode == Mode.Climbing) Drop();
        }

        void Drop()
        {
            LeaveLadder();
            ai.enabled = true;
            mode = Mode.Falling;
            if (fall != null) fall.BeginFall(true);
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
                if (structure != null && structure.Placement.Definition.Kind == BuildPieceKind.Gate) return structure;
            // 성문 위에서 내려온 조각도 길을 막는 동안 성문 파괴병의 공격 대상이다.
            PlacedStructure nearest = null;
            float distance = float.PositiveInfinity;
            foreach (PlacedStructure structure in world.Structures)
            {
                BuildPlacement placement = structure.Placement;
                if (placement.Level != 0) continue;
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
            if (gate == null || gateBodies == null) return false;
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
            gate.ReceiveDamage(DamageRules.Calculate(Actor.Stats.GetValue(StatType.Attack), 0f, Actor.Profile.MinimumDamage));
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
            cannon.ReceiveDamage(DamageRules.Calculate(Actor.Stats.GetValue(StatType.Attack), 0f, Actor.Profile.MinimumDamage));
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
            mode = Mode.Shooting;
            CharacterActor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                // 지금 자리에서 쏠 수 있는 적이 있으면 그대로 서서 쏜다.
                if (CombatPhysics.IsInRange(Actor, candidate) && CombatPhysics.HasClearShot(Actor, candidate))
                {
                    if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold);
                    return;
                }
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            if (nearest == null) return;
            // 가장 가까운 적을 사거리 안에 두는 성 바깥쪽 자리로 다가간다.
            float range = Actor.Stats.GetValue(StatType.AttackRange) * settings.ArcherApproachRatio;
            Vector3 point = nearest.transform.position + OutwardWorld * range;
            point.y = world.transform.position.y;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return;
            if (ai.Directive != UnitDirective.Move || (ai.MoveDestination - hit.position).sqrMagnitude > 4f)
                ai.SetDirective(UnitDirective.Move, hit.position);
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
