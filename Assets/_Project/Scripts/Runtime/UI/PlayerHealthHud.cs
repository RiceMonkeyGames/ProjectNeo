using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>플레이어 체력을 표시하며 체력 규칙에는 관여하지 않는다.</summary>
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] CharacterActor player;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] RectTransform fill;
        [Header("Portrait layout (UI-12)")]
        [Tooltip("초상화 옆 위의 큰 숫자(\"100/100\", 최대값은 작게). 비어 있으면 쓰지 않는다.")]
        [SerializeField] TMP_Text numberLabel;
        [Tooltip("칸으로 나눈 체력바의 한 칸 견본(Filled Horizontal). 숨겨 두고 복제한다. 비어 있으면 칸을 쓰지 않는다.")]
        [SerializeField] Image segmentTemplate;
        [Tooltip("한 칸이 나타내는 체력. 칸 수는 최대 체력에 맞춰 늘되 MaxSegments를 넘지 않는다.")]
        [SerializeField, Min(1f)] float healthPerSegment = 10f;
        const int MaxSegments = 20;
        readonly List<Image> segments = new List<Image>();
        Health health;

        public string ValueText => valueLabel != null ? valueLabel.text : string.Empty;
        public float FillRatio => fill != null ? fill.anchorMax.x : 0f;
        public bool IsShowing => content != null && content.activeSelf;
        public string NumberText => numberLabel != null ? numberLabel.text : string.Empty;
        /// <summary>지금 보이는 체력 칸 수.</summary>
        public int SegmentCount { get { int n = 0; foreach (Image s in segments) if (s.gameObject.activeSelf) n++; return n; } }
        /// <summary>칸 하나의 채움(0~1). 없는 칸이면 0.</summary>
        public float SegmentFill(int index) => index >= 0 && index < segments.Count && segments[index].gameObject.activeSelf ? segments[index].fillAmount : 0f;

        /// <summary>초상화 + 큰 숫자 + 칸 체력바 모양을 붙인다(UI-12). 예전 "HP x / y" 글과 막대는 그대로 계산한다.</summary>
        public void ConfigurePortraitLayout(TMP_Text number, Image segment, float perSegment)
        {
            numberLabel = number; segmentTemplate = segment; healthPerSegment = Mathf.Max(1f, perSegment);
            foreach (Image old in segments)
                if (old != null)
                {
                    if (Application.isPlaying) Destroy(old.gameObject);
                    else DestroyImmediate(old.gameObject);
                }
            segments.Clear();
            RefreshValues();
        }

        public void Configure(CharacterActor actor, GamePhaseView view, GameObject panel,
            TMP_Text label, RectTransform barFill)
        {
            player = actor;
            phaseView = view;
            content = panel;
            valueLabel = label;
            fill = barFill;
        }

        void LateUpdate()
        {
            // 초기화 순서 및 같은 씬에서 플레이어 체력 객체가 교체되는 경우를 처리한다.
            Health next = player != null ? player.Health : null;
            if (!ReferenceEquals(health, next))
            {
                Unbind();
                health = next;
                if (health != null) health.Changed += RefreshValues;
                RefreshValues();
            }
            // UI-07(사용자 2026-11-23): 시설·장비 등 창이 열려 있어도 보인다(창이 가리지 않는데 꺼지던 것을 고침).
            // UI-13(사용자 2026-11-24): 건설 모드에서도 다른 준비 HUD와 같이 보인다.
            bool gameplay = phaseView != null && (phaseView.IsExplorationView || phaseView.IsBattleView || phaseView.IsPanelView || phaseView.IsBuildView);
            GamePhase phase = GameSession.Flow.Phase;
            if (content != null) content.SetActive(health != null && gameplay
                && (phase == GamePhase.Preparation || phase == GamePhase.Battle));
        }

        void RefreshValues()
        {
            if (health == null) return;
            if (valueLabel != null)
                valueLabel.text = $"HP {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Maximum)}";
            if (fill != null)
                fill.anchorMax = new Vector2(health.Maximum > 0f
                    ? Mathf.Clamp01(health.Current / health.Maximum) : 0f, 1f);
            if (numberLabel != null)
                numberLabel.text = PrepHudText.HealthNumber(Mathf.CeilToInt(health.Current), Mathf.CeilToInt(health.Maximum));
            ShowSegments();
        }

        // 칸 체력바(UI-12): 최대 체력을 칸으로 나누고, 칸마다 그 칸에 해당하는 만큼 채운다(마지막 칸은 일부만 찰 수 있다).
        void ShowSegments()
        {
            if (segmentTemplate == null || health == null) return;
            int count = PrepHudText.HealthSegments(health.Maximum, healthPerSegment, MaxSegments);
            float per = count > 0 ? health.Maximum / count : 0f;
            while (segments.Count < count)
            {
                Image segment = Instantiate(segmentTemplate, segmentTemplate.transform.parent, false);
                segment.name = "Segment" + (segments.Count + 1);
                // 견본 바로 뒤에 차례로 두어 그 뒤의 테두리·그늘 그림이 칸 위에 그려지게 한다.
                segment.transform.SetSiblingIndex(segmentTemplate.transform.GetSiblingIndex() + 1 + segments.Count);
                segments.Add(segment);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                bool used = i < count;
                if (segments[i].gameObject.activeSelf != used) segments[i].gameObject.SetActive(used);
                if (used) segments[i].fillAmount = per > 0f ? Mathf.Clamp01((health.Current - i * per) / per) : 0f;
            }
        }

        void OnDisable()
        {
            Unbind();
            if (content != null) content.SetActive(false);
        }

        void Unbind()
        {
            if (health != null) health.Changed -= RefreshValues;
            health = null;
        }
    }
}
