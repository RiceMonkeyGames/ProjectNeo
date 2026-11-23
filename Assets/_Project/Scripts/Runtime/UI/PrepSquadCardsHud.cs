using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Morale;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 화면 왼쪽 분대 카드 열(UI-06). 1인칭 탐색과 부대 배치 화면에서 분대마다 카드 한 장(인원·사기 게이지)을 보여 주고,
    /// 그동안 시험용 분대 글 목록(SquadCommandHud)은 숨긴다. 카드는 숨긴 견본을 복제해 분대 수에 맞춘다.
    /// 분대가 한 페이지(HudStyle.SquadCardsPerPage)보다 많으면 페이지로 나눠 [ ] 키로 넘기고(UI-12), 선택 키로 고른 분대가
    /// 다른 페이지에 있으면 그 페이지로 옮긴다. 다른 페이지에 사기가 낮은 분대가 있으면 페이지 표시 옆에 빨간 점을 켠다.
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
        [Tooltip("UI-12 페이지 표시(\"[  1/2  ]\"). 페이지가 둘 이상일 때만 보인다. 비어 있으면 표시 없이 넘기기만 한다.")]
        [SerializeField] TMP_Text pageLabel;
        [Tooltip("UI-12 다른 페이지에 사기가 낮은 분대가 있을 때 켜는 점.")]
        [SerializeField] GameObject pageRisk;

        readonly List<PrepSquadCard> cards = new List<PrepSquadCard>();
        readonly List<bool> selected = new List<bool>(), selectedNow = new List<bool>(), risky = new List<bool>();
        InputAction previousPage, nextPage;
        int page;

        /// <summary>지금 페이지(0부터)와 페이지 수.</summary>
        public int Page => page;
        public int PageCount { get; private set; } = 1;
        public bool PageLabelShowing => pageLabel != null && pageLabel.gameObject.activeInHierarchy;
        public string PageLabelText => pageLabel != null ? pageLabel.text : string.Empty;
        public bool HiddenRiskShowing => pageRisk != null && pageRisk.activeInHierarchy;

        public void ConfigurePages(TMP_Text label, GameObject riskDot) { pageLabel = label; pageRisk = riskDot; }

        /// <summary>다음/이전 페이지로 넘긴다([ ] 키와 같다). 끝에서 처음으로 돈다.</summary>
        public void NextPage() => page = SquadCardPages.Step(page, 1, PageCount);
        public void PreviousPage() => page = SquadCardPages.Step(page, -1, PageCount);

        int PerPage => style != null ? style.SquadCardsPerPage : HudStyle.DefaultSquadCardsPerPage;

        /// <summary>준비 HUD 공용 모양 값. 실행 중에 만드는 창(Esc 메뉴)도 이 값을 쓴다(UI-07).</summary>
        public HudStyle Style => style;
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
            // UI-07(사용자 2026-11-23): 시설·장비 등 창이 열려 있어도 보인다(창이 가리지 않는데 꺼지던 것을 고침).
            bool show = GameSession.Flow.Phase == GamePhase.Preparation && phaseView != null
                && (phaseView.IsExplorationView || phaseView.IsPlacementView || phaseView.IsPanelView)
                && center != null && center.Commander != null && template != null;
            if (content.activeSelf != show) content.SetActive(show);
            if (commandHud != null) commandHud.SetListVisible(!show);
            if (!show) return;
            // 사기 관리자는 실행 중에 붙으므로 씬 설정에 없으면 같은 씬에서 찾는다(SquadCommandHud와 같다).
            if (morale == null)
                foreach (var candidate in FindObjectsByType<GameSquadMorale>())
                    if (candidate.gameObject.scene == gameObject.scene) { morale = candidate; break; }

            int count = center.Commander.SquadCount;
            UpdatePage(count);
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
                bool used = i < count && SquadCardPages.OnPage(i, page, PerPage);
                if (cards[i].gameObject.activeSelf != used) cards[i].gameObject.SetActive(used);
                if (used) ShowCard(cards[i], i);
            }
            if (messageLabel != null) messageLabel.text = Message();
            ShowPageMarks(count);
        }

        // 페이지 넘기기 키, 선택 따라가기, 범위 맞추기.
        void UpdatePage(int count)
        {
            PageCount = SquadCardPages.PageCount(count, PerPage);
            if (previousPage == null && center.Settings != null)
            {
                // 공유 에셋의 액션 상태를 바꾸지 않도록 복제해서 쓴다(SquadCommandInput과 같다).
                previousPage = center.Settings.PreviousPage.Clone();
                nextPage = center.Settings.NextPage.Clone();
                previousPage.Enable(); nextPage.Enable();
            }
            if (PageCount > 1 && previousPage != null)
            {
                if (previousPage.WasPressedThisFrame()) PreviousPage();
                if (nextPage.WasPressedThisFrame()) NextPage();
            }
            selectedNow.Clear();
            for (int i = 0; i < count; i++) selectedNow.Add(center.Commander.IsSelected(i));
            page = SquadCardPages.FollowSelection(page, PerPage, selected, selectedNow);
            selected.Clear();
            selected.AddRange(selectedNow);
            page = SquadCardPages.Clamp(page, PageCount);
        }

        void ShowPageMarks(int count)
        {
            bool paged = PageCount > 1;
            if (pageLabel != null)
            {
                if (pageLabel.gameObject.activeSelf != paged) pageLabel.gameObject.SetActive(paged);
                if (paged) pageLabel.text = SquadCardPages.Label(page, PageCount);
            }
            if (pageRisk == null) return;
            bool risk = false;
            if (paged)
            {
                risky.Clear();
                for (int i = 0; i < count; i++) risky.Add(SquadCardPages.AtRisk(BandOf(i, out _, out _)));
                risk = SquadCardPages.AnyOutside(risky, page, PerPage);
            }
            if (pageRisk.activeSelf != risk) pageRisk.SetActive(risk);
        }

        MoraleBand BandOf(int squad, out float current, out float max)
        {
            SquadMorale value = morale != null ? morale.MoraleOf(squad) : null;
            current = value != null ? value.Value : 0f;
            max = value != null ? value.Max : 0f;
            if (value == null || morale.Settings == null) return MoraleBand.Normal;
            return PrepHudText.Band(current, morale.Settings.BreakingPoint, morale.Settings.HighBandThreshold);
        }

        void ShowCard(PrepSquadCard card, int squad)
        {
            int capacity = squad < center.Squads.Count ? center.Squads[squad].Capacity : 0;
            MoraleBand band = BandOf(squad, out float current, out float max);
            string state = morale != null ? morale.StateOf(squad) : string.Empty;
            card.Show(style, SquadCommandHud.SquadName(center, squad), center.AliveCount(squad), capacity,
                current, max, band, state, center.Commander.IsSelected(squad));
            card.ShowDetails(squad + 1, center.Commander.OrderOf(squad).Kind,
                center.AliveCount(squad), capacity);
        }

        string Message()
        {
            string input = commandInput != null ? commandInput.Message : string.Empty;
            string notice = morale != null ? morale.LastNotice : string.Empty;
            if (string.IsNullOrEmpty(notice)) return input ?? string.Empty;
            return string.IsNullOrEmpty(input) ? notice : input + "\n" + notice;
        }

        void OnEnable()
        {
            previousPage?.Enable();
            nextPage?.Enable();
        }

        void OnDestroy()
        {
            previousPage?.Dispose();
            nextPage?.Dispose();
        }

        void OnDisable()
        {
            previousPage?.Disable();
            nextPage?.Disable();
            if (content != null) content.SetActive(false);
            if (commandHud != null) commandHud.SetListVisible(true);
        }
    }
}
