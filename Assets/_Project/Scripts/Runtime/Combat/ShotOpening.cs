using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 마치콜레이션 구멍 하나(SIEGE-12). 이 물체의 위치가 사격 자리, 앞(+z)이 성 바깥쪽이다.
    /// 사격 자리에 선 그 편의 원거리 공격자는 통로(<see cref="MachicolationShaft"/>) 안에서 받치는 조각(성문 칸)을 지나
    /// 아래로 쏠 수 있다. 사선 검사(<see cref="CombatPhysics"/>)와 화살(<see cref="CombatProjectile"/>)이 같은 기준을 쓴다.
    /// AI가 한 구멍을 한 명만 쓰도록 맡은 유닛을 기억한다.
    /// </summary>
    public sealed class ShotOpening : MonoBehaviour
    {
        static readonly List<ShotOpening> active = new List<ShotOpening>();

        [SerializeField] CombatTeam team = CombatTeam.Allied;
        [SerializeField] float standRadius = 0.6f;
        [SerializeField] float searchRadius = 12f;
        Transform[] exemptRoots = System.Array.Empty<Transform>();
        MachicolationAttack[] attacks = System.Array.Empty<MachicolationAttack>();
        MachicolationShaft shaft;

        /// <summary>켜져 있는 구멍들.</summary>
        public static IReadOnlyList<ShotOpening> Active => active;
        public CombatTeam Team => team;
        public Vector3 StandPoint => transform.position;
        public Vector3 Outward => transform.forward;
        public float StandRadius => standRadius;
        /// <summary>AI가 지키는 자리에서 이 거리 안이면 쓰러 간다.</summary>
        public float SearchRadius => searchRadius;
        public MachicolationShaft Shaft => shaft;
        /// <summary>이 구멍을 쓰러 가거나 쓰고 있는 AI 유닛.</summary>
        public UnitCombatAI Claimant { get; private set; }

        public void Configure(CombatTeam owner, MachicolationShaft passage, float stand, float search, Transform[] roots,
            MachicolationAttack[] methods)
        {
            team = owner;
            shaft = passage;
            standRadius = Mathf.Max(0.05f, stand);
            searchRadius = Mathf.Max(0f, search);
            exemptRoots = roots ?? System.Array.Empty<Transform>();
            attacks = methods ?? System.Array.Empty<MachicolationAttack>();
        }

        /// <summary>이 구멍에 끼운 공격 방식 중 그 공격자가 쓸 수 있는 것이 있는지. 같은 편이어야 한다.</summary>
        public bool CanBeUsedBy(CharacterActor attacker)
        {
            if (attacker == null || attacker.Team != team || attacker.gameObject.scene != gameObject.scene) return false;
            foreach (MachicolationAttack attack in attacks)
                if (attack != null && attack.CanUse(attacker)) return true;
            return false;
        }

        void OnEnable() { if (!active.Contains(this)) active.Add(this); }

        void OnDisable()
        {
            active.Remove(this);
            Claimant = null;
        }

        /// <summary>세계 좌표 점을 통로 좌표로(크기 무시).</summary>
        public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(transform.rotation) * (world - transform.position);

        public bool Contains(Vector3 world) => shaft.Contains(ToLocal(world));

        /// <summary>그 충돌체가 이 구멍을 받치는 조각(성문 칸)의 것인지.</summary>
        public bool IsExempt(Collider collider)
        {
            if (collider == null) return false;
            foreach (Transform root in exemptRoots)
                if (root != null && collider.transform.IsChildOf(root)) return true;
            return false;
        }

        /// <summary>공격자 발이 사격 자리에 있는지(수평 반경, 높이 차 0.75m 안).</summary>
        public bool IsAtStand(Vector3 feet)
        {
            Vector3 offset = feet - StandPoint;
            if (Mathf.Abs(offset.y) > 0.75f) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= standRadius * standRadius;
        }

        /// <summary>
        /// 그 공격자가 지금 서서 쓰는 구멍. 같은 편이고 끼운 공격 방식을 쓸 수 있어야 한다(지금은 화살: 원거리 공격자). 없으면 null.
        /// </summary>
        public static ShotOpening At(CharacterActor attacker)
        {
            if (attacker == null) return null;
            Vector3 feet = attacker.transform.position;
            foreach (ShotOpening opening in active)
                if (opening.IsAtStand(feet) && opening.CanBeUsedBy(attacker)) return opening;
            return null;
        }

        /// <summary>
        /// 선(<paramref name="entry"/> → <paramref name="exit"/>)이 이 충돌체를 통로 안에서만 지나는지
        /// (<see cref="MachicolationRules.PassesThrough"/>).
        /// </summary>
        public bool Passes(Collider collider, Vector3 entry, Vector3 exit) =>
            MachicolationRules.PassesThrough(IsExempt(collider), Contains(entry), Contains(exit));

        /// <summary>화살이 그 지점에서 이 충돌체에 닿을 때 지나가도 되는지.</summary>
        public bool Passes(Collider collider, Vector3 contact) =>
            MachicolationRules.PassesThrough(IsExempt(collider), Contains(contact), true);

        /// <summary>구멍 아래(땅 쪽)에 <paramref name="viewer"/>의 적이 서 있는지.</summary>
        public bool HasEnemyBelow(CharacterActor viewer)
        {
            foreach (CharacterActor candidate in CharacterActor.ActiveActors)
                if (viewer.IsEnemy(candidate) && shaft.IsBelow(ToLocal(candidate.transform.position), 1.5f)) return true;
            return false;
        }

        /// <summary>
        /// <paramref name="unit"/>에게 비어 있는지. 다른 유닛이 맡지 않았고(<see cref="MachicolationRules.IsClaimFree"/>),
        /// 그 유닛 말고 다른 산 사람(플레이어 포함)이 사격 자리에 서 있지 않아야 한다.
        /// </summary>
        public bool IsFreeFor(UnitCombatAI unit)
        {
            UnitCombatAI claimant = Claimant;
            bool claimantActive = claimant != null && claimant.isActiveAndEnabled && claimant.Actor != null
                && claimant.Actor.Health != null && !claimant.Actor.Health.IsDead;
            if (!MachicolationRules.IsClaimFree(claimant != null, claimant == unit, claimantActive)) return false;
            foreach (CharacterActor other in CharacterActor.ActiveActors)
                if (other != unit.Actor && other.Health != null && !other.Health.IsDead
                    && other.gameObject.scene == gameObject.scene && IsAtStand(other.transform.position)) return false;
            return true;
        }

        public void Claim(UnitCombatAI unit) => Claimant = unit;

        public void Release(UnitCombatAI unit)
        {
            if (Claimant == unit) Claimant = null;
        }
    }
}
