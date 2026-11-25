using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 시간 시계 표시(PREP-06). 화면 맨 위 가운데(단계 표시 상자 바로 위) 한 줄로, 준비 중에는 1인칭·건설·시설 창·배치 화면
    /// 어디서나 보인다. 시설 창보다 위에 그려진다. 시간 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class PrepClockHud : MonoBehaviour
    {
        [SerializeField] GameBattleController battle;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text label;
        [Tooltip("열려 있는 동안 시계를 숨기는 화면. 거의 화면 전체를 덮어 시계 줄이 제목에 닿는 창(부대 편성)만 넣는다.")]
        [SerializeField] GameObject[] hideWhileOpen = new GameObject[0];
        [Tooltip("남은 준비 시간 게이지의 채움(UI-06). 비어 있으면 글만 보여 준다.")]
        [SerializeField] RectTransform fill;
        [Header("Hourglass layout (UI-12)")]
        [Tooltip("모래시계 옆 위의 숫자(\"5.5/8\", 전체 시간은 흐리게). 비어 있으면 쓰지 않는다.")]
        [SerializeField] TMP_Text numberLabel;
        [Tooltip("칸 게이지의 한 칸 견본(Filled Horizontal). 숨겨 두고 복제한다. 비어 있으면 칸을 쓰지 않는다.")]
        [SerializeField] Image segmentTemplate;
        [Tooltip("한 칸이 나타내는 준비 시간(시간). 칸 수는 예산에 맞춰 늘되 MaxSegments를 넘지 않는다.")]
        [SerializeField, Min(0.1f)] float hoursPerSegment = 1f;
        [Tooltip("시계 줄 오른쪽의 장인 수(\"장인 3/4\", PREP-11). 비어 있으면 쓰지 않는다.")]
        [SerializeField] TMP_Text artisanLabel;
        const int MaxSegments = 16;
        readonly List<Image> segments = new List<Image>();

        public string Text => label != null ? label.text : string.Empty;
        /// <summary>남은 시간 게이지 비율(0~1). 게이지가 없으면 0.</summary>
        public float FillRatio => fill != null ? fill.anchorMax.x : 0f;
        public string NumberText => numberLabel != null ? numberLabel.text : string.Empty;
        /// <summary>지금 보이는 시간 칸 수.</summary>
        public int SegmentCount { get { int n = 0; foreach (Image s in segments) if (s.gameObject.activeSelf) n++; return n; } }
        /// <summary>칸 하나의 채움(0~1). 없는 칸이면 0.</summary>
        public float SegmentFill(int index) => index >= 0 && index < segments.Count && segments[index].gameObject.activeSelf ? segments[index].fillAmount : 0f;

        /// <summary>모래시계 + 숫자 + 칸 게이지 모양을 붙인다(UI-12). 예전 한 줄 글과 게이지는 그대로 계산한다.</summary>
        public void ConfigureHourglassLayout(TMP_Text number, Image segment, float perSegment)
        {
            numberLabel = number; segmentTemplate = segment; hoursPerSegment = Mathf.Max(0.1f, perSegment);
            foreach (Image old in segments)
                if (old != null)
                {
                    if (Application.isPlaying) Destroy(old.gameObject);
                    else DestroyImmediate(old.gameObject);
                }
            segments.Clear();
        }

        public void ConfigureGauge(RectTransform gaugeFill) => fill = gaugeFill;
        /// <summary>장인 수 글자를 붙인다(PREP-11).</summary>
        public void ConfigureArtisans(TMP_Text label) => artisanLabel = label;
        public string ArtisanText => artisanLabel != null ? artisanLabel.text : string.Empty;

        /// <summary>장인 수 한 줄: "장인 3/4"(장인 수 / 지금 대장간 레벨의 상한).</summary>
        public static string DescribeArtisans(RunState state) => $"장인 {state.ArtisanCount}/{state.ArtisanCap}";
        public bool IsShowing => content != null && content.activeSelf;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;

        public void Configure(GameBattleController controller, GameObject line, TMP_Text text, params GameObject[] hidden)
        {
            battle = controller;
            content = line;
            label = text;
            hideWhileOpen = hidden ?? new GameObject[0];
        }

        bool Covered()
        {
            foreach (GameObject window in hideWhileOpen)
                if (window != null && window.activeInHierarchy) return true;
            return false;
        }

        void LateUpdate()
        {
            if (content == null || label == null) return;
            RunState state = battle != null ? battle.RunLink.State : null;
            bool show = state != null && state.PrepTime != null && GameSession.Flow.Phase == GamePhase.Preparation
                && state.ClockPreparation == state.BattlesFought && !Covered();
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;
            label.text = Describe(state.Clock);
            if (fill != null) fill.anchorMax = new Vector2(PrepHudText.Ratio(state.Clock.Remaining, state.Clock.Budget), 1f);
            if (numberLabel != null) numberLabel.text = PrepHudText.ClockNumber(state.Clock.Remaining, state.Clock.Budget);
            ShowSegments(state.Clock.Remaining, state.Clock.Budget);
            if (artisanLabel != null)
            {
                string artisans = DescribeArtisans(state);
                if (artisanLabel.text != artisans) artisanLabel.text = artisans;
            }
        }

        // 칸 게이지(UI-12): 예산을 칸으로 나누고, 칸마다 그 칸에 해당하는 만큼 채운다(마지막 남은 칸은 일부만 찰 수 있다).
        void ShowSegments(float remaining, float budget)
        {
            if (segmentTemplate == null) return;
            int count = PrepHudText.HealthSegments(budget, hoursPerSegment, MaxSegments);
            float per = count > 0 ? budget / count : 0f;
            while (segments.Count < count)
            {
                Image segment = Instantiate(segmentTemplate, segmentTemplate.transform.parent, false);
                segment.name = "Segment" + (segments.Count + 1);
                segment.transform.SetSiblingIndex(segmentTemplate.transform.GetSiblingIndex() + 1 + segments.Count);
                segments.Add(segment);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                bool used = i < count;
                if (segments[i].gameObject.activeSelf != used) segments[i].gameObject.SetActive(used);
                if (used) segments[i].fillAmount = per > 0f ? Mathf.Clamp01((remaining - i * per) / per) : 0f;
            }
        }

        /// <summary>시계 한 줄. 예: "Prep time 7h left / 12h". 다 쓰면 끝에 안내를 붙인다.</summary>
        public static string Describe(PrepClock clock)
        {
            string text = $"준비 시간 {clock.Remaining:0.#}시간 남음 / {clock.Budget:0.#}시간";
            return clock.IsExhausted ? text + " - 시간 없음" : text;
        }

        void OnDisable()
        {
            if (content != null) content.SetActive(false);
        }
    }
}
