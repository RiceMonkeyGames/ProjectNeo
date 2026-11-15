using ProjectNeo.Siege;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 대포 조작 중 조준선 표시(SIEGE-07). 조준선 색으로 쏠 수 있는지 알리고, 바로 아래에 재장전 막대와 한 줄 안내를 둔다.
    /// 임시 외형이라 조준선 옆에 실행 중에 만들어 붙이며, 씬과 프리팹은 건드리지 않는다.
    /// </summary>
    public sealed class CannonReticleView : MonoBehaviour
    {
        static readonly Vector2 BarSize = new Vector2(120f, 8f);
        TMP_Text reticle;
        TMP_Text label;
        GameObject panel;
        RectTransform fill;
        Image fillImage;
        Color idleColor;
        Vector3 idleScale;
        CannonAimState lastState;
        float pulseRemaining;
        bool showing;

        public bool IsShowing => showing;
        public string LabelText => label != null ? label.text : string.Empty;
        public Color ReticleColor => reticle != null ? reticle.color : Color.white;
        /// <summary>재장전 막대가 찬 비율(0~1).</summary>
        public float BarFill => fill != null ? fill.anchorMax.x : 0f;

        /// <summary>조준선 오브젝트에 표시를 붙인다. 조준선 글자가 없으면 색 없이 막대와 안내만 보여준다.</summary>
        public static CannonReticleView Attach(GameObject reticleObject)
        {
            var view = reticleObject.GetComponent<CannonReticleView>();
            if (view != null) return view;
            view = reticleObject.AddComponent<CannonReticleView>();
            view.Build();
            return view;
        }

        void Build()
        {
            reticle = GetComponent<TMP_Text>();
            idleColor = reticle != null ? reticle.color : Color.white;
            idleScale = transform.localScale;
            // 조준선이 커졌다 돌아올 때 함께 커지지 않도록 조준선의 형제로 둔다.
            panel = new GameObject("CannonReticlePanel", typeof(RectTransform));
            var root = (RectTransform)panel.transform;
            root.SetParent(transform.parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -30f);
            root.sizeDelta = BarSize;

            var back = new GameObject("ReloadBar", typeof(RectTransform), typeof(Image));
            var backRect = (RectTransform)back.transform;
            backRect.SetParent(root, false);
            backRect.anchorMin = Vector2.zero; backRect.anchorMax = Vector2.one;
            backRect.offsetMin = backRect.offsetMax = Vector2.zero;
            var backImage = back.GetComponent<Image>();
            backImage.color = new Color(0f, 0f, 0f, 0.6f);
            backImage.raycastTarget = false;

            var bar = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill = (RectTransform)bar.transform;
            fill.SetParent(backRect, false);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(1f, 1f); fill.offsetMax = new Vector2(-1f, -1f);
            fillImage = bar.GetComponent<Image>();
            fillImage.raycastTarget = false;

            if (reticle != null)
            {
                // 조준선 글자를 복사해 같은 글꼴(한글 포함)을 쓴다.
                label = Instantiate(reticle, root);
                var copied = label.GetComponent<CannonReticleView>();
                if (copied != null) Destroy(copied);
                label.name = "State";
                label.rectTransform.localScale = Vector3.one;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                label.rectTransform.sizeDelta = new Vector2(360f, 24f);
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.Top;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.text = string.Empty;
                label.gameObject.SetActive(true);
            }
            panel.SetActive(false);
        }

        /// <summary>조작 상태를 조준선에 반영한다. 타고 있는 동안 매 프레임 부른다.</summary>
        public void Show(CannonOperator cannonOperator, bool korean)
        {
            CannonModule cannon = cannonOperator.Manned;
            CannonOperatorSettings settings = cannonOperator.Settings;
            if (cannon == null || settings == null) { Hide(); return; }
            CannonAimState state = cannonOperator.AimState;
            Color color = settings.ColorFor(state);
            if (!showing) { showing = true; lastState = state; pulseRemaining = 0f; panel.SetActive(true); }
            // 장전이 끝난 순간 조준선을 잠깐 키워 눈에 띄게 한다.
            if (lastState == CannonAimState.Reloading && state != CannonAimState.Reloading)
                pulseRemaining = settings.ReadyPulseSeconds;
            lastState = state;
            float scale = 1f;
            if (pulseRemaining > 0f && settings.ReadyPulseSeconds > 0f)
            {
                scale = Mathf.Lerp(1f, settings.ReadyPulseScale, pulseRemaining / settings.ReadyPulseSeconds);
                pulseRemaining = Mathf.Max(0f, pulseRemaining - Time.unscaledDeltaTime);
            }
            transform.localScale = idleScale * scale;
            if (reticle != null) reticle.color = color;
            float progress = Mathf.Clamp01(cannon.Reload.Progress);
            fill.anchorMax = new Vector2(progress, 1f);
            fillImage.color = state == CannonAimState.Reloading ? settings.ReloadingColor : settings.ReadyColor;
            if (label != null)
            {
                label.color = color;
                label.text = Describe(state, cannon.Reload.Remaining, korean);
            }
        }

        /// <summary>대포에서 내리면 조준선을 원래 모습으로 돌린다.</summary>
        public void Hide()
        {
            if (!showing) return;
            showing = false;
            pulseRemaining = 0f;
            panel.SetActive(false);
            transform.localScale = idleScale;
            if (reticle != null) reticle.color = idleColor;
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        public static string Describe(CannonAimState state, float reloadRemaining, bool korean)
        {
            switch (state)
            {
                case CannonAimState.Ready: return korean ? "발사 가능" : "Ready";
                case CannonAimState.Reloading:
                    return korean ? $"재장전 {reloadRemaining:0.0}초" : $"Reloading {reloadRemaining:0.0}s";
                case CannonAimState.OutOfRange: return korean ? "사거리 밖" : "Out of range";
                case CannonAimState.OutOfArc: return korean ? "회전 범위 밖" : "Outside arc";
                default: return korean ? "조준한 곳 없음" : "No target";
            }
        }
    }
}
