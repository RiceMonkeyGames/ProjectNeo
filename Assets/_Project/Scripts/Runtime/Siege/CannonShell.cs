using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 포물선으로 날아가 처음 닿은 곳에서 터지는 포탄. 반경 안 대상에게 거리 감쇠·방어력을 적용한 피해를 준다.
    /// 외형은 Visual 자식이 <see cref="Exploded"/>를 듣고 처리한다.
    /// </summary>
    public sealed class CannonShell : MonoBehaviour
    {
        static readonly List<CannonShell> active = new List<CannonShell>();
        readonly HashSet<CharacterActor> hitActors = new HashSet<CharacterActor>();
        readonly HashSet<PlacedStructure> hitStructures = new HashSet<PlacedStructure>();
        CannonSettings settings;
        CombatTeam team;
        Vector3 velocity;
        float remaining;
        bool launched;
        PlacedSiegeWeapon source;
        Collider footing;

        public static IReadOnlyList<CannonShell> Active => active;
        public Vector3 Velocity => velocity;
        public bool HasExploded { get; private set; }
        /// <summary>터진 자리. 외형(폭발 효과·소리)이 듣는다.</summary>
        public event Action<CannonShell, Vector3> Exploded;

        public void Launch(Vector3 origin, Vector3 launchVelocity, CannonSettings cannon, CombatTeam shooter,
            PlacedSiegeWeapon weapon = null)
        {
            transform.position = origin;
            velocity = launchVelocity;
            settings = cannon;
            team = shooter;
            source = weapon;
            // 대포를 받치는 성벽은 통과한다. 성벽 위에서 바로 아래로 쏠 때 발밑 모서리에 터지지 않게 한다.
            footing = weapon != null && weapon.Support != null ? weapon.Support.GetComponentInChildren<Collider>() : null;
            remaining = settings.ShellLifetime;
            launched = true;
        }

        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (!launched || HasExploded) return;
            float delta = Mathf.Min(Time.deltaTime, remaining);
            Vector3 from = transform.position;
            float gravity = settings.ShellGravity;
            Vector3 to = from + velocity * delta + Vector3.down * (0.5f * gravity * delta * delta);
            velocity += Vector3.down * (gravity * delta);
            Vector3 step = to - from;
            float length = step.magnitude;
            if (length > 0f && TryFindImpact(from, step / length, length, out Vector3 impact))
            {
                transform.position = impact;
                Explode(impact);
                return;
            }
            transform.position = to;
            if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity);
            remaining -= delta;
            if (remaining <= 0f) { launched = false; Destroy(gameObject); }
        }

        bool TryFindImpact(Vector3 origin, Vector3 direction, float distance, out Vector3 point)
        {
            point = default;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || ShouldPass(hit.collider)) continue;
                nearest = hit.distance;
                point = hit.point;
            }
            return nearest < float.PositiveInfinity;
        }

        bool ShouldPass(Collider collider)
        {
            if (collider.gameObject.scene != gameObject.scene || collider == footing) return true;
            if (collider.transform.IsChildOf(transform)) return true;
            if (source != null && collider.transform.IsChildOf(source.transform)) return true;
            var actor = collider.GetComponentInParent<CharacterActor>();
            if (actor == null) return false;
            // 죽은 몸과, 맞지 않을 아군은 지나간다.
            return actor.Health == null || actor.Health.IsDead
                || !CannonBlast.ShouldDamage(team, actor.Team, settings.FriendlyFire);
        }

        /// <summary>지금 자리에서 터뜨린다. 시험과 직접 조작 확인에도 쓴다.</summary>
        public void Explode(Vector3 point)
        {
            if (HasExploded) return;
            HasExploded = true;
            launched = false;
            ApplyBlast(point);
            Exploded?.Invoke(this, point);
            Destroy(gameObject);
        }

        void ApplyBlast(Vector3 point)
        {
            hitActors.Clear();
            hitStructures.Clear();
            float radius = settings.BlastRadius;
            bool structures = CannonBlast.ShouldDamageOwnStructures(settings.FriendlyFire);
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider.gameObject.scene != gameObject.scene) continue;
                var actor = collider.GetComponentInParent<CharacterActor>();
                if (actor != null)
                {
                    if (actor.Health == null || actor.Health.IsDead || !hitActors.Add(actor)) continue;
                    if (!CannonBlast.ShouldDamage(team, actor.Team, settings.FriendlyFire)) continue;
                    float distance = Vector3.Distance(Closest(actor, point), point);
                    actor.ReceiveDamage(CannonBlast.Damage(settings.CenterDamage, distance, radius,
                        settings.EdgeDamageRatio, actor.Stats.GetValue(StatType.Defense), settings.MinimumDamage));
                    continue;
                }
                if (!structures) continue;
                var structure = collider.GetComponentInParent<PlacedStructure>();
                if (structure == null || structure.Health == null || structure.Health.IsDead || !hitStructures.Add(structure)) continue;
                float wallDistance = Vector3.Distance(collider.ClosestPoint(point), point);
                structure.ReceiveDamage(CannonBlast.Damage(settings.CenterDamage, wallDistance, radius,
                    settings.EdgeDamageRatio, 0f, settings.MinimumDamage));
            }
        }

        static Vector3 Closest(CharacterActor actor, Vector3 point) =>
            actor.Body != null && actor.Body.enabled ? actor.Body.ClosestPoint(point) : actor.transform.position;
    }
}
