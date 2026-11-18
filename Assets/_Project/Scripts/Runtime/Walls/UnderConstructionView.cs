using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 짓는 중인 조각·무기의 임시 표시(SIEGE-08). 렌더러를 반투명 미리보기 재질로 바꾸고 위에 진행률 글자를 띄운다.
    /// 끝나면 원래 재질로 되돌리고 글자를 없앤다. 규칙에는 관여하지 않는다. Synty 교체 때 이 컴포넌트만 바꾸면 된다.
    /// </summary>
    public sealed class UnderConstructionView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        MaterialPropertyBlock block;
        TextMeshPro label;
        float height = 3f;

        public float Progress { get; private set; }
        public string LabelText => label != null ? label.text : string.Empty;
        public bool IsShowing => label != null;

        /// <summary>반투명 표시를 켠다. 이미 켜져 있으면 진행률만 맞춘다.</summary>
        public void Begin(Material ghost, Color tint, TMP_FontAsset font, float labelHeight)
        {
            height = Mathf.Max(0.5f, labelHeight);
            if (label == null)
            {
                block ??= new MaterialPropertyBlock();
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.GetComponent<TMP_SubMesh>() != null || renderer.GetComponent<TextMeshPro>() != null) continue;
                    originals[renderer] = renderer.sharedMaterials;
                    if (ghost != null)
                    {
                        var replaced = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < replaced.Length; i++) replaced[i] = ghost;
                        renderer.sharedMaterials = replaced;
                    }
                    block.SetColor(BaseColor, tint);
                    renderer.SetPropertyBlock(block);
                }
                var holder = new GameObject("WorkProgress");
                holder.transform.SetParent(transform, false);
                label = holder.AddComponent<TextMeshPro>();
                if (font != null) label.font = font;
                label.fontSize = 6f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1f, 0.86f, 0.55f, 1f);
                label.rectTransform.sizeDelta = new Vector2(6f, 2f);
            }
            SetProgress(Progress);
        }

        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            if (label != null) label.text = Mathf.FloorToInt(Progress * 100f + 0.0001f) + "%";
        }

        /// <summary>반투명 표시를 끄고 원래 재질로 되돌린다.</summary>
        public void End()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in originals)
            {
                if (entry.Key == null) continue;
                entry.Key.sharedMaterials = entry.Value;
                entry.Key.SetPropertyBlock(null);
            }
            originals.Clear();
            if (label != null) Destroy(label.gameObject);
            label = null;
        }

        void LateUpdate()
        {
            if (label == null) return;
            // 크기와 상관없이 조각 위쪽 세상 기준 높이에 둔다.
            label.transform.position = transform.position + Vector3.up * (height + 0.6f);
            // 조각 몸통의 배율을 따라가지 않게 세상 기준 크기 1로 맞춘다.
            Vector3 parent = transform.lossyScale;
            label.transform.localScale = new Vector3(Inverse(parent.x), Inverse(parent.y), Inverse(parent.z));
            Camera viewer = ActiveCamera();
            if (viewer != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - viewer.transform.position);
        }

        static float Inverse(float value) => Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;

        static Camera ActiveCamera()
        {
            Camera best = null;
            foreach (Camera camera in Camera.allCameras)
                if (camera.isActiveAndEnabled && (best == null || camera.depth > best.depth)) best = camera;
            return best;
        }

        void OnDestroy() => originals.Clear();
    }
}
