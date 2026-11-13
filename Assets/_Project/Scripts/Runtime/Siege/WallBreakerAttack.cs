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
        [SerializeField] WallBreakerSettings settings;
        readonly List<PlacedStructure> pieces = new List<PlacedStructure>();
        readonly List<WallTargetCandidate> candidates = new List<WallTargetCandidate>();
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

        bool TryApproach(PlacedStructure piece, out Vector3 best)
        {
            best = default;
            float nearest = float.PositiveInfinity;
            var placement = piece.Placement;
            float size = world.Grid.CellSize;
            float left = placement.Cell.x * size, right = (placement.Cell.x + placement.Size.x) * size;
            float bottom = placement.Cell.y * size, top = (placement.Cell.y + placement.Size.y) * size;
            Vector3 local = world.LocalPoint(transform.position);
            float margin = agent.radius + settings.ApproachClearance;
            float x = Mathf.Clamp(local.x, left + margin, right - margin);
            float z = Mathf.Clamp(local.z, bottom + margin, top - margin);
            var points = new[] { new Vector3(left - margin, 0f, z), new Vector3(right + margin, 0f, z),
                new Vector3(x, 0f, bottom - margin), new Vector3(x, 0f, top + margin) };
            Collider[] bodies = piece.GetComponents<Collider>();
            foreach (Vector3 at in points)
            {
                Vector3 point = world.transform.TransformPoint(at);
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, settings.NavigationSnapDistance, agent.areaMask)
                    || Mathf.Abs(hit.position.y - point.y) > settings.VerticalSnapTolerance
                    || !WithinRange(hit.position + Vector3.up * actor.Profile.Height * 0.5f, bodies)
                    || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = (hit.position - transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; best = hit.position;
            }
            return !float.IsPositiveInfinity(nearest);
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
            if (actor.TryAttack()) Target.ReceiveDamage(DamageRules.Calculate(actor.Stats.GetValue(StatType.Attack), 0f, actor.Profile.MinimumDamage));
        }

        void Hold() { if (ai.Directive != UnitDirective.Hold) ai.SetDirective(UnitDirective.Hold); }
    }
}
