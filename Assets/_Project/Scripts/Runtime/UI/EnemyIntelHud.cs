using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Stages;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 적 목록 창(C-5, 왼쪽 부대 목록 아래). 준비 중 1인칭 탐색에서 V로 열고 닫으며 다음 전투의 종류별 수를 보여 준다(숨긴 무리는 "? × 수").
    /// 전투 중에는 늘 보이고 남은 수가 실시간으로 줄며, 숨긴 무리는 전장에 나오면 종류가 드러난다.
    /// 화면을 막지 않으며 적 규칙에는 관여하지 않는다(내용은 GameBattleController.Intel).
    /// </summary>
    public sealed class EnemyIntelHud : MonoBehaviour
    {
        /// <summary>준비 중 미리보기 제목(UI-12 사용자 결정, 예전 "다음 전투 - 적").</summary>
        public const string PreviewTitle = "적 정보";
        public const string BattleTitle = "남은 적";

        [SerializeField] GameBattleController battle;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [Header("Placement (UI-06)")]
        [Tooltip("켜면 준비 미리보기와 전투 중 창 위치를 따로 둔다. 준비 중 왼쪽은 분대 카드가 쓴다.")]
        [SerializeField] bool placeByPhase;
        [SerializeField] Vector2 previewCorner = new Vector2(1f, 1f);
        [SerializeField] Vector2 previewOffset;
        [SerializeField] Vector2 battleCorner = new Vector2(0f, 1f);
        [SerializeField] Vector2 battleOffset;

        [Header("Portrait rows (UI-12)")]
        [Tooltip("초상화 + 이름 + \"× 수\" 한 줄 견본. 숨겨 두고 줄마다 복제한다. 비어 있으면 여러 줄 글(body)만 쓴다.")]
        [SerializeField] EnemyIntelRowView rowTemplate;
        readonly List<EnemyIntelRowView> rowViews = new List<EnemyIntelRowView>();

        EnemyIntel shown;
        int shownVersion = -1;
        GamePhase shownPhase;
        PreparationInteraction listening;

        /// <summary>준비 단계에서 창을 열어 두었는가. 전투가 끝나도 기억한다.</summary>
        public bool PreviewOpen { get; private set; }
        public bool IsShowing => content != null && content.activeSelf;
        public string Title => title != null ? title.text : string.Empty;
        public string Body => body != null ? body.text : string.Empty;
        /// <summary>지금 보이는 초상화 줄 수(UI-12). 줄 견본이 없으면 0.</summary>
        public int RowCount { get { int n = 0; foreach (EnemyIntelRowView v in rowViews) if (v.gameObject.activeSelf) n++; return n; } }
        public EnemyIntelRowView RowView(int index) => rowViews[index];

        /// <summary>초상화 줄 모양을 붙인다(UI-12). null이면 떼고 여러 줄 글로 돌아간다. 여러 줄 글(Body)은 그대로 계산한다.</summary>
        public void ConfigureRows(EnemyIntelRowView template)
        {
            rowTemplate = template;
            foreach (EnemyIntelRowView old in rowViews)
                if (old != null)
                {
                    if (Application.isPlaying) Destroy(old.gameObject);
                    else DestroyImmediate(old.gameObject);
                }
            rowViews.Clear();
            shown = null;
        }

        public void Configure(GameBattleController controller, GamePhaseView view, PreparationInteraction input,
            GameObject panel, TMP_Text heading, TMP_Text lines)
        {
            battle = controller; phaseView = view; content = panel; title = heading; body = lines;
            Unlisten();
            interaction = input;
            if (isActiveAndEnabled) Listen();
        }

        [SerializeField] float previewScale = 1f;

        /// <summary>준비 미리보기와 전투 중 창 자리(UI-06). corner는 화면 모서리(앵커·피벗), offset은 그 모서리에서의 거리.</summary>
        public void ConfigurePlacement(Vector2 previewAt, Vector2 previewShift, Vector2 battleAt, Vector2 battleShift)
        {
            placeByPhase = true;
            previewCorner = previewAt; previewOffset = previewShift;
            battleCorner = battleAt; battleOffset = battleShift;
        }

        /// <summary>준비 미리보기 창 크기 배율(UI-12 준비 HUD 0.8배). 전투 중 창은 늘 1배다.</summary>
        public void ConfigurePreviewScale(float scale) => previewScale = scale > 0f ? scale : 1f;

        void Place(GamePhase phase)
        {
            if (!placeByPhase) return;
            var rect = (RectTransform)content.transform;
            bool fighting = phase == GamePhase.Battle;
            rect.localScale = Vector3.one * (fighting ? 1f : previewScale);
            Vector2 corner = fighting ? battleCorner : previewCorner;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = fighting ? battleOffset : previewOffset;
        }

        /// <summary>준비 단계 창을 열거나 닫는다. V 키와 같다.</summary>
        public void TogglePreview() => PreviewOpen = !PreviewOpen;

        void OnEnable() => Listen();

        void OnDisable()
        {
            Unlisten();
            if (content != null) content.SetActive(false);
        }

        void Listen()
        {
            Unlisten();
            if (interaction == null) return;
            interaction.EnemyPreviewToggled += TogglePreview;
            listening = interaction;
        }

        void Unlisten()
        {
            if (listening != null) listening.EnemyPreviewToggled -= TogglePreview;
            listening = null;
        }

        void LateUpdate()
        {
            if (content == null || body == null) return;
            GamePhase phase = GameSession.Flow.Phase;
            EnemyIntel intel = battle != null ? battle.Intel : null;
            bool show = intel != null && (phase == GamePhase.Battle
                || phase == GamePhase.Preparation && PreviewOpen && phaseView != null
                    && (phaseView.IsExplorationView || phaseView.IsPanelView)); // UI-07: 창이 열려도 보인다(사용자 2026-11-23)
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;
            Place(phase);
            if (intel == shown && intel.Version == shownVersion && phase == shownPhase) return;
            shown = intel; shownVersion = intel.Version; shownPhase = phase;
            if (title != null) title.text = phase == GamePhase.Battle ? $"{BattleTitle} {intel.TotalRemaining}" : PreviewTitle;
            body.text = intel.Describe();
            ShowRows(intel.Rows());
        }

        // 초상화 줄(UI-12): 줄마다 견본을 복제해 쓰고, 남는 줄은 숨긴다. 적이 없으면 여러 줄 글("적 없음")을 대신 보인다.
        void ShowRows(IReadOnlyList<EnemyIntelRow> rows)
        {
            if (rowTemplate == null) return;
            if (body.gameObject.activeSelf != (rows.Count == 0)) body.gameObject.SetActive(rows.Count == 0);
            while (rowViews.Count < rows.Count)
            {
                EnemyIntelRowView view = Instantiate(rowTemplate, rowTemplate.transform.parent, false);
                view.name = "IntelRow" + (rowViews.Count + 1);
                view.transform.SetSiblingIndex(rowTemplate.transform.GetSiblingIndex() + 1 + rowViews.Count);
                rowViews.Add(view);
            }
            for (int i = 0; i < rowViews.Count; i++)
            {
                bool used = i < rows.Count;
                if (rowViews[i].gameObject.activeSelf != used) rowViews[i].gameObject.SetActive(used);
                if (used) rowViews[i].Show(rows[i]);
            }
        }
    }
}
