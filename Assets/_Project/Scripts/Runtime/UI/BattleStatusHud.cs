using System.Collections.Generic;
using ProjectNeo.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 전투 상황판(UI-13 전투 HUD A안, 사용자 2026-11-24). 준비 HUD 사이드바와 같은 자리·모양으로 전투 중에만 보인다.
    /// 왼쪽 위 머리 판은 준비 머리 판(스테이지 제목·승천)과 같고 아래 줄이 준비 시간 대신 웨이브(나온 수/전체, 다음 웨이브까지 초,
    /// 웨이브마다 금색 칸)다. 오른쪽 위 칩은 골드 칩 자리에 아군 수와 성문 체력을 보여 주고, 그 아래에는 적 정보(남은 적)가 온다.
    /// 화면은 씬을 바꾸지 않게 실행 중에 코드로 만든다(<see cref="BossHealthHud"/>와 같은 방식). 전투 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class BattleStatusHud : MonoBehaviour
    {
        // 준비 HUD 사이드바(PrepHudSidebarLayout)와 같은 1280x720 값: 가장자리 12, 판 폭 260, 머리 판 112.5, 칩 46, 전체 0.8배.
        const float Edge = 12f, Width = 260f, HeaderHeight = 112.5f, ChipHeight = 46f, HudScale = 0.8f;
        const float Inner = 12f, InnerWidth = Width - 2f * Inner, TitleRow = 39.5f;
        const float WaveTop = TitleRow + 18f, WaveHeight = 42f, SegmentHeight = 12f;
        const float GaugeLeft = 166f, GaugeWidth = 80f;
        static readonly Color PendingWave = new Color32(0x2A, 0x2E, 0x33, 0xFF);

        GameBattleController battle;
        HudStyle style;
        GameObject root;
        TMP_Text title, ascension, waveNumber, waveState, allies, gateLabel;
        RectTransform segmentRow, gateFill;
        readonly List<Image> segments = new List<Image>();

        static int showing;
        /// <summary>전투 상황판이 보이는가. 보이는 동안 예전 전투 상태 글 상자와 단계 상자는 숨는다.</summary>
        public static bool Showing => showing > 0;
        bool counted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShowing() => showing = 0;

        public bool Visible => root != null && root.activeSelf;
        public string TitleText => title != null ? title.text : string.Empty;
        public string WaveText => waveNumber != null ? waveNumber.text : string.Empty;
        public string WaveStateText => waveState != null ? waveState.text : string.Empty;
        public string AlliesText => allies != null ? allies.text : string.Empty;
        public string GateText => gateLabel != null ? gateLabel.text : string.Empty;
        /// <summary>성문 게이지 채움 0~1.</summary>
        public float GateRatio => gateFill != null ? gateFill.anchorMax.x : 0f;
        /// <summary>금색으로 찬 웨이브 칸 수.</summary>
        public int FilledWaves { get { int n = 0; foreach (Image s in segments) if (s.gameObject.activeSelf && s.color == HudPlate.Gold) n++; return n; } }
        /// <summary>머리 판과 칩(겹침 시험용).</summary>
        public RectTransform HeaderArea { get; private set; }
        public RectTransform ChipArea { get; private set; }

        public void Configure(GameBattleController controller, HudStyle hudStyle)
        {
            battle = controller; style = hudStyle;
            if (root == null) Build();
        }

        void LateUpdate()
        {
            bool show = battle != null && GameSession.Flow.Phase == GamePhase.Battle && GameSession.Flow.Run != null;
            SetShowing(show);
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;
            GameFlow flow = GameSession.Flow;
            title.text = PrepHudText.StageTitle(flow.Run.StageNumber);
            // 준비 머리 판(PrepTopBarHud)과 같이 승천 0도 늘 보여 준다.
            ascension.text = PrepHudText.AscensionTag(flow.Run.AscensionLevel);
            waveNumber.text = BattleHudText.WaveNumber(battle.WavesReleased, battle.WaveCount);
            waveState.text = BattleHudText.WaveState(battle.SecondsUntilNextWave, battle.IsRetreating);
            waveState.color = battle.IsRetreating ? (style != null ? style.Warning : HudPlate.HealthRed) : HudPlate.Cream;
            ShowSegments(Mathf.Max(1, battle.WaveCount), Mathf.Max(1, battle.WavesReleased));
            allies.text = BattleHudText.Allies(battle.AlliesAlive);
            battle.GateHealth(out float current, out float maximum);
            bool destroyed = battle.GateDestroyed;
            gateLabel.text = BattleHudText.Gate(destroyed);
            gateLabel.color = destroyed ? (style != null ? style.Warning : HudPlate.HealthRed) : HudPlate.Cream;
            float ratio = destroyed ? 0f : PrepHudText.Ratio(current, maximum);
            gateFill.anchorMax = new Vector2(ratio, 1f);
        }

        void SetShowing(bool show)
        {
            if (show == counted) return;
            counted = show;
            showing += show ? 1 : -1;
        }

        void OnDisable()
        {
            SetShowing(false);
            if (root != null) root.SetActive(false);
        }

        void ShowSegments(int count, int released)
        {
            while (segments.Count < count)
            {
                Image segment = HudPlate.Box(segmentRow, "Wave" + (segments.Count + 1), PendingWave);
                if (style != null && style.WindowPlateSprite != null)
                {
                    segment.sprite = style.WindowPlateSprite;
                    segment.type = Image.Type.Sliced;
                    segment.pixelsPerUnitMultiplier = 4f;
                }
                segments.Add(segment);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                bool used = i < count;
                if (segments[i].gameObject.activeSelf != used) segments[i].gameObject.SetActive(used);
                if (used) segments[i].color = i < released ? HudPlate.Gold : PendingWave;
            }
        }

        void Build()
        {
            RectTransform canvas = HudPlate.OverlayCanvas(transform, "BattleStatusCanvas", 20);
            root = canvas.gameObject;
            BuildHeader(canvas);
            BuildChip(canvas);
            root.SetActive(false);
        }

        // 머리 판: 준비 머리 판과 같은 자리. 제목(왼쪽)·승천(오른쪽), 금색 줄, 웨이브 줄.
        void BuildHeader(RectTransform canvas)
        {
            RectTransform header = HudPlate.Node(canvas, "BattleHeader");
            HudPlate.Place(header, new Vector2(0f, 1f), new Vector2(Edge, -Edge) * HudScale, new Vector2(Width, HeaderHeight), new Vector2(0f, 1f));
            header.localScale = Vector3.one * HudScale;
            HeaderArea = header;
            HudPlate.Plate(header, style, HudPlate.PlateColor);

            title = HudPlate.Label(header, "StageTitle", style, 21f, Color.white, TextAlignmentOptions.MidlineLeft);
            HudPlate.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Inner - 1f, -8.5f), new Vector2(InnerWidth, 32f), new Vector2(0f, 1f));
            ascension = HudPlate.Label(header, "Ascension", style, 16f, HudPlate.Cream, TextAlignmentOptions.MidlineRight);
            HudPlate.Place(ascension.rectTransform, new Vector2(0f, 1f), new Vector2(Inner - 1f, -8.5f), new Vector2(InnerWidth + 1f, 32f), new Vector2(0f, 1f));
            HudPlate.Divider(header, Inner, TitleRow + 8f, Width);

            RectTransform row = HudPlate.Node(header, "WaveRow");
            HudPlate.Place(row, new Vector2(0f, 1f), new Vector2(Inner, -WaveTop), new Vector2(InnerWidth, WaveHeight), new Vector2(0f, 1f));
            waveNumber = HudPlate.Label(row, "Number", style, 20f, Color.white, TextAlignmentOptions.BottomLeft);
            HudPlate.Place(waveNumber.rectTransform, Vector2.zero, new Vector2(0f, SegmentHeight + 4f), new Vector2(InnerWidth, 26f), Vector2.zero);
            waveState = HudPlate.Label(row, "State", style, 15f, HudPlate.Cream, TextAlignmentOptions.BottomRight);
            HudPlate.Place(waveState.rectTransform, Vector2.zero, new Vector2(0f, SegmentHeight + 5f), new Vector2(InnerWidth, 24f), Vector2.zero);

            segmentRow = HudPlate.Node(row, "Segments");
            HudPlate.Place(segmentRow, Vector2.zero, new Vector2(0f, 2f), new Vector2(InnerWidth, SegmentHeight), Vector2.zero);
            Image track = HudPlate.Box(segmentRow, "Track", HudPlate.TrackColor);
            HudPlate.Stretch(track.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            track.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = segmentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        }

        // 칩: 준비 골드 칩 자리. 아군 수(왼쪽)와 성문 체력 게이지(오른쪽).
        void BuildChip(RectTransform canvas)
        {
            RectTransform chip = HudPlate.Node(canvas, "BattleChip");
            HudPlate.Place(chip, Vector2.one, new Vector2(-Edge, -Edge) * HudScale, new Vector2(Width, ChipHeight), Vector2.one);
            chip.localScale = Vector3.one * HudScale;
            ChipArea = chip;
            HudPlate.Plate(chip, style, HudPlate.PlateColor);

            allies = HudPlate.Label(chip, "Allies", style, 19f, Color.white, TextAlignmentOptions.MidlineLeft);
            HudPlate.Place(allies.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(100f, 30f), new Vector2(0f, 0.5f));
            gateLabel = HudPlate.Label(chip, "Gate", style, 16f, HudPlate.Cream, TextAlignmentOptions.MidlineRight);
            HudPlate.Place(gateLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(GaugeLeft - 8f, 0f), new Vector2(80f, 30f), new Vector2(1f, 0.5f));

            RectTransform gauge = HudPlate.Node(chip, "GateGauge");
            HudPlate.Place(gauge, new Vector2(0f, 0.5f), new Vector2(GaugeLeft, 0f), new Vector2(GaugeWidth, SegmentHeight), new Vector2(0f, 0.5f));
            Image track = HudPlate.Box(gauge, "Track", HudPlate.TrackColor);
            HudPlate.Stretch(track.rectTransform, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            Image fill = HudPlate.Box(gauge, "Fill", HudPlate.HealthRed);
            gateFill = fill.rectTransform;
            gateFill.anchorMin = Vector2.zero; gateFill.anchorMax = Vector2.one;
            gateFill.offsetMin = gateFill.offsetMax = Vector2.zero;
        }
    }
}
