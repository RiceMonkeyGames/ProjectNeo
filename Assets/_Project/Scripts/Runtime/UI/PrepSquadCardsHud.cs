using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Morale;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 화면 왼쪽 분대 카드 열(UI-06). 1인칭 탐색과 부대 배치 화면에서 분대마다 카드 한 장(인원·사기 게이지)을 보여 주고,
    /// 그동안 시험용 분대 글 목록(SquadCommandHud)은 숨긴다. 카드는 숨긴 견본을 복제해 분대 수에 맞춘다.
    /// 분대·사기 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class PrepSquadCardsHud : MonoBehaviour
    {
        [SerializeField] HudStyle style;
        [SerializeField] SquadCommandCenter center;
        [SerializeField] GameSquadMorale morale;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] SquadCommandHud commandHud;
        [SerializeField] SquadCommandInput commandInput;
        [SerializeField] GameObject content;
        [SerializeField] PrepSquadCard template;
        [Tooltip("카드 아래 알림 줄: 지휘 입력 알림과 마지막 사기 알림.")]
        [SerializeField] TMP_Text messageLabel;

        readonly List<PrepSquadCard> cards = new List<PrepSquadCard>();

        public bool IsShowing => content != null && content.activeSelf;
        public IReadOnlyList<PrepSquadCard> Cards => cards;
        public string MessageText => messageLabel != null ? messageLabel.text : string.Empty;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;

        public void Configure(HudStyle hudStyle, SquadCommandCenter squads, GameSquadMorale squadMorale, GamePhaseView view,
            SquadCommandHud listHud, SquadCommandInput input, GameObject column, PrepSquadCard cardTemplate, TMP_Text message)
        {
            style = hudStyle; center = squads; morale = squadMorale; phaseView = view; commandHud = listHud;
            commandInput = input; content = column; template = cardTemplate; messageLabel = message;
        }

        void LateUpdate()
        {
            if (content == null) return;
            bool show = GameSession.Flow.Phase == GamePhase.Preparation && phaseView != null
                && (phaseView.IsExplorationView || phaseView.IsPlacementView)
                && center != null && center.Commander != null && template != null;
            if (content.activeSelf != show) content.SetActive(show);
            if (commandHud != null) commandHud.SetListVisible(!show);
            if (!show) return;
            // 사기 관리자는 실행 중에 붙으므로 씬 설정에 없으면 같은 씬에서 찾는다(SquadCommandHud와 같다).
            if (morale == null)
                foreach (var candidate in FindObjectsByType<GameSquadMorale>())
                    if (candidate.gameObject.scene == gameObject.scene) { morale = candidate; break; }

            int count = center.Commander.SquadCount;
            while (cards.Count < count)
            {
                PrepSquadCard card = Instantiate(template, template.transform.parent, false);
                card.name = "SquadCard" + (cards.Count + 1);
                // 카드는 견본 바로 뒤에 차례로 두어 알림 줄이 늘 맨 아래에 남게 한다.
                card.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1 + cards.Count);
                cards.Add(card);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < count;
                if (cards[i].gameObject.activeSelf != used) cards[i].gameObject.SetActive(used);
                if (used) ShowCard(cards[i], i);
            }
            if (messageLabel != null) messageLabel.text = Message();
        }

        void ShowCard(PrepSquadCard card, int squad)
        {
            int capacity = squad < center.Squads.Count ? center.Squads[squad].Capacity : 0;
            SquadMorale value = morale != null ? morale.MoraleOf(squad) : null;
            float current = value != null ? value.Value : 0f, max = value != null ? value.Max : 0f;
            MoraleBand band = MoraleBand.Normal;
            if (value != null && morale.Settings != null)
                band = PrepHudText.Band(current, morale.Settings.BreakingPoint, morale.Settings.HighBandThreshold);
            string state = morale != null ? morale.StateOf(squad) : string.Empty;
            card.Show(style, SquadCommandHud.SquadName(center, squad), center.AliveCount(squad), capacity,
                current, max, band, state, center.Commander.IsSelected(squad));
        }

        string Message()
        {
            string input = commandInput != null ? commandInput.Message : string.Empty;
            string notice = morale != null ? morale.LastNotice : string.Empty;
            if (string.IsNullOrEmpty(notice)) return input ?? string.Empty;
            return string.IsNullOrEmpty(input) ? notice : input + "\n" + notice;
        }

        void OnDisable()
        {
            if (content != null) content.SetActive(false);
            if (commandHud != null) commandHud.SetListVisible(true);
        }
    }
}
