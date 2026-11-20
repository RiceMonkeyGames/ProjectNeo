using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 보스 전용 체력바(SIEGE-09, Q-74). 보스가 전장에 나와 살아 있는 동안 화면 위 가운데에 이름·체력을 보여 주고,
    /// 죽거나 전투가 끝나면 숨긴다. 씬 파일을 바꾸지 않게 전투가 실행 중에 붙이고 화면은 코드로 만든다.
    /// </summary>
    public sealed class BossHealthHud : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color BackColor = new Color(0.18f, 0.05f, 0.05f, 0.95f);
        static readonly Color FillColor = new Color(0.82f, 0.16f, 0.12f, 1f);
        GameBattleController battle;
        GameObject panel;
        RectTransform fill;
        TMP_Text nameLabel;
        TMP_Text valueLabel;
        BossUnit shown;
        float shownCurrent = -1f, shownMaximum = -1f;

        /// <summary>지금 보이는 보스. 숨겨져 있으면 null.</summary>
        public BossUnit Shown => panel != null && panel.activeSelf ? shown : null;
        /// <summary>지금 이름 줄 글자(테스트용).</summary>
        public string NameText => nameLabel != null ? nameLabel.text : string.Empty;
        /// <summary>지금 채워진 비율 0~1(테스트용).</summary>
        public float FillRatio => fill != null ? fill.anchorMax.x : 0f;

        public void Configure(GameBattleController controller)
        {
            battle = controller;
            if (panel == null) Build();
        }

        void LateUpdate()
        {
            BossUnit boss = battle != null && battle.IsRunning ? battle.ActiveBoss : null;
            Health health = boss != null && boss.Actor != null ? boss.Actor.Health : null;
            if (health == null)
            {
                if (panel.activeSelf) panel.SetActive(false);
                shown = null;
                return;
            }
            if (!panel.activeSelf) panel.SetActive(true);
            if (boss != shown)
            {
                shown = boss;
                nameLabel.text = boss.Definition.DisplayName;
                shownCurrent = shownMaximum = -1f;
            }
            float current = Mathf.Max(0f, health.Current), maximum = Mathf.Max(1f, health.Maximum);
            if (Mathf.Approximately(current, shownCurrent) && Mathf.Approximately(maximum, shownMaximum)) return;
            shownCurrent = current; shownMaximum = maximum;
            fill.anchorMax = new Vector2(Mathf.Clamp01(current / maximum), 1f);
            valueLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        }

        // 화면 위 가운데: 보스 이름, 그 아래 빨간 막대와 체력 숫자.
        void Build()
        {
            var root = new GameObject("BossHealthCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = Box("BossHealth", root.transform, PanelColor);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -16f);
            rect.sizeDelta = new Vector2(520f, 62f);

            nameLabel = Label("Name", panel.transform, 20f, FontStyles.Bold, new Vector2(0f, 0.55f), Vector2.one);
            GameObject back = Box("Bar", panel.transform, BackColor);
            var barRect = (RectTransform)back.transform;
            barRect.anchorMin = new Vector2(0f, 0f); barRect.anchorMax = new Vector2(1f, 0.55f);
            barRect.offsetMin = new Vector2(12f, 8f); barRect.offsetMax = new Vector2(-12f, -2f);
            GameObject filled = Box("Fill", back.transform, FillColor);
            fill = (RectTransform)filled.transform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            valueLabel = Label("Value", back.transform, 15f, FontStyles.Normal, Vector2.zero, Vector2.one);
            panel.SetActive(false);
        }

        static GameObject Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return go;
        }

        static TMP_Text Label(string name, Transform parent, float size, FontStyles style, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = size; label.fontStyle = style; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }
    }
}
