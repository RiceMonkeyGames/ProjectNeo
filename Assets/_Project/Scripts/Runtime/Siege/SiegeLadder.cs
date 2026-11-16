using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 맵에 고정된 사다리 자리 하나. 그 칸에 성벽이 있을 때만 쓸 수 있다.
    /// 적이 밑에서 세우고 한 명씩 오르며, 성벽 위의 아군이 밀어 쓰러뜨린다. 외형은 Visual 자식이 맡는다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class SiegeLadder : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        [SerializeField] Vector2Int cell;
        // 성 바깥을 향하는 격자 방향.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        readonly HashSet<SiegeAttackerBrain> crowd = new HashSet<SiegeAttackerBrain>();
        SiegeAttackerBrain raiser;
        SiegeAttackerBrain climber;
        readonly LadderTraffic<CharacterActor> traffic = new LadderTraffic<CharacterActor>();
        readonly List<CharacterActor> removedRequests = new List<CharacterActor>();
        readonly Dictionary<CharacterActor, LadderPushInterruptHandle> aiPushHandles = new Dictionary<CharacterActor, LadderPushInterruptHandle>();
        CharacterActor sharedRaiser;
        static readonly List<SiegeLadder> active = new List<SiegeLadder>();
        public static IReadOnlyList<SiegeLadder> Active => active;
        public SiegeSettings Settings => settings;
        public ConstructionWorld World => world;
        public CharacterActor Occupant => traffic.Occupant;
        public IReadOnlyList<CharacterActor> Waiting => traffic.Waiting;
        InputAction push;
        float wallHeight = 3f;
        int supportId;
        public SiegeLadderState State { get; private set; }
        public Vector2Int Cell => cell;
        public int Crowd => crowd.Count;
        /// <summary>플레이어가 밀 수 있는 거리 안에 있고 사다리가 세워져 있는지. 안내 표시에 쓴다.</summary>
        public bool PlayerCanPush { get; private set; }

        public void Configure(ConstructionWorld owner, SiegeSettings siegeSettings, Vector2Int ladderCell, Vector2Int outwardDirection)
        {
            world = owner; settings = siegeSettings; cell = ladderCell; outward = outwardDirection;
        }

        void Awake()
        {
            State = new SiegeLadderState(settings.LadderRaiseSeconds, settings.LadderPushSeconds,
                pushTuning: settings.CreateLadderPushTuning());
            State.Toppled += OnToppled;
            State.ClimberDislodged += OnClimberDislodged;
            push = settings.PushLadder.Clone();
        }

        void OnEnable() { push.Enable(); active.Add(this); }
        void OnDisable()
        {
            active.Remove(this);
            push.Disable();
            State.Topple();
            traffic.Clear();
            sharedRaiser = null;
            ClearAiPushHandles();
        }
        void OnDestroy()
        {
            ClearAiPushHandles();
            if (State != null)
            {
                State.Toppled -= OnToppled;
                State.ClimberDislodged -= OnClimberDislodged;
            }
            if (push != null) push.Dispose();
        }

        public bool HasWall
        {
            get
            {
                BuildPlacement placement = world.Layout != null ? world.Layout.Top(cell) : null;
                if (placement == null || placement.Definition.Kind != BuildPieceKind.Wall) return false;
                float height = placement.Elevation + placement.Definition.Height;
                if (supportId != 0 && (supportId != placement.Id || !Mathf.Approximately(wallHeight, height)))
                    State?.Topple();
                supportId = placement.Id;
                wallHeight = height;
                // 무게중심이 발 바로 위에 오는 실제 수직 각도를 규칙에 전달한다.
                float angle = Vector3.Angle(OutwardWorld, EdgePoint - BasePoint);
                State?.ConfigureBalancePoint(angle > 0f ? Mathf.Clamp01(90f / angle) : 1f);
                return true;
            }
        }

        public float WallHeight => wallHeight;
        public void ResetBattle()
        {
            State.Topple();
            traffic.Clear(); sharedRaiser = null;
            ClearAiPushHandles();
            crowd.Clear(); raiser = null; climber = null; supportId = 0; PlayerCanPush = false;
        }

        void ClearAiPushHandles()
        {
            foreach (var pair in aiPushHandles) pair.Value.Dispose();
            aiPushHandles.Clear();
        }
        public Vector3 OutwardWorld => world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;

        Vector3 Point(float fromCenter, float height)
        {
            float size = world.Grid.CellSize;
            var center = new Vector3((cell.x + 0.5f) * size, height, (cell.y + 0.5f) * size);
            return world.transform.TransformPoint(center + new Vector3(outward.x, 0f, outward.y) * fromCenter);
        }

        /// <summary>적이 사다리를 세우고 오르기 시작하는 땅 위의 자리.</summary>
        public Vector3 BasePoint => Point(world.Grid.CellSize * 0.5f + settings.LadderBaseOffset, 0f);
        /// <summary>사다리 위쪽 끝. 성벽 바깥면 바로 앞이다.</summary>
        public Vector3 EdgePoint => Point(world.Grid.CellSize * 0.5f + 0.35f, wallHeight);
        /// <summary>다 오른 유닛이 올라서는 성벽 위의 자리.</summary>
        public Vector3 TopPoint => Point(world.Grid.CellSize * 0.5f - settings.LadderTopInset, wallHeight);

        /// <summary>외형과 탑승자가 공유하는 현재 사다리 방향.</summary>
        public Vector3 CurrentDirection => Vector3.Slerp(OutwardWorld,
            (EdgePoint - BasePoint).normalized, State.RaisedFraction);

        /// <summary>사다리 접점만 기울이고, 몸과 접근 여유의 방향은 유지한다.</summary>
        public Vector3 FollowTilt(Vector3 raisedPosition)
        {
            Vector3 raisedDirection = (EdgePoint - BasePoint).normalized;
            float along = Mathf.Max(0f, Vector3.Dot(raisedPosition - BasePoint, raisedDirection));
            // 사용 반경에서 막 탑승한 적의 접근 오프셋까지 회전하면 발이 땅 아래로 들어간다.
            // 접점 이동분만 적용해 그 여유는 월드 공간에 남겨 둔다.
            return raisedPosition + (CurrentDirection - raisedDirection) * along;
        }

        /// <summary>사다리에서 분리될 때 외형과 겹치지 않는 시작 위치와 바깥쪽 낙하 속도를 반환한다.</summary>
        public bool TryGetFallMotion(Vector3 currentPosition, out Vector3 separatedPosition, out Vector3 outwardVelocity)
        {
            separatedPosition = currentPosition;
            outwardVelocity = Vector3.zero;
            if (world == null || settings == null || !HasWall) return false;
            separatedPosition = ComputeFallSeparation(currentPosition, BasePoint, CurrentDirection, OutwardWorld, settings.LadderFallClearance);
            outwardVelocity = OutwardWorld * settings.LadderFallOutwardSpeed;
            return true;
        }

        /// <summary>현재 사다리 축에서 바깥쪽 법선 방향으로 최소 간격만큼 떨어뜨린 위치를 계산한다.</summary>
        public static Vector3 ComputeFallSeparation(Vector3 position, Vector3 basePoint,
            Vector3 ladderDirection, Vector3 outwardWorld, float minClearance)
        {
            if (minClearance <= 0f) return position;
            Vector3 flatOutward = new Vector3(outwardWorld.x, 0f, outwardWorld.z);
            if (flatOutward.sqrMagnitude < 0.0001f) return position;
            flatOutward.Normalize();

            Vector3 dir = ladderDirection.sqrMagnitude > 0.0001f ? ladderDirection.normalized : Vector3.up;
            float along = Mathf.Max(0f, Vector3.Dot(position - basePoint, dir));
            Vector3 railPoint = basePoint + dir * along;
            float currentOutward = Vector3.Dot(position - railPoint, flatOutward);
            if (currentOutward >= minClearance) return position;
            return position + flatOutward * (minClearance - currentOutward);
        }

        public void Join(SiegeAttackerBrain unit) => crowd.Add(unit);

        public void Leave(SiegeAttackerBrain unit)
        {
            if (unit != null) Leave(unit.Actor);
            crowd.Remove(unit);
            if (raiser == unit) raiser = null;
            if (climber == unit) { climber = null; State.EndClimb(); }
        }

        public bool IsRaiser(SiegeAttackerBrain unit) => raiser == unit && unit != null;

        /// <summary>바닥에 놓인 사다리를 세우기 시작한다. 이미 다른 유닛이 세우고 있으면 실패한다.</summary>
        public bool TryRaise(SiegeAttackerBrain unit)
        {
            if (!HasWall || State.Phase == LadderPhase.Up || State.IsSettling) return false;
            if (Alive(sharedRaiser) && IsNear(sharedRaiser, false)) return false;
            if (raiser != null && raiser != unit && IsNearBase(raiser)) return false;
            raiser = unit;
            return true;
        }

        public bool TryBeginClimb(SiegeAttackerBrain unit)
        {
            if (unit == null || !TryEnter(unit.Actor)) return false;
            climber = unit;
            unit.Actor.SetOnLadder(true);
            return true;
        }

        public bool IsNear(CharacterActor actor, bool upper)
        {
            if (actor == null) return false;
            Vector3 delta = actor.transform.position - (upper ? TopPoint : BasePoint);
            float height = Mathf.Abs(delta.y); delta.y = 0f;
            return height <= settings.LadderHeightTolerance && delta.sqrMagnitude <= settings.LadderUseRadius * settings.LadderUseRadius;
        }

        public bool TryEnter(CharacterActor actor)
        {
            if (!HasWall || !Alive(actor) || State.IsSettling
                || (!IsNear(actor, false) && !IsNear(actor, true))) return false;
            traffic.Join(actor);
            if (!State.CanBoard || State.HasClimber || !traffic.TryEnter(actor)) return false;
            if (State.TryBeginClimb()) return true;
            traffic.Leave(actor);
            return false;
        }

        public void Leave(CharacterActor actor)
        {
            bool occupied = traffic.Occupant == actor;
            traffic.Leave(actor);
            if (actor != null) actor.SetOnLadder(false);
            if (occupied) State.EndClimb();
            if (sharedRaiser == actor) sharedRaiser = null;
        }

        public bool TryRaise(CharacterActor actor)
        {
            if (!HasWall || !Alive(actor) || !IsNear(actor, false)
                || State.Phase == LadderPhase.Up || State.IsSettling) return false;
            if (raiser != null && IsNearBase(raiser)) return false;
            if (Alive(sharedRaiser) && sharedRaiser != actor && IsNear(sharedRaiser, false)) return false;
            sharedRaiser = actor;
            return true;
        }

        public void StopRaising(CharacterActor actor) { if (sharedRaiser == actor) sharedRaiser = null; }
        static bool Alive(CharacterActor actor) => actor != null && actor.isActiveAndEnabled && actor.Health != null && !actor.Health.IsDead;
        bool FriendlyUse
        {
            get
            {
                if (Alive(traffic.Occupant) && traffic.Occupant.Team == CombatTeam.Allied) return true;
                foreach (var actor in traffic.Waiting) if (Alive(actor) && actor.Team == CombatTeam.Allied) return true;
                return false;
            }
        }

        void CleanTraffic()
        {
            removedRequests.Clear();
            foreach (var actor in traffic.Waiting)
                if (!Alive(actor) || (!IsNear(actor, false) && !IsNear(actor, true))) removedRequests.Add(actor);
            foreach (var actor in removedRequests) Leave(actor);
            if (!ReferenceEquals(traffic.Occupant, null) && !Alive(traffic.Occupant)) Leave(traffic.Occupant);
        }

        bool IsNearBase(SiegeAttackerBrain unit)
        {
            if (unit == null || !unit.isActiveAndEnabled || unit.Actor.Health == null || unit.Actor.Health.IsDead) return false;
            Vector3 offset = unit.transform.position - BasePoint;
            offset.y = 0f;
            return offset.sqrMagnitude <= settings.LadderUseRadius * settings.LadderUseRadius;
        }

        void Update()
        {
            PlayerCanPush = false;
            TickAiPushHandles(Time.deltaTime);
            CleanTraffic();
            if (!HasWall)
            {
                // 받치던 성벽이 사라지면 사다리도 쓰러지고 오르던 유닛은 떨어진다.
                State.Topple();
                raiser = null;
                return;
            }
            if (raiser != null && !IsNearBase(raiser)) raiser = null;
            if (!Alive(sharedRaiser) || !IsNear(sharedRaiser, false)) sharedRaiser = null;
            int pushers = 0; bool blocked = false;
            if (State.Phase == LadderPhase.Up) FindPushers(out pushers, out blocked);
            State.Tick(Time.deltaTime, raiser != null || sharedRaiser != null, pushers, blocked);
            if (State.Phase == LadderPhase.Up) { raiser = null; sharedRaiser = null; }
        }

        void TickAiPushHandles(float deltaTime)
        {
            foreach (var pair in aiPushHandles)
            {
                pair.Value.SetPushing(false, settings.LadderPushPoise, settings.LadderPushInterruptCooldown);
                pair.Value.Tick(deltaTime);
            }
        }

        /// <summary>위쪽 끝에서 함께 밀 수 있는 아군 수와, 올라온 적 때문에 밀기가 막혔는지 센다.</summary>
        void FindPushers(out int pushers, out bool blocked)
        {
            pushers = 0; blocked = false;
            Vector3 top = TopPoint;
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor.gameObject.scene != gameObject.scene) continue;
                if (climber != null && actor == climber.Actor) continue;
                Vector3 offset = actor.transform.position - top;
                if (Mathf.Abs(offset.y) > settings.LadderHeightTolerance) continue;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (actor.Team == CombatTeam.Hostile)
                {
                    // 올라온 적이 있으면 아군은 그 적과 싸우느라 밀지 못한다.
                    if (distance <= settings.LadderBlockRadius) blocked = true;
                    continue;
                }
                if (distance > settings.LadderPushRadius) continue;
                if (actor.GetComponent<PlayerInputController>() != null)
                {
                    PlayerCanPush = !State.IsSettling;
                    var traversal = actor.GetComponent<LadderTraveller>();
                    if (traversal != null && traversal.IsHoldingTopple(this) && !FriendlyUse) pushers++;
                }
                else
                {
                    var ai = actor.GetComponent<UnitCombatAI>();
                    if (ai != null && ai.Target == null && !actor.IsAttacking && !FriendlyUse)
                    {
                        if (!aiPushHandles.TryGetValue(actor, out var handle))
                        {
                            handle = new LadderPushInterruptHandle(actor);
                            aiPushHandles[actor] = handle;
                        }
                        if (!handle.IsOnCooldown)
                        {
                            handle.SetPushing(true, settings.LadderPushPoise, settings.LadderPushInterruptCooldown);
                            pushers++;
                        }
                    }
                }
            }
            if (blocked) PlayerCanPush = false;
            if (FriendlyUse) PlayerCanPush = false;
        }

        void OnToppled(bool hadClimber)
        {
            ReleaseOccupant(false);
            traffic.Clear(); sharedRaiser = null;
            raiser = null;
        }

        void OnClimberDislodged() => ReleaseOccupant(true);

        void ReleaseOccupant(bool followTilt)
        {
            CharacterActor occupant = traffic.Occupant;
            traffic.Leave(occupant);
            SiegeAttackerBrain fallen = climber;
            climber = null;
            if (fallen != null)
            {
                if (followTilt) fallen.FollowLadderTilt();
                fallen.OnLadderToppled();
            }
            if (occupant != null)
            {
                occupant.SetOnLadder(false);
                var traveller = occupant.GetComponent<LadderTraveller>();
                if (followTilt) traveller?.FollowLadderTilt();
                traveller?.OnToppled();
            }
        }
    }
}
