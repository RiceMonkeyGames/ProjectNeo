using UnityEngine;

namespace ProjectNeo.Combat
{
    public static class CombatPhysics
    {
        /// <summary>유닛이 딛고 선 물체(땅·성벽 윗면). 사람 몸은 제외한다.</summary>
        public static Collider GroundUnder(CharacterActor actor)
        {
            Collider ground = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(actor.transform.position + Vector3.up * 0.3f, Vector3.down,
                0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != actor.gameObject.scene
                    || hit.collider.GetComponentInParent<CharacterActor>() != null || hit.distance >= nearest) continue;
                ground = hit.collider;
                nearest = hit.distance;
            }
            return ground;
        }

        /// <summary>
        /// 원거리 공격이 발밑 물체를 지나도 되는 가장 낮은 높이. 발 높이에서 <see cref="CombatProfile.FootingClearance"/>만큼 아래다.
        /// 성벽 위 궁수가 모서리 너머로 몸을 내밀고 쏘는 만큼만 허용하고, 그보다 깊이 벽 몸통을 뚫지는 못한다
        /// (BUG-wall-archer-shoots-through-footing).
        /// </summary>
        public static float FootingFloor(CharacterActor attacker) =>
            attacker.transform.position.y - attacker.Profile.FootingClearance;

        static readonly Collider[] coverBuffer = new Collider[16];

        /// <summary>
        /// 자기 편 사격을 통과시키는 엄폐물(성가퀴)이 <paramref name="radius"/> 안에 있는지(COMBAT-10 웅크려 재장전).
        /// 성가퀴는 아군 편으로만 세워지므로 성벽을 차지한 적은 해당하지 않는다.
        /// </summary>
        public static bool IsBehindOwnCover(CharacterActor actor, float radius)
        {
            if (radius <= 0f) return false;
            int count = Physics.OverlapSphereNonAlloc(actor.transform.position + Vector3.up * 0.5f, radius, coverBuffer,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (coverBuffer[i].gameObject.scene == actor.gameObject.scene && ShotCover.Passes(coverBuffer[i], actor.Team))
                    return true;
            return false;
        }

        public static bool HasClearShot(CharacterActor attacker, CharacterActor target) =>
            TryGetShotPoint(attacker, target, out _);

        /// <summary>
        /// 대상에서 쏠 수 있는 지점을 찾는다(SIEGE-10). 몸 가운데(<see cref="CharacterActor.AimPoint"/>)를 먼저 보고,
        /// 가려졌으면 위쪽 몸통(<see cref="CombatProfile.UpperAimRatio"/>)을 본다. 둘 다 가려지면 false이고 지점은 몸 가운데다.
        /// 낮은 난간 뒤 수비병의 상체를 노리게 해 준다. 솟은 부분(머론) 뒤처럼 온몸이 가려지면 여전히 쏘지 못한다.
        /// 원거리 공격자가 딛고 선 물체는 모서리를 스치는 만큼만 지날 수 있다(<see cref="FootingFloor"/>).
        /// </summary>
        public static bool TryGetShotPoint(CharacterActor attacker, CharacterActor target, out Vector3 point)
        {
            Collider footing = attacker.Profile.AttackKind == AttackKind.Projectile ? GroundUnder(attacker) : null;
            // 마치콜레이션 사격 자리에 섰으면 구멍 통로 안에서 성문 조각을 지나 아래로 쏠 수 있다(SIEGE-12).
            return TryGetShotPoint(attacker.AimPoint, footing, footing != null ? FootingFloor(attacker) : 0f,
                ShotOpening.At(attacker), attacker, target, out point);
        }

        /// <summary>
        /// 공격자가 <paramref name="standPoint"/>(발 위치)에 섰다면 대상을 쏠 수 있는지. 사격 자리를 미리 고를 때 쓴다(SIEGE-10 궁수 자리 찾기).
        /// 사거리는 보지 않는다.
        /// </summary>
        public static bool HasClearShotFrom(Vector3 standPoint, CharacterActor attacker, CharacterActor target) =>
            TryGetShotPoint(standPoint + Vector3.up * (attacker.Profile.Height * 0.5f), null, 0f, null, attacker, target, out _);

        static bool TryGetShotPoint(Vector3 origin, Collider footing, float footingFloor, ShotOpening opening,
            CharacterActor attacker, CharacterActor target, out Vector3 point)
        {
            point = target.AimPoint;
            if (IsClear(origin, footing, footingFloor, opening, attacker, target, point)) return true;
            // 웅크린 대상은 낮아진 키로 잰다(COMBAT-10). 받침 뒤에 웅크리면 위쪽 몸통도 가려진다.
            Vector3 upper = target.transform.position + Vector3.up * (target.CurrentHeight * target.Profile.UpperAimRatio);
            if (!IsClear(origin, footing, footingFloor, opening, attacker, target, upper)) return false;
            point = upper;
            return true;
        }

        static bool IsClear(Vector3 origin, Collider footing, float footingFloor, ShotOpening opening,
            CharacterActor attacker, CharacterActor target, Vector3 point)
        {
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude < 1e-6f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (opening != null && PassesOpening(opening, hit, point, origin)) continue;
                if (hit.collider == footing && GrazesFooting(footing, footingFloor, point, origin)) continue;
                if (hit.collider.gameObject.scene != attacker.gameObject.scene
                    || ShotCover.Passes(hit.collider, attacker.Team)) continue;
                var actor = hit.collider.GetComponentInParent<CharacterActor>();
                if (actor == target || actor == attacker) continue;
                if (actor != null && (actor.Team == attacker.Team || actor.Health == null || actor.Health.IsDead)) continue;
                return false;
            }
            return true;
        }

        // 선(from → to)이 발밑 물체를 빠져나가는 점이 허용 깊이(footingFloor) 위인지. RaycastAll은 들어가는 점만 주므로
        // 반대쪽(대상)에서 거꾸로 쏴 나가는 점을 찾는다. 대상 쪽 끝이 물체 안이면 빠져나가지 못한 것이다.
        static bool GrazesFooting(Collider footing, float footingFloor, Vector3 to, Vector3 from)
        {
            Vector3 back = from - to;
            return footing.Raycast(new Ray(to, back.normalized), out RaycastHit exit, back.magnitude)
                && exit.point.y >= footingFloor;
        }

        // 선이 그 충돌체에 들어가는 점과 나오는 점이 모두 구멍 통로 안인지(SIEGE-12). 나오는 점은 대상 쪽에서 거꾸로 쏴 찾는다.
        // 대상 쪽 끝이 충돌체 안이면 나오지 못한 것이라 막힌다.
        static bool PassesOpening(ShotOpening opening, RaycastHit hit, Vector3 to, Vector3 from)
        {
            if (!opening.IsExempt(hit.collider)) return false;
            Vector3 back = from - to;
            return hit.collider.Raycast(new Ray(to, back.normalized), out RaycastHit exit, back.magnitude)
                && opening.Passes(hit.collider, hit.point, exit.point);
        }

        /// <summary>
        /// 이 대상을 칠 때의 사거리. 땅에 선 원거리 공격자가 대상보다 충분히 높이 있으면 높은 곳 보너스를 받는다.
        /// 근접 공격자와 날아다니는 공격자는 기본 사거리 그대로다.
        /// </summary>
        public static float AttackRange(CharacterActor attacker, CharacterActor target)
        {
            float range = attacker.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange);
            if (attacker.Profile.AttackKind != AttackKind.Projectile || attacker.Airborne != null) return range;
            return range * RangeRules.HighGroundMultiplier(attacker.transform.position.y - target.transform.position.y,
                attacker.Profile.HighGroundMinimumDrop, attacker.Profile.HighGroundRangeBonus);
        }

        /// <summary>어떤 대상에게든 닿을 수 있는 가장 긴 사거리. 대상 후보를 미리 거를 때 쓴다.</summary>
        public static float MaximumAttackRange(CharacterActor attacker)
        {
            float range = attacker.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange);
            if (attacker.Profile.AttackKind != AttackKind.Projectile || attacker.Airborne != null) return range;
            return range * (1f + attacker.Profile.HighGroundRangeBonus);
        }

        public static bool IsInRange(CharacterActor attacker, CharacterActor target)
        {
            Collider body = target.Body;
            if (body == null || !body.enabled) return false;
            float range = AttackRange(attacker, target);
            // 공중 대상은 근접이면 바닥 투영점 기준으로 잰다(Q-43). 원거리는 몸통까지 그대로 잰다.
            AirborneBody air = target.Airborne;
            if (air != null && attacker.Profile.AttackKind == AttackKind.Melee)
                return AirborneReach.InMeleeReach(attacker.transform.position, air.GroundPoint, air.IsMeleeExposed,
                    range, target.Profile.Radius);
            return (body.ClosestPoint(attacker.AimPoint) - attacker.AimPoint).sqrMagnitude <= range * range;
        }

        /// <summary>길찾기로 다가갈 지점. 공중 대상은 바닥 투영점이다.</summary>
        public static Vector3 ReachPoint(CharacterActor target)
        {
            AirborneBody air = target.Airborne;
            return air != null && air.HasGround ? air.GroundPoint : target.transform.position;
        }

        /// <summary>
        /// 대상으로 고를 수 있는지. 근접 공격자는 높이 떠 있는 공중 대상을 쫓지 않는다.
        /// 쫓아가 봐야 닿지 않고, 그동안 땅의 적을 놓치기 때문이다.
        /// </summary>
        public static bool CanTarget(CharacterActor attacker, CharacterActor target)
        {
            AirborneBody air = target.Airborne;
            return air == null || attacker.Profile.AttackKind != AttackKind.Melee
                || AirborneReach.CanMeleeTarget(air.HasGround, air.IsMeleeExposed);
        }
    }
}
