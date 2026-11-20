using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 나는 적 하나의 판단(B-6.3, Q-43). 길찾기 없이 성벽을 넘어 날아가 고른 아군 옆에 떠서 공격한다.
    /// 목표 선택·이동·공격 자리는 FlyerSettings의 부품이 정하고, 이 부품은 순서와 상태만 다룬다.
    /// 공격은 몸통(CharacterActor)의 기존 공격 속도·선딜레이·근접/원거리 처리를 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor), typeof(AirborneBody))]
    public sealed class FlyerBrain : SiegeAttacker
    {
        enum Mode { Deciding, Pursuing, Holding, Retreating, Exiting }

        [SerializeField] FlyerSettings flyer;
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        // 성 바깥을 향하는 격자 방향. 후퇴할 판 끝을 정할 때 쓴다.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        readonly List<CharacterActor> targets = new List<CharacterActor>();
        readonly List<FlyerTargetCandidate> candidates = new List<FlyerTargetCandidate>();
        AirborneBody body;
        Func<Vector3, float?> surfaceAt;
        Mode mode = Mode.Deciding;
        Vector3 destination;
        float cruiseHeight;
        float think;
        bool retreatRequested;
        float retreatRemaining;
        float corpseSpeed;
        bool corpseLanded;
        CharacterActor hoverTarget;
        Vector3 hoverFeet;
        const float HoverRefreshDistance = 0.3f;

        /// <summary>지금 노리는 아군. 없으면 null.</summary>
        public CharacterActor Target { get; private set; }
        public FlyerSettings Settings => flyer;
        /// <summary>먼저 노릴 아군인지(SIEGE-09 결투형 보스). 비워 두면(기본) 설정의 대상 전략만 쓴다.</summary>
        public System.Func<CharacterActor, bool> PriorityTarget { get; set; }
        /// <summary>노릴 아군이 없어 성 안마당 위에서 기다리는 중인지.</summary>
        public bool IsHolding => mode == Mode.Holding;
        public Vector3 Destination => destination;
        public float CruiseHeight => cruiseHeight;
        public override SiegeAttackerRole Role => SiegeAttackerRole.Flyer;
        public override bool HasDecided => mode != Mode.Deciding;
        public override bool IsAssaulting => mode == Mode.Pursuing && Target != null;
        public override bool IsRetreating => (retreatRequested || mode == Mode.Exiting) && !Fled;
        public override bool UsesFallController => false;
        public override bool IsMidAction => false;

        public void Configure(FlyerSettings flyerSettings, SiegeSettings siegeSettings, Vector2Int outwardDirection)
        {
            flyer = flyerSettings; settings = siegeSettings; outward = outwardDirection;
        }

        public override void Bind(ConstructionWorld owner, SiegeLadder[] ladderSpots, Vector2Int outwardDirection,
            SiegeSettings battleSettings = null)
        {
            // 사다리는 쓰지 않는다.
            world = owner; outward = outwardDirection;
            if (battleSettings != null) settings = battleSettings;
        }

        public override void Retreat()
        {
            if (Fled || Actor.Health == null || Actor.Health.IsDead) return;
            retreatRequested = true;
        }

        public override bool CanReachAnyEnemy()
        {
            // 성벽에 막히지 않으므로 살아 있는 아군이 있으면 닿는다.
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
                if (Actor.IsEnemy(candidate)) return true;
            return false;
        }

        public override Vector3 BeginExit(Vector3 exit, float searchRadius)
        {
            retreatRequested = false;
            Target = null;
            mode = Mode.Exiting;
            if (world != null && flyer != null) cruiseHeight = ComputeCruiseHeight();
            destination = new Vector3(exit.x, Mathf.Max(cruiseHeight, exit.y), exit.z);
            return destination;
        }

        void Awake()
        {
            Actor = GetComponent<CharacterActor>();
            body = GetComponent<AirborneBody>();
            surfaceAt = SurfaceNear;
        }

        void OnEnable() => Actor.Died += OnDied;

        void OnDisable()
        {
            Actor.Died -= OnDied;
            Target = null;
            hoverTarget = null;
        }

        void Update()
        {
            if (Actor.Health == null) return;
            if (Actor.Health.IsDead) { UpdateCorpse(); return; }
            if (flyer == null || flyer.Movement == null || world == null || settings == null) return;
            UpdateBody();
            if (mode == Mode.Exiting) { Fly(destination, true); return; }
            if (retreatRequested) { UpdateRetreat(); return; }
            think -= Time.deltaTime;
            // 대상이 죽거나 사라지면 판단 주기를 기다리지 않고 바로 다시 고른다.
            if (!ReferenceEquals(Target, null) && !Actor.IsEnemy(Target)) think = 0f;
            if (think <= 0f)
            {
                think = settings.AttackerDecisionInterval;
                Decide();
            }
            if (mode == Mode.Pursuing && Target != null) Pursue();
            else Fly(destination, true);
        }

        void Decide()
        {
            cruiseHeight = ComputeCruiseHeight();
            targets.Clear();
            candidates.Clear();
            float ground = world.transform.position.y;
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
            {
                if (!Actor.IsEnemy(candidate)) continue;
                // 같은 거리일 때는 활성 목록에 먼저 들어온 아군을 고른다.
                candidates.Add(new FlyerTargetCandidate(targets.Count,
                    (candidate.transform.position - transform.position).sqrMagnitude,
                    candidate.transform.position.y - ground >= flyer.WallTopMinimumHeight,
                    PriorityTarget != null && PriorityTarget(candidate)));
                targets.Add(candidate);
            }
            // 먼저 노릴 대상 규칙이 있으면(SIEGE-09 결투형 보스) 그 대상을 먼저 고르고, 없으면 원래 전략을 쓴다.
            int index = PriorityTarget != null
                ? FlyerPriorityTargeting.Select(candidates, flyer.TargetStrategy)
                : flyer.TargetStrategy != null ? flyer.TargetStrategy.Select(candidates) : -1;
            if (index < 0 || index >= targets.Count)
            {
                // 노릴 아군이 없으면 성 안마당 위 높은 곳에서 기다린다.
                Target = null;
                mode = Mode.Holding;
                destination = HoldPoint();
                return;
            }
            if (targets[index] != Target) hoverTarget = null;
            Target = targets[index];
            mode = Mode.Pursuing;
        }

        void Pursue()
        {
            // 발판 검사가 들어가므로 대상이 조금이라도 움직였거나 새로 고른 때만 자리를 다시 구한다.
            Vector3 feet = Target.transform.position;
            if (Target != hoverTarget || (feet - hoverFeet).sqrMagnitude > HoverRefreshDistance * HoverRefreshDistance)
            {
                hoverTarget = Target;
                hoverFeet = feet;
                float range = Actor.Stats.GetValue(StatType.AttackRange);
                destination = flyer.Engagement != null
                    ? flyer.Engagement.HoverPoint(transform.position, feet, range,
                        Actor.Profile.Radius, Target.Profile.Radius, surfaceAt)
                    : feet;
            }
            bool inRange = CombatPhysics.IsInRange(Actor, Target);
            Fly(destination, !inRange);
            if (!inRange) return;
            Face(Target.transform.position - transform.position);
            if (Actor.CanAttack(Target)) Actor.TryAttack(Target);
        }

        void Fly(Vector3 point, bool faceMotion)
        {
            // 공격 판정을 기다리는 동안에는 자리를 지킨다.
            if (Actor.IsAttacking) return;
            Vector3 next = flyer.Movement.Step(transform.position, point, cruiseHeight,
                Actor.Stats.GetValue(StatType.MoveSpeed), Time.deltaTime);
            if (faceMotion) Face(next - transform.position);
            transform.position = next;
        }

        void UpdateRetreat()
        {
            if (mode != Mode.Retreating)
            {
                Target = null;
                mode = Mode.Retreating;
                retreatRemaining = settings.RetreatTimeout;
                cruiseHeight = ComputeCruiseHeight();
                destination = RetreatPoint();
            }
            Fly(destination, true);
            retreatRemaining -= Time.deltaTime;
            Vector3 offset = destination - transform.position;
            if (offset.sqrMagnitude <= 0.25f || retreatRemaining <= 0f)
            {
                Fled = true;
                // 꺼지면 대상 후보 목록에서도 빠진다.
                gameObject.SetActive(false);
            }
        }

        // 지금 자리에서 성 바깥 방향으로 판 끝까지 간 지점의 순항 높이.
        Vector3 RetreatPoint()
        {
            Vector3 local = world.LocalPoint(transform.position);
            float size = world.Grid.CellSize, margin = settings.RetreatEdgeMargin;
            if (outward.x > 0) local.x = world.Grid.Width * size - margin;
            else if (outward.x < 0) local.x = margin;
            else if (outward.y > 0) local.z = world.Grid.Depth * size - margin;
            else local.z = margin;
            local.y = 0f;
            Vector3 point = world.transform.TransformPoint(local);
            point.y = cruiseHeight;
            return point;
        }

        Vector3 OutwardWorld => world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;

        // 성 조각들의 가운데에서 성 안쪽으로 들어간 곳의 순항 높이. 조각이 없으면 판 가운데.
        Vector3 HoldPoint()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null) continue;
                sum += structure.transform.position;
                count++;
            }
            Vector3 center = count > 0 ? sum / count - OutwardWorld * flyer.HoldInset
                : world.transform.TransformPoint(new Vector3(world.Grid.Width * world.Grid.CellSize * 0.5f, 0f,
                    world.Grid.Depth * world.Grid.CellSize * 0.5f));
            center.y = cruiseHeight;
            return center;
        }

        float ComputeCruiseHeight()
        {
            float top = 0f;
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure == null) continue;
                BuildPlacement placement = structure.Placement;
                top = Mathf.Max(top, placement.Elevation + placement.Definition.Height);
            }
            return world.transform.position.y + top + flyer.CruiseClearance;
        }

        void UpdateBody()
        {
            bool found = FindGround(transform.position + Vector3.up * 0.1f, flyer.GroundProbeDistance, out Vector3 ground);
            float altitude = found ? transform.position.y - ground.y : float.PositiveInfinity;
            body.SetState(found ? ground : transform.position, found, altitude <= flyer.MeleeExposureAltitude);
        }

        // 대상 높이 근처에서 그 점 아래 발판의 높이. 유닛 몸통은 발판으로 치지 않는다.
        float? SurfaceNear(Vector3 point)
        {
            const float above = 2.5f, below = 1.5f;
            return FindGround(point + Vector3.up * above, above + below, out Vector3 ground) ? ground.y : (float?)null;
        }

        bool FindGround(Vector3 start, float distance, out Vector3 point)
        {
            point = default;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(start, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != gameObject.scene
                    || hit.collider.GetComponentInParent<CharacterActor>() != null || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point = hit.point;
            }
            return nearest < float.PositiveInfinity;
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        void OnDied()
        {
            Target = null;
            corpseSpeed = 0f;
            corpseLanded = false;
            body.SetState(transform.position, false, false);
        }

        // 죽으면 그 자리에 떠 있지 않고 아래 발판까지 떨어진다.
        void UpdateCorpse()
        {
            if (corpseLanded || flyer == null) return;
            corpseSpeed += flyer.CorpseFallAcceleration * Time.deltaTime;
            float step = corpseSpeed * Time.deltaTime;
            if (FindGround(transform.position + Vector3.up * 0.1f, step + 0.1f, out Vector3 ground))
            {
                transform.position = ground;
                corpseLanded = true;
                return;
            }
            transform.position += Vector3.down * step;
            if (transform.position.y < -100f) corpseLanded = true;
        }
    }
}
