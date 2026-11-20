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
        public const string PreviewTitle = "다음 전투 - 적";
        public const string BattleTitle = "남은 적";

        [SerializeField] GameBattleController battle;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;

        EnemyIntel shown;
        int shownVersion = -1;
        GamePhase shownPhase;
        PreparationInteraction listening;

        /// <summary>준비 단계에서 창을 열어 두었는가. 전투가 끝나도 기억한다.</summary>
        public bool PreviewOpen { get; private set; }
        public bool IsShowing => content != null && content.activeSelf;
        public string Title => title != null ? title.text : string.Empty;
        public string Body => body != null ? body.text : string.Empty;

        public void Configure(GameBattleController controller, GamePhaseView view, PreparationInteraction input,
            GameObject panel, TMP_Text heading, TMP_Text lines)
        {
            battle = controller; phaseView = view; content = panel; title = heading; body = lines;
            Unlisten();
            interaction = input;
            if (isActiveAndEnabled) Listen();
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
                || phase == GamePhase.Preparation && PreviewOpen && phaseView != null && phaseView.IsExplorationView);
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;
            if (intel == shown && intel.Version == shownVersion && phase == shownPhase) return;
            shown = intel; shownVersion = intel.Version; shownPhase = phase;
            if (title != null) title.text = phase == GamePhase.Battle ? $"{BattleTitle} {intel.TotalRemaining}" : PreviewTitle;
            body.text = intel.Describe();
        }
    }
}
