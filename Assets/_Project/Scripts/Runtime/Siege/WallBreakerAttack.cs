using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Siege
{
    /// <summary>성벽 밑단의 도달 가능한 공격 지점과 타격을 담당한다. 돌파/후퇴 판단은 기존 brain이 맡는다.</summary>
    [RequireComponent(typeof(CharacterActor), typeof(UnitCombatAI), typeof(NavMeshAgent))]
    public sealed class WallBreakerAttack : MonoBehaviour
    {
        struct Slot
        {
            public Vector3 Point;
            public float Score;
        }

        [SerializeField] WallBreakerSettings settings;
        // 지금 움직이는 성벽 파괴병. 서로 잡은 자리를 피할 때 쓴다(SIEGE-17).
        static readonly List<WallBreakerAttack> active = new List<WallBreakerAttack>();
        static readonly System.Comparison<Slot> byScore = (a, b) => a.Score.CompareTo(b.Score);
        readonly List<PlacedStructure> pieces = new List<PlacedStructure>();
        readonly List<WallTargetCandidate> candidates = new List<WallTargetCandidate>();
        readonly List<Slot> slots = new List<Slot>();
        readonly List<Vector3> claims = new List<Vector3>();
        CharacterActor actor;
        UnitCombatAI ai;
        NavMeshAgent agent;
        NavMeshPath path;
        ConstructionWorld world;
        Collider[] targetBodies;
        bool selected;
        public PlacedStructure Target { get; private set; }
        public Vector3 ApproachPoint { get; private set; }
        public bool TargetInvalidated => selected && (Target == null || Target.Health.IsDead);

        public void Configure(WallBreakerSettings config) => settings = config;
        public void Bind(ConstructionWorld owner) => world = owner;

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            ai = GetComponent<UnitCombatAI>();
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() { active.Remove(this); Clear(); }

        public void Clear()
        {
            Target = null;
            selected = false;
            targetBodies = null;
        }

        public void Decide()
        {
            if (settings == null || settings.TargetStrategy == null || world == null)
            { Clear(); Hold(); return; }
            pieces.Clear(); candidates.Clear();
            foreach (var piece in world.Structures)
            {
                if (piece == null || piece.Health.IsDead || piece.Placement.Level != 0
                    || piece.Placement.Definition.Kind == BuildPieceKind.Stairs) continue;
                float distance = float.PositiveInfinity;
                foreach (var body in piece.GetComponents<Collider>())
                    if (body.enabled) distance = Mathf.Min(distance, (body.ClosestPoint(actor.AimPoint) - actor.AimPoint).sqrMagnitude);
                pieces.Add(piece);
                candidates.Add(new WallTargetCandidate(piece.Placement.Id, distance));
            }
            while (candidates.Count > 0)
            {
                int index = settings.TargetStrategy.Select(candidates);
                if (index < 0 || index >= candidates.Count) break;
                var piece = pieces[index];
                // 이미 그 대상에 닿아 치고 있으면 자리를 다시 고르지 않는다. 다른 파괴병이 이 자리를 피하는 기준이 흔들리지 않는다.
                if (piece == Target && InReach()) { Hold(); return; }
                if (TryApproach(piece, out Vector3 point))
                {
                    bool changed = Target != piece || (ApproachPoint - point).sqrMagnitude > 0.01f;
                    Target = piece; selected = true;
                    targetBodies = piece.GetComponents<Collider>();
                    ApproachPoint = point;
                    if (InReach()) Hold();
                    else if (changed || ai.Directive != UnitDirective.Move) ai.SetDirective(UnitDirective.Move, point);
                    return;
                }
                candidates.RemoveAt(index); pieces.RemoveAt(index);
            }
            Clear(); Hold();
        }

        /// <summary>
        /// 대상 조각의 네 면을 따라 고르게 놓은 자리 중, 가깝고 다른 파괴병이 잡지 않은 순서로 걸어서 닿고 공격 사거리에 드는
        /// 첫 자리(SIEGE-17). 면 길이는 칸 크기에서 구하므로 칸 크기와 무관하다. 길찾기는 대상마다 정해진 수까지만 한다.
        /// </summary>
        bool TryApproach(PlacedStructure piece, out Vector3 best)
        {
            best = default;
            var placement = piece.Placement;
            float size = world.Grid.CellSize;
            float left = placement.Cell.x * size, right = (placement.Cell.x + placement.Size.x) * size;
            float bottom = placement.Cell.y * size, top = (placement.Cell.y + placement.Size.y) * size;
            float margin = agent.radius + settings.ApproachClearance;
            float spacing = Mathf.Max(settings.SlotSpacing, agent.radius * 2f);
            GatherClaims();
            slots.Clear();
            // 왼쪽·오른쪽 면은 z를 따라, 아래·위 면은 x를 따라 자리를 편다.
            AddFace(new Vector3(left - margin, 0f, bottom), Vector3.forward, top - bottom, margin, spacing);
            AddFace(new Vector3(right + margin, 0f, bottom), Vector3.forward, top - bottom, margin, spacing);
            AddFace(new Vector3(left, 0f, bottom - margin), Vector3.right, right - left, margin, spacing);
            AddFace(new Vector3(left, 0f, top + margin), Vector3.right, right - left, margin, spacing);
            slots.Sort(byScore);
            Collider[] bodies = piece.GetComponents<Collider>();
            int pathChecks = 0;
            foreach (Slot slot in slots)
            {
                if (!NavMesh.SamplePosition(slot.Point, out NavMeshHit hit, settings.NavigationSnapDistance, agent.areaMask)
                    || Mathf.Abs(hit.position.y - slot.Point.y) > settings.VerticalSnapTolerance
                    || !WithinRange(hit.position + Vector3.up * actor.Profile.Height * 0.5f, bodies)) continue;
                if (pathChecks++ >= settings.MaxSlotPathChecks) break;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                best = hit.position;
                return true;
            }
            return false;
        }

        void AddFace(Vector3 start, Vector3 along, float length, float margin, float spacing)
        {
            int count = WallBreakerSpreadRules.SlotCount(length, margin, spacing);
            for (int i = 0; i < count; i++)
            {
                Vector3 local = start + along * WallBreakerSpreadRules.SlotOffset(i, count, length, margin);
                Vector3 point = world.transform.TransformPoint(local);
                float distance = Vector3.Distance(point, transform.position);
                int claimed = WallBreakerSpreadRules.CountClaims(point, claims, spacing);
                slots.Add(new Slot { Point = point, Score = WallBreakerSpreadRules.Score(distance, claimed, settings.SlotClaimPenalty) });
            }
        }

        // 다른 파괴병이 지금 잡고 있는 자리.
        void GatherClaims()
        {
            claims.Clear();
            foreach (var other in active)
                if (other != this && other.selected && other.Target != null && !other.Target.Health.IsDead)
                    claims.Add(other.ApproachPoint);
        }

        bool WithinRange(Vector3 origin, Collider[] bodies)
        {
            float range = actor.Stats.GetValue(StatType.AttackRange);
            foreach (var body in bodies)
                if (body != null && body.enabled && (body.ClosestPoint(origin) - origin).sqrMagnitude <= range * range) return true;
            return false;
        }

        bool InReach() => Target != null && !Target.Health.IsDead && targetBodies != null && WithinRange(actor.AimPoint, targetBodies);

        public void Tick()
        {
            if (world == null || world.IsRebuilding || world.RebuildPending || !InReach()) return;
            Hold();
            Vector3 direction = Target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            // 기존 성문 공격과 동일한 공격 속도·공격 시작 신호·최소 피해를 사용한다.
            if (actor.TryAttack()) Target.ReceiveDamage(BossUnit.StructureDamage(actor, DamageRules.Calculate(actor.Stats.GetValue(StatType.Attack), 0f, actor.Profile.MinimumDamage)));
        }

        void Hold() { if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold); }
    }
}
