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
    /// 성벽 위 아군이 없으면 설치된 대포도 노리고(SIEGE-16), 같은 대상을 쫓는 비행 적끼리는 다른 자리에 뜬다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor), typeof(AirborneBody))]
    public sealed class FlyerBrain : SiegeAttacker, IAnimationCueSource, IMotionVelocitySource
    {
        enum Mode { Deciding, Pursuing, Holding, Retreating, Exiting }

        [SerializeField] FlyerSettings flyer;
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        // 성 바깥을 향하는 격자 방향. 후퇴할 판 끝을 정할 때 쓴다.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        readonly List<CharacterActor> targets = new List<CharacterActor>();
        readonly List<FlyerTargetCandidate> candidates = new List<FlyerTargetCandidate>();
        readonly List<PlacedSiegeWeapon> weaponTargets = new List<PlacedSiegeWeapon>();
        readonly List<Vector3> occupied = new List<Vector3>();
        // 같은 대상 둘레의 자리를 나누려고 켜진 비행 적을 모두 기억한다(SIEGE-16).
        static readonly List<FlyerBrain> Active = new List<FlyerBrain>();
        static readonly Collider[] Overlaps = new Collider[16];
        // 지붕·발판 광선 검사가 매번 배열을 만들지 않게 함께 쓰는 버퍼(PERF-ai-alloc-and-split). 메인 스레드에서만 쓴다.
        static readonly RaycastHit[] RayHits = new RaycastHit[32];
        // 같은 아군의 지붕 검사 결과를 여러 비행 적이 잠깐 나눠 쓴다.
        static readonly ShelterSampleCache<CharacterActor> ShelterCache = new ShelterSampleCache<CharacterActor>();
        static int nextStaggerIndex;
        int staggerIndex = -1;
        // 발밑 높이 캐시. 마지막으로 잰 시각·자리와 그때의 발판 높이.
        bool groundCached;
        bool groundFound;
        float groundHeight;
        float groundProbeTime = float.NegativeInfinity;
        Vector3 groundProbePosition;
        AILodTier lodTier = AILodTier.Lod0;
        float lodCheckRemaining;
        AirborneBody body;
        Func<Vector3, float?> surfaceAt;
        Func<Vector3, bool> clearForActor;
        Func<Vector3, bool> clearForWeapon;
        Mode mode = Mode.Deciding;
        Vector3 destination;
        float cruiseHeight;
        float think;
        bool retreatRequested;
        float retreatRemaining;
        float corpseSpeed;
        bool corpseLanded;
        // 지금 자리를 구해 둔 대상(아군 또는 대포)과 그때의 발 위치, 처음 다가온 방향.
        object hoverTarget;
        Vector3 hoverFeet;
        Vector3 approach;
        const float HoverRefreshDistance = 0.3f;
        // 대상 잠금 남은 시간과 다음 사선 검사까지 남은 시간(SIEGE-32).
        float lockRemaining;
        float sightlineCheckRemaining;
        // 공격 자리에 닿았다고 보는 거리(m). 그 전에는 지금 자리 대신 공격 자리에서 사선을 본다.
        const float HoverArrivalDistance = 0.5f;

        /// <summary>지금 노리는 아군. 없으면 null.</summary>
        public CharacterActor Target { get; private set; }
        /// <summary>지금 노리는 수성 무기(SIEGE-16). 아군을 노리거나 아무것도 노리지 않으면 null.</summary>
        public PlacedSiegeWeapon WeaponTarget { get; private set; }
        /// <summary>대상을 쫓으며 그 둘레에 공격 자리를 잡아 두었는지. 다른 비행 적이 이 자리를 피한다.</summary>
        public bool HasHoverPoint => mode == Mode.Pursuing && hoverTarget != null;
        // 애니메이션 신호(ART-04): 쫓는 대상을 겨눈다. 활을 당긴 채 기다리는 자세는 쓰지 않는다.
        CharacterActor IAnimationCueSource.AimTarget => Target;
        bool IAnimationCueSource.HoldsDrawnBow => false;
        public FlyerSettings Settings => flyer;
        /// <summary>이번 프레임 비행 속도(SIEGE-33). 대포가 앞질러 겨눌 때 읽는다. 날지 않은 프레임은 0.</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>먼저 노릴 아군인지(SIEGE-09 결투형 보스). 비워 두면(기본) 설정의 대상 전략만 쓴다.</summary>
        public System.Func<CharacterActor, bool> PriorityTarget { get; set; }
        /// <summary>노릴 아군이 없어 성 안마당 위에서 기다리는 중인지.</summary>
        public bool IsHolding => mode == Mode.Holding;
        public Vector3 Destination => destination;
        public float CruiseHeight => cruiseHeight;
        /// <summary>지금 판단·발밑 검사 빈도를 정하는 AI LOD 등급(PERF-ai-alloc-and-split).</summary>
        public AILodTier LodTier => lodTier;
        public override SiegeAttackerRole Role => SiegeAttackerRole.Flyer;
        public override bool HasDecided => mode != Mode.Deciding;
        public override bool IsAssaulting => mode == Mode.Pursuing && PursuitKey != null;
        public override bool IsRetreating => (retreatRequested || mode == Mode.Exiting) && !Fled;
        public override bool UsesFallController => false;
        public override bool IsMidAction => false;
        // 쫓는 대상 하나. 아군이 먼저이고, 없으면 대포다.
        object PursuitKey => Target != null ? (object)Target : WeaponTarget != null ? WeaponTarget : null;

        public void Configure(FlyerSettings flyerSettings, SiegeSettings siegeSettings, Vector2Int outwardDirection)
        {
            flyer = flyerSettings; settings = siegeSettings; outward = outwardDirection;
        }

        public override void Bind(ConstructionWorld owner, System.Collections.Generic.IReadOnlyList<SiegeLadder> ladderSpots, Vector2Int outwardDirection,
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
            // 성벽에 막히지 않으므로 지붕 아래에 있지 않은 살아 있는 아군이 있으면 닿는다.
            IReadOnlyList<CharacterActor> actors = CharacterActor.ActiveActors;
            for (int i = 0; i < actors.Count; i++)
                if (Actor.IsEnemy(actors[i]) && !IsShelteredCached(actors[i])) return true;
            // 아군이 모두 지붕 아래여도 노릴 수 있는 대포가 남았으면 닿는다(SIEGE-32). 대상 고르기와 같은 기준이다.
            if (flyer != null && flyer.TargetsSiegeWeapons && world != null)
                foreach (PlacedSiegeWeapon placed in world.Weapons)
                    if (IsLiveWeapon(placed)) return true;
            return false;
        }

        // 판단·후퇴 검사용. 같은 아군을 최근에 거의 같은 자리에서 쟀으면 그 결과를 다시 쓴다.
        // 캐시 시간은 판단 주기의 절반을 넘지 않는다. 그래서 한 비행 적의 다음 판단은 늘 새로 재고(지붕이 사라지면 바로 안다),
        // 같은 판단 창에 모인 여러 비행 적과 후퇴 검사만 결과를 나눠 쓴다.
        bool IsShelteredCached(CharacterActor ally)
        {
            if (ally == null || flyer == null) return false;
            float now = Time.time;
            float maxAge = settings != null
                ? Mathf.Min(flyer.ShelterCacheSeconds, settings.AttackerDecisionInterval * 0.5f)
                : flyer.ShelterCacheSeconds;
            Vector3 position = ally.transform.position;
            ShelterCache.Prune(now, maxAge);
            if (ShelterCache.TryGet(ally, position, now, maxAge, flyer.ShelterMoveTolerance,
                    out bool sheltered)) return sheltered;
            sheltered = IsSheltered(ally);
            ShelterCache.Store(ally, position, now, sheltered);
            return sheltered;
        }

        /// <summary>
        /// 아군 머리 위 <see cref="FlyerSettings.RoofProbeDistance"/> 안에 성 조각 같은 지붕이 있는지(SIEGE-16).
        /// 유닛 몸통과 트리거, 다른 씬의 물체는 지붕으로 치지 않는다.
        /// </summary>
        public bool IsSheltered(CharacterActor ally)
        {
            if (ally == null || flyer == null || flyer.RoofProbeDistance <= 0f) return false;
            float head = ally.Profile != null ? ally.Profile.Height : 2f;
            Vector3 start = ally.transform.position + Vector3.up * (head + 0.05f);
            int count = Physics.RaycastNonAlloc(start, Vector3.up, RayHits, flyer.RoofProbeDistance, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = RayHits[i].collider;
                if (hit == null || hit.gameObject.scene != ally.gameObject.scene
                    || hit.GetComponentInParent<CharacterActor>() != null) continue;
                return true;
            }
            return false;
        }

        public override Vector3 BeginExit(Vector3 exit, float searchRadius)
        {
            retreatRequested = false;
            ClearTarget();
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
            clearForActor = point => BodyFits(point) && Target != null && CombatPhysics.HasClearShotFrom(point, Actor, Target);
            clearForWeapon = BodyFits;
        }

        void OnEnable()
        {
            Actor.Died += OnDied;
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable()
        {
            Actor.Died -= OnDied;
            Active.Remove(this);
            ClearTarget();
            // 풀에서 다시 켜지면 새 자리에서 발밑과 LOD를 처음부터 잰다.
            groundCached = false;
            lodTier = AILodTier.Lod0;
            lodCheckRemaining = 0f;
            staggerIndex = -1;
            if (Active.Count == 0) ShelterCache.Clear();
        }

        /// <summary>판단 분산 순번을 처음으로 되돌린다(테스트·새 전투용).</summary>
        public static void ResetStaggerCounter() => nextStaggerIndex = 0;

        // 다음 판단까지 기다릴 시간. 첫 판단 뒤 한 번만, 함께 생긴 비행 적들을 묶음별로 어긋나게 한다(지상 유닛과 같은 규칙).
        float NextDecisionInterval()
        {
            // 방금 판단으로 대상을 잡았으면 LOD 등급 갱신을 기다리지 않고 원래 주기를 쓴다.
            AILodTier tier = PursuitKey != null ? AILodTier.Lod0 : lodTier;
            float interval = settings.AttackerDecisionInterval * AILodRules.IntervalMultiplier(tier);
            if (staggerIndex >= 0) return interval;
            staggerIndex = Active.Count <= 1 ? 0 : nextStaggerIndex++;
            return interval + AIDecisionScheduler.InitialOffset(staggerIndex, flyer.DecisionStaggerBuckets, interval);
        }

        // 카메라에서 먼 비행 적은 판단과 발밑 검사를 덜 자주 한다. 대상을 쫓는 중이면(아군·대포) 늘 LOD 0이라
        // 대상 교체가 늦어지지 않는다. 노릴 대상 없이 기다리는 비행 적만 느려진다.
        void UpdateLodTier()
        {
            // 쫓는 대상이 생기면 다음 평가 시점을 기다리지 않고 바로 LOD 0으로 올린다.
            if (!flyer.EnableAiLod || Actor.Character != null || PursuitKey != null) { lodTier = AILodTier.Lod0; return; }
            lodCheckRemaining -= Time.deltaTime;
            if (lodCheckRemaining > 0f) return;
            lodCheckRemaining = flyer.LodEvaluationInterval;
            if (!AILodContext.TrySampleView(transform.position, out float sqrDistance, out bool inView, out bool topDown))
            {
                lodTier = AILodTier.Lod0;
                return;
            }
            lodTier = AILodRules.Evaluate(true, sqrDistance, flyer.Lod0Distance, flyer.Lod1Distance, inView,
                false, false, topDown);
        }

        void ClearTarget()
        {
            Target = null;
            WeaponTarget = null;
            hoverTarget = null;
            lockRemaining = 0f;
            sightlineCheckRemaining = 0f;
        }

        void Update()
        {
            Velocity = Vector3.zero;
            if (Actor.Health == null) return;
            if (Actor.Health.IsDead) { UpdateCorpse(); return; }
            if (flyer == null || flyer.Movement == null || world == null || settings == null) return;
            UpdateLodTier();
            UpdateBody();
            if (mode == Mode.Exiting) { Fly(destination, true); return; }
            if (retreatRequested) { UpdateRetreat(); return; }
            think -= Time.deltaTime;
            lockRemaining = Mathf.Max(0f, lockRemaining - Time.deltaTime);
            // 대상이 죽거나 사라지면 판단 주기를 기다리지 않고 바로 다시 고른다.
            if (!ReferenceEquals(Target, null) && !Actor.IsEnemy(Target)) think = 0f;
            if (!ReferenceEquals(WeaponTarget, null) && !IsLiveWeapon(WeaponTarget)) think = 0f;
            if (think <= 0f)
            {
                Decide();
                think = NextDecisionInterval();
            }
            if (mode == Mode.Pursuing && PursuitKey != null) Pursue();
            else Fly(destination, true);
        }

        void Decide()
        {
            cruiseHeight = ComputeCruiseHeight();
            targets.Clear();
            candidates.Clear();
            weaponTargets.Clear();
            float ground = world.transform.position.y;
            // 지금 대상이 후보 목록에서 몇 번인지. 없으면(죽음·지붕 아래·제거) -1이라 잠금 없이 바로 다시 고른다.
            int currentIndex = -1;
            // 인터페이스 목록을 foreach로 돌면 열거자가 할당되므로 번호로 돈다(PERF-ai-alloc-and-split).
            IReadOnlyList<CharacterActor> actors = CharacterActor.ActiveActors;
            for (int i = 0; i < actors.Count; i++)
            {
                CharacterActor candidate = actors[i];
                // 지붕 아래 아군(계단방·탑 안·성문 통로)은 날아서 닿지 않으므로 고르지 않는다.
                if (!Actor.IsEnemy(candidate) || IsShelteredCached(candidate)) continue;
                if (candidate == Target) currentIndex = candidates.Count;
                // 같은 거리일 때는 활성 목록에 먼저 들어온 아군을 고른다.
                candidates.Add(new FlyerTargetCandidate(targets.Count,
                    (candidate.transform.position - transform.position).sqrMagnitude,
                    candidate.transform.position.y - ground >= flyer.WallTopMinimumHeight,
                    PriorityTarget != null && PriorityTarget(candidate)));
                targets.Add(candidate);
            }
            // 대포는 아군 뒤에 붙인다. 인덱스가 아군 수 이상이면 대포다(SIEGE-16).
            if (flyer.TargetsSiegeWeapons)
                foreach (PlacedSiegeWeapon placed in world.Weapons)
                {
                    if (!IsLiveWeapon(placed)) continue;
                    if (placed == WeaponTarget) currentIndex = candidates.Count;
                    candidates.Add(FlyerTargetCandidate.SiegeWeapon(candidates.Count,
                        (placed.transform.position - transform.position).sqrMagnitude));
                    weaponTargets.Add(placed);
                }
            // 먼저 노릴 대상 규칙이 있으면(SIEGE-09 결투형 보스) 그 대상을 먼저 고르고, 없으면 원래 전략을 쓴다.
            // 지금 대상은 잠금 시간과 거리 여유만큼 지킨다(SIEGE-32).
            int index = FlyerTargetLockRules.Select(candidates, flyer.TargetStrategy, PriorityTarget != null,
                currentIndex, lockRemaining, flyer.RetargetDistanceMargin);
            if (index < 0 || index >= targets.Count + weaponTargets.Count)
            {
                // 노릴 대상이 없으면 성 안마당 위 높은 곳에서 기다린다.
                ClearTarget();
                mode = Mode.Holding;
                destination = HoldPoint();
                return;
            }
            CharacterActor actor = index < targets.Count ? targets[index] : null;
            PlacedSiegeWeapon weapon = actor == null ? weaponTargets[index - targets.Count] : null;
            if (actor != Target || weapon != WeaponTarget)
            {
                hoverTarget = null;
                lockRemaining = flyer.TargetLockSeconds;
                sightlineCheckRemaining = 0f;
            }
            Target = actor;
            WeaponTarget = weapon;
            mode = Mode.Pursuing;
        }

        static bool IsLiveWeapon(PlacedSiegeWeapon weapon) => weapon != null && !weapon.IsRemoved
            && weapon.Definition != null && weapon.Health != null && !weapon.Health.IsDead;

        void Pursue()
        {
            object key = PursuitKey;
            Vector3 feet = Target != null ? Target.transform.position : WeaponTarget.transform.position;
            // 발판·시야 검사가 들어가므로 대상이 조금이라도 움직였거나 새로 고른 때만 자리를 다시 구한다.
            if (!ReferenceEquals(key, hoverTarget)
                || (feet - hoverFeet).sqrMagnitude > HoverRefreshDistance * HoverRefreshDistance)
            {
                if (!ReferenceEquals(key, hoverTarget))
                {
                    // 다가온 방향은 대상을 고를 때 한 번만 정한다. 다시 구할 때 자기 자리를 기준으로 삼아 조금씩 돌지 않게 한다.
                    approach = transform.position - feet;
                    approach.y = 0f;
                    approach = approach.sqrMagnitude > 0.0001f ? approach.normalized : transform.forward;
                }
                hoverTarget = key;
                hoverFeet = feet;
                destination = ComputeHoverPoint(key, feet);
            }
            if (Target != null) StrikeActor();
            else StrikeWeapon();
        }

        Vector3 ComputeHoverPoint(object key, Vector3 feet)
        {
            if (flyer.Engagement == null) return feet;
            // 같은 대상을 쫓으며 이미 자리를 잡은 다른 비행 적의 자리는 피한다(SIEGE-16).
            occupied.Clear();
            foreach (FlyerBrain other in Active)
                if (other != this && other.HasHoverPoint && ReferenceEquals(other.hoverTarget, key)
                    && other.Actor.Health != null && !other.Actor.Health.IsDead)
                    occupied.Add(other.destination);
            float targetRadius = Target != null ? Target.Profile.Radius
                : Mathf.Max(WeaponTarget.Definition.Size.x, WeaponTarget.Definition.Size.z) * 0.5f;
            var query = new FlyerHoverQuery(feet + approach, feet, Actor.Stats.GetValue(StatType.AttackRange),
                Actor.Profile.Radius, targetRadius, surfaceAt, occupied, flyer.HoverSeparation,
                Target != null ? clearForActor : clearForWeapon);
            return flyer.Engagement.HoverPoint(query);
        }

        void StrikeActor()
        {
            bool inRange = CombatPhysics.IsInRange(Actor, Target);
            Fly(destination, !inRange);
            if (RevalidateSightline()) return;
            if (!inRange) return;
            Face(Target.transform.position - transform.position);
            if (Actor.CanAttack(Target)) Actor.TryAttack(Target);
        }

        // 쫓는 중 정해진 간격마다 사선을 다시 본다(SIEGE-32). 대상이 성가퀴·기둥 뒤로 가려졌으면 공격 자리를 버리고
        // 다음 프레임에 다시 구한다(자리 고르기는 사선이 열린 곳을 먼저 고른다). 공격 자리에 닿았으면 지금 자리에서,
        // 아직 가는 중이면 공격 자리에서 본다. 자리를 버렸으면 true.
        bool RevalidateSightline()
        {
            sightlineCheckRemaining -= Time.deltaTime;
            if (sightlineCheckRemaining > 0f || hoverTarget == null || Actor.IsAttacking) return false;
            sightlineCheckRemaining = flyer.SightlineRecheckInterval;
            bool arrived = (transform.position - destination).sqrMagnitude <= HoverArrivalDistance * HoverArrivalDistance;
            bool clear = arrived ? CombatPhysics.HasClearShot(Actor, Target)
                : CombatPhysics.HasClearShotFrom(destination, Actor, Target);
            if (clear) return false;
            hoverTarget = null;
            return true;
        }

        // 대포에는 충돌체가 없어 무기 크기로 닿는지 잰다. 걷는 적과 같은 규칙이고 방어력은 없다(SIEGE-02).
        // 피해는 공격 판정 때(원거리면 화살이 멈출 때) 한 번 확정해서 준다(SIEGE-32). 그 사이에 대포가 없어지면 주지 않는다.
        void StrikeWeapon()
        {
            PlacedSiegeWeapon weapon = WeaponTarget;
            bool inReach = SiegeWeaponStrikeRules.InReach(transform.position, weapon.transform.position,
                weapon.Definition.Size, Actor.Stats.GetValue(StatType.AttackRange));
            Fly(destination, !inReach);
            if (!inReach) return;
            Face(weapon.transform.position - transform.position);
            // 공격을 시작할 수 없는 프레임에는 판정 콜백을 만들지 않는다.
            if (Actor.IsAttacking || Actor.CooldownRemaining > 0f) return;
            float damage = BossUnit.StructureDamage(Actor,
                DamageRules.Calculate(Actor.Stats.GetValue(StatType.Attack), 0f, Actor.Profile.MinimumDamage));
            Vector3 size = weapon.Definition.Size;
            Vector3 aim = weapon.transform.position + Vector3.up * (size.y * 0.5f);
            float stopRadius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
            Actor.TryAttackPoint(aim, stopRadius, () =>
            {
                if (IsLiveWeapon(weapon)) weapon.ReceiveDamage(damage);
            });
        }

        // 이 자리(발 위치)에 비행 적 몸통이 성 조각 등에 박히지 않고 들어가는지. 유닛 몸통과 트리거는 따지지 않는다.
        bool BodyFits(Vector3 point)
        {
            float radius = Mathf.Max(0.05f, Actor.Profile.Radius * 0.9f);
            float height = Mathf.Max(radius * 2f, Actor.Profile.Height);
            Vector3 bottom = point + Vector3.up * (radius + 0.05f), top = point + Vector3.up * (height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Overlaps[i];
                if (hit == null || hit.gameObject.scene != gameObject.scene
                    || hit.GetComponentInParent<CharacterActor>() != null) continue;
                return false;
            }
            return true;
        }

        void Fly(Vector3 point, bool faceMotion)
        {
            // 공격 판정을 기다리는 동안에는 자리를 지킨다.
            if (Actor.IsAttacking) return;
            Vector3 next = flyer.Movement.Step(transform.position, point, cruiseHeight,
                Actor.Stats.GetValue(StatType.MoveSpeed), Time.deltaTime);
            if (faceMotion) Face(next - transform.position);
            if (Time.deltaTime > 0f) Velocity = (next - transform.position) / Time.deltaTime;
            transform.position = next;
        }

        void UpdateRetreat()
        {
            if (mode != Mode.Retreating)
            {
                ClearTarget();
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

        // 발밑 발판 높이는 정해진 간격(먼 LOD일수록 길게)이나 수평으로 조금 움직였을 때만 다시 잰다.
        // 높이 자체는 매 프레임 지금 위치로 계산하므로 오르내림은 바로 반영된다.
        void UpdateBody()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - groundProbePosition;
            moved.y = 0f;
            float tolerance = flyer.GroundProbeMoveTolerance;
            bool belowCachedGround = groundFound && position.y < groundHeight;
            if (!groundCached || belowCachedGround || moved.sqrMagnitude > tolerance * tolerance
                || Time.time - groundProbeTime >= flyer.GroundProbeInterval * AILodRules.IntervalMultiplier(lodTier))
            {
                groundFound = FindGround(position + Vector3.up * 0.1f, flyer.GroundProbeDistance, out Vector3 hit);
                groundHeight = hit.y;
                groundCached = true;
                groundProbeTime = Time.time;
                groundProbePosition = position;
            }
            float altitude = groundFound ? position.y - groundHeight : float.PositiveInfinity;
            Vector3 ground = groundFound ? new Vector3(position.x, groundHeight, position.z) : position;
            body.SetState(ground, groundFound, altitude <= flyer.MeleeExposureAltitude);
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
            int count = Physics.RaycastNonAlloc(start, Vector3.down, RayHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = RayHits[i];
                if (hit.collider == null || hit.distance >= nearest || hit.collider.gameObject.scene != gameObject.scene
                    || hit.collider.GetComponentInParent<CharacterActor>() != null) continue;
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
            ClearTarget();
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
