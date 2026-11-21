using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 화살이 성벽·흉벽·땅·캐릭터에 맞으면 사라지지 않고 그 자리에 박혀 남게 한다(ART-05).
    /// 캐릭터에 박히면 가장 가까운 뼈를 따라 움직이고, 죽은 뒤에도 시체에 남는다.
    /// 박힌 뒤 수명이 지나거나, 붙은 물체가 사라지거나, 상한을 넘어 밀려나면 없어진다.
    /// 전투가 끝나면 GameBattleController가 CombatProjectile과 함께 지운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StuckArrow : MonoBehaviour
    {
        static readonly StuckArrowLedger<StuckArrow> Ledger = new StuckArrowLedger<StuckArrow>();
        static readonly List<StuckArrow> Evicted = new List<StuckArrow>();

        [SerializeField] StuckArrowSettings settings;

        Transform anchor;
        Vector3 anchorOffset;
        Quaternion anchorRotation;
        float expiresAt;
        bool stuck;

        public bool IsStuck => stuck;
        public Transform Anchor => anchor;
        public static int StuckCount => Ledger.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLedger() => Ledger.Clear();

        public void Configure(StuckArrowSettings value) => settings = value;

        /// <summary>
        /// 맞은 충돌체에 박는다. reach는 발사체 중심에서 맞은 면까지의 거리(발사체 반지름)다.
        /// 박지 못하면(설정 없음·1인칭 플레이어) false를 돌려주고, 부르는 쪽이 화살을 없앤다.
        /// </summary>
        public bool Stick(Collider hit, float reach)
        {
            if (stuck) return true;
            if (settings == null || hit == null) return false;
            var actor = hit.GetComponentInParent<CharacterActor>();
            // 1인칭 플레이어 몸에 박히면 화면을 가리므로 박지 않는다.
            if (actor != null && actor.GetComponentInChildren<Camera>(true) != null) return false;

            Vector3 forward = transform.forward;
            transform.position += forward * (Mathf.Max(0f, reach) + settings.Penetration);
            anchor = actor != null ? NearestBone(actor, transform.position, hit.transform) : hit.transform;
            Quaternion inverse = Quaternion.Inverse(anchor.rotation);
            // 크기(스케일)는 빼고 위치·회전만 따라간다. 늘어난 모델에 박혀도 화살 모양이 변하지 않는다.
            anchorOffset = inverse * (transform.position - anchor.position);
            anchorRotation = inverse * transform.rotation;
            expiresAt = Time.time + settings.Lifetime;
            stuck = true;

            Ledger.Add(this, settings.MaxCount, Evicted);
            foreach (StuckArrow old in Evicted)
                if (old != null && old != this) Destroy(old.gameObject);
            Evicted.Clear();
            return true;
        }

        void LateUpdate()
        {
            if (!stuck) return;
            if (Time.time >= expiresAt || anchor == null || !anchor.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            transform.SetPositionAndRotation(anchor.position + anchor.rotation * anchorOffset, anchor.rotation * anchorRotation);
        }

        void OnDestroy()
        {
            if (stuck) Ledger.Remove(this);
        }

        /// <summary>사람형 모델이면 사람 뼈, 아니면 스킨 메쉬 뼈 가운데 맞은 점에서 가장 가까운 뼈를 고른다.</summary>
        static Transform NearestBone(CharacterActor actor, Vector3 point, Transform fallback)
        {
            Transform best = fallback;
            float bestDistance = float.MaxValue;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                for (HumanBodyBones bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
                    Consider(animator.GetBoneTransform(bone), point, ref best, ref bestDistance);
            }
            else
            {
                foreach (SkinnedMeshRenderer skinned in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    foreach (Transform bone in skinned.bones) Consider(bone, point, ref best, ref bestDistance);
            }
            return best;
        }

        static void Consider(Transform bone, Vector3 point, ref Transform best, ref float bestDistance)
        {
            if (bone == null) return;
            float distance = (bone.position - point).sqrMagnitude;
            if (distance >= bestDistance) return;
            bestDistance = distance;
            best = bone;
        }
    }
}
