using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectNeo.Characters
{
    /// <summary>
    /// 1인칭에서 보이는 내 몸(SIEGE-36). 원래 모델은 그림자만 그리고(투구까지 온전한 그림자), 화면에는 같은 모델의 복사본을 그린다.
    /// 복사본은 매 프레임 원래 모델의 뼈 자세를 그대로 따라 하고, 머리 뼈만 아주 작게 줄여 투구가 카메라를 가리지 않게 한다.
    /// 무기·칼집처럼 손·허리를 오가는 부품은 원래 모델 것을 그대로 보여 준다. 실행 중에 만들며 씬·프리팹을 바꾸지 않는다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        static readonly Vector3 Collapsed = Vector3.one * 0.0001f;

        GameObject copy;
        Transform copyHead;
        readonly List<(Transform source, Transform target)> bones = new List<(Transform, Transform)>();
        SkinnedMeshRenderer[] sourceSkins = new SkinnedMeshRenderer[0];
        ShadowCastingMode[] sourceShadows = new ShadowCastingMode[0];
        bool firstPerson;

        /// <summary>모델(Animator)에 1인칭 몸을 붙인다. 휴머노이드가 아니면 null.</summary>
        public static FirstPersonBody Attach(Animator model)
        {
            if (model == null || !model.isHuman) return null;
            var body = model.GetComponent<FirstPersonBody>();
            if (body == null) body = model.gameObject.AddComponent<FirstPersonBody>();
            body.Build(model);
            return body;
        }

        void Build(Animator model)
        {
            if (copy != null) return;
            sourceSkins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sourceShadows = new ShadowCastingMode[sourceSkins.Length];
            for (int i = 0; i < sourceSkins.Length; i++) sourceShadows[i] = sourceSkins[i].shadowCastingMode;

            bool wasActive = model.gameObject.activeSelf;
            copy = Instantiate(model.gameObject, model.transform.parent);
            copy.name = model.name + "_FirstPerson";
            copy.SetActive(false);
            if (copy.TryGetComponent(out FirstPersonBody nested)) Destroy(nested);
            if (copy.TryGetComponent(out Animator copyAnimator)) Destroy(copyAnimator);
            foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
            // 무기·칼집 등 따로 붙은 부품은 원래 모델 것을 보여 준다.
            foreach (MeshRenderer part in copy.GetComponentsInChildren<MeshRenderer>(true)) Destroy(part.gameObject);
            foreach (SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.updateWhenOffscreen = true;
            }

            var lookup = new Dictionary<string, Transform>();
            Collect(copy.transform, string.Empty, lookup);
            Pair(model.transform, string.Empty, lookup);
            Transform head = model.GetBoneTransform(HumanBodyBones.Head);
            foreach ((Transform source, Transform target) in bones) if (source == head) copyHead = target;
            if (!wasActive) model.gameObject.SetActive(false);
        }

        static void Collect(Transform node, string path, Dictionary<string, Transform> lookup)
        {
            lookup[path] = node;
            foreach (Transform child in node) Collect(child, path + "/" + child.name, lookup);
        }

        void Pair(Transform node, string path, Dictionary<string, Transform> lookup)
        {
            if (path.Length > 0 && lookup.TryGetValue(path, out Transform target)) bones.Add((node, target));
            foreach (Transform child in node) Pair(child, path + "/" + child.name, lookup);
        }

        /// <summary>1인칭이면 복사본을 보이고 원래 모델은 그림자만 그린다. 아니면 원래대로 돌린다.</summary>
        public void SetFirstPerson(bool value)
        {
            firstPerson = value;
            if (copy != null) copy.SetActive(value);
            for (int i = 0; i < sourceSkins.Length; i++)
                if (sourceSkins[i] != null)
                    sourceSkins[i].shadowCastingMode = value ? ShadowCastingMode.ShadowsOnly : sourceShadows[i];
            if (value) LateUpdate();
        }

        void LateUpdate()
        {
            if (!firstPerson || copy == null) return;
            foreach ((Transform source, Transform target) in bones)
            {
                if (source == null || target == null) continue;
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }
            if (copyHead != null) copyHead.localScale = Collapsed;
        }

        void OnDestroy()
        {
            if (copy != null) Destroy(copy);
        }
    }
}
