using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 건설 모드 아래 건설 막대(UI-13 B안, 사용자 2026-11-24). 분류 탭(성벽 / 수성 무기)을 고르면 그 분류의 조각 카드가
    /// 막대에 한 줄로 서고, 한 줄을 넘으면 가로로 스크롤한다. 카드는 시설 창 타일(<see cref="FacilityTileView"/>)을 다시 쓴다:
    /// 이름, 골드 값, 고르면 금테, 골드가 모자라면 흐림. 카드를 누르면 <see cref="ConstructionHud"/>로 조각이나 무기를 고른다.
    /// 해체·전체 수리·흉벽 강화 단추는 막대 오른쪽 끝에 있고, 해체 중에는 해체 단추 판이 금색이 된다.
    /// 작업 탭(PREP-11 B안, 사용자 2026-11-25)은 끝나지 않은 건설·수리 주문을 우선순위 순서로 카드로 보인다(장인이 맡은 주문 먼저,
    /// 대기 주문은 흐림). 그 탭에서는 막대 오른쪽 도구가 앞으로 / 뒤로 / 취소와 장인 고용 단추로 바뀐다.
    /// 화면 부품은 에디터 설정(<c>ConstructionBarLayout</c>)이 만들어 건다. 배치 규칙과 값은 가지고 있지 않다.
    /// </summary>
    public sealed class ConstructionBarHud : MonoBehaviour
    {
        public enum Category { Walls = 0, Weapons = 1, Work = 2 }

        [SerializeField] ConstructionHud hud;
        [SerializeField] GameBuildEconomy economy;
        [SerializeField] Button wallsTab;
        [SerializeField] Button weaponsTab;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform cardRow;
        [SerializeField] FacilityTileView cardTemplate;
        [SerializeField] Button dismantleButton;
        // 전체 수리 단추 글자(수리비를 붙인다)와 상태 띠 오른쪽의 결과 안내(환불·골드 부족 등). 사용자 2026-11-24:
        // 왼쪽 위 안내 판은 통째로 없앴다.
        [SerializeField] TMP_Text repairLabel;
        [SerializeField] TMP_Text noticeLabel;
        [SerializeField] Color dismantleOn = new Color32(0x8A, 0x2A, 0x22, 0xFF);
        [SerializeField] Color tabOn = new Color32(0x6A, 0x52, 0x24, 0xFF);
        [SerializeField] Color tabOff = new Color32(0x24, 0x4A, 0x66, 0xFF);
        [Header("Work tab (PREP-11)")]
        [SerializeField] Button workTab;
        [SerializeField] TMP_Text workTabLabel;
        [Tooltip("성벽·수성 무기 탭에서만 보이는 도구 묶음(해체·전체 수리·흉벽).")]
        [SerializeField] GameObject[] buildTools = new GameObject[0];
        [Tooltip("작업 탭에서만 보이는 도구 묶음(앞으로·뒤로·취소·장인 고용).")]
        [SerializeField] GameObject workTools;
        [SerializeField] Button moveEarlierButton;
        [SerializeField] Button moveLaterButton;
        [SerializeField] Button cancelWorkButton;
        [SerializeField] Button hireButton;
        [SerializeField] TMP_Text hireLabel;

        readonly List<FacilityTileView> cards = new List<FacilityTileView>();
        readonly List<Object> pieces = new List<Object>();
        readonly List<WorkOrder> orders = new List<WorkOrder>();
        readonly List<WorkOrder> pending = new List<WorkOrder>();
        WorkOrder selectedOrder;
        Category shown = Category.Walls;
        Object lastSelected;
        bool built;

        public Category Shown => shown;
        public int CardCount => cards.Count;
        /// <summary>index번째 카드 이름(테스트용).</summary>
        public string CardName(int index) => index >= 0 && index < cards.Count ? cards[index].NameText : string.Empty;
        public bool IsCardSelected(int index) => index >= 0 && index < cards.Count && cards[index].IsSelected;
        /// <summary>index번째 카드 아래 줄(값·진행률, 테스트용).</summary>
        public string CardSub(int index) => index >= 0 && index < cards.Count ? cards[index].SubText : string.Empty;
        /// <summary>index번째 카드 딱지(작업 탭: "작업 중" / "대기", 테스트용).</summary>
        public string CardTag(int index) => index >= 0 && index < cards.Count ? cards[index].TagText : string.Empty;
        public string WorkTabCaption => workTabLabel != null ? workTabLabel.text : string.Empty;
        public string HireCaption => hireLabel != null ? hireLabel.text : string.Empty;
        /// <summary>작업 탭에서 고른 주문. 없으면 null.</summary>
        public WorkOrder SelectedOrder => selectedOrder;
        GameConstructionWork Work => economy != null ? economy.Work : null;

        public void Configure(ConstructionHud construction, GameBuildEconomy buildEconomy, Button walls, Button weapons, ScrollRect row,
            RectTransform content, FacilityTileView template, Button dismantle, TMP_Text repair, TMP_Text notice)
        {
            hud = construction; economy = buildEconomy; wallsTab = walls; weaponsTab = weapons; scroll = row;
            cardRow = content; cardTemplate = template; dismantleButton = dismantle; repairLabel = repair; noticeLabel = notice;
        }

        /// <summary>작업 탭(PREP-11) 부품을 건다. 에디터 설정이 부른다.</summary>
        public void ConfigureWork(Button work, TMP_Text workLabel, GameObject[] buildGroup, GameObject workGroup, Button earlier,
            Button later, Button cancel, Button hire, TMP_Text hireText)
        {
            workTab = work; workTabLabel = workLabel; buildTools = buildGroup; workTools = workGroup;
            moveEarlierButton = earlier; moveLaterButton = later; cancelWorkButton = cancel; hireButton = hire; hireLabel = hireText;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShowing() => showing = 0;

        void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (wallsTab != null) { wallsTab.onClick.RemoveAllListeners(); wallsTab.onClick.AddListener(() => Show(Category.Walls)); }
            if (weaponsTab != null) { weaponsTab.onClick.RemoveAllListeners(); weaponsTab.onClick.AddListener(() => Show(Category.Weapons)); }
            if (workTab != null) { workTab.onClick.RemoveAllListeners(); workTab.onClick.AddListener(() => Show(Category.Work)); }
            Listen(moveEarlierButton, () => MoveSelected(true));
            Listen(moveLaterButton, () => MoveSelected(false));
            Listen(cancelWorkButton, () => CancelSelected());
            Listen(hireButton, () => Hire());
        }

        static int showing;
        /// <summary>건설 막대가 화면에 있는가. 있으면 아래 안내 판은 숨긴다(<see cref="GameFlowHud"/>).</summary>
        public static bool Showing => showing > 0;

        void OnEnable()
        {
            showing++;
            if (!built) Show(shown);
        }

        void OnDisable() => showing = Mathf.Max(0, showing - 1);

        /// <summary>분류 탭을 바꾸고 그 분류의 카드를 다시 만든다.</summary>
        public void Show(Category category)
        {
            shown = category;
            built = true;
            foreach (FacilityTileView card in cards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            cards.Clear();
            pieces.Clear();
            orders.Clear();
            if (hud != null) hud.IdleHint = category == Category.Work ? "작업 카드를 골라 순서를 바꾸거나 취소하세요" : ConstructionHud.PickPieceHint;
            foreach (GameObject tool in buildTools ?? new GameObject[0]) if (tool != null) tool.SetActive(category != Category.Work);
            if (workTools != null) workTools.SetActive(category == Category.Work);
            if (category == Category.Work)
            {
                // 작업 탭에서는 놓을 조각을 고르지 않는다(카드를 누르면 주문만 고른다).
                if (hud != null && hud.Placement != null && (hud.Placement.Selected != null || hud.Placement.SelectedWeapon != null)) hud.Cancel();
                lastSelected = null;
                if (Work != null) Work.PendingOrders(orders);
                if (!orders.Contains(selectedOrder)) selectedOrder = null;
            }
            if (hud == null || cardTemplate == null || cardRow == null) return;
            if (category == Category.Walls)
                foreach (BuildPieceDefinition piece in new[] { hud.Wall, hud.Stairs, hud.Bastion, hud.Tower })
                    if (piece != null) pieces.Add(piece);
            if (category == Category.Weapons)
                foreach (SiegeWeaponDefinition weapon in hud.Weapons)
                    if (weapon != null) pieces.Add(weapon);
            int count = category == Category.Work ? orders.Count : pieces.Count;
            for (int i = 0; i < count; i++)
            {
                FacilityTileView card = Instantiate(cardTemplate, cardRow);
                card.gameObject.SetActive(true);
                cards.Add(card);
            }
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
            Refresh();
        }

        /// <summary>index번째 카드를 누른 것과 같다. 이미 고른 카드를 다시 누르면 고르기를 푼다.</summary>
        public bool Choose(int index)
        {
            if (shown == Category.Work)
            {
                if (index < 0 || index >= orders.Count) return false;
                selectedOrder = selectedOrder == orders[index] ? null : orders[index];
                return true;
            }
            if (index < 0 || index >= pieces.Count) return false;
            Choose(pieces[index]);
            return true;
        }

        void Choose(Object piece)
        {
            PlacementController placement = hud.Placement;
            if (placement == null) return;
            bool again = piece is SiegeWeaponDefinition weapon ? placement.SelectedWeapon == weapon : placement.Selected == (BuildPieceDefinition)piece;
            if (again) { hud.Cancel(); return; }
            if (piece is SiegeWeaponDefinition chosenWeapon) hud.SelectWeapon(chosenWeapon);
            else hud.Select((BuildPieceDefinition)piece);
        }

        /// <summary>고른 주문을 한 칸 앞(earlier)이나 뒤로 옮긴다.</summary>
        public bool MoveSelected(bool earlier)
        {
            bool moved = Work != null && Work.Move(selectedOrder, earlier);
            if (moved) Show(Category.Work);
            return moved;
        }

        /// <summary>고른 주문을 취소한다(건설은 철거·전액 환불, 수리는 남은 몫 환불).</summary>
        public bool CancelSelected()
        {
            bool cancelled = Work != null && Work.CancelOrder(selectedOrder);
            if (cancelled) { selectedOrder = null; Show(Category.Work); }
            return cancelled;
        }

        /// <summary>장인을 한 명 고용한다.</summary>
        public bool Hire()
        {
            bool hired = Work != null && Work.HireArtisan();
            if (hired && shown == Category.Work) Show(Category.Work);
            return hired;
        }

        static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void LateUpdate()
        {
            // 다른 곳(코드·시험)에서 다른 분류의 조각을 새로 고르면 그 분류로 넘어간다. 탭만 바꾼 경우에는 그대로 둔다.
            PlacementController placement = hud != null ? hud.Placement : null;
            Object current = placement == null ? null : placement.SelectedWeapon != null ? placement.SelectedWeapon : (Object)placement.Selected;
            if (current != lastSelected)
            {
                lastSelected = current;
                if (current != null && shown == Category.Work) Show(current is SiegeWeaponDefinition ? Category.Weapons : Category.Walls);
                if (current is SiegeWeaponDefinition && shown != Category.Weapons) Show(Category.Weapons);
                else if (current is BuildPieceDefinition && shown != Category.Walls) Show(Category.Walls);
            }
            // 주문이 늘거나(새로 놓기) 끝나거나 순서가 바뀌면 작업 카드를 다시 만든다.
            if (shown == Category.Work && Work != null)
            {
                Work.PendingOrders(pending);
                if (!SameOrders(pending, orders)) Show(Category.Work);
            }
            Refresh();
        }

        static bool SameOrders(List<WorkOrder> a, List<WorkOrder> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        void RefreshWork()
        {
            GameConstructionWork work = Work;
            for (int i = 0; i < cards.Count && i < orders.Count; i++)
            {
                WorkOrder order = orders[i];
                bool assigned = work != null && work.IsAssigned(order);
                string name = work != null ? work.NameOf(order) : string.Empty;
                string sub = $"{Mathf.FloorToInt(order.Progress * 100f)}%";
                string tag = assigned ? "작업 중" : "대기";
                bool selected = order == selectedOrder;
                FacilityTileView card = cards[i];
                int index = i;
                if (card.NameText != name || card.SubText != sub || card.TagText != tag || card.IsSelected != selected)
                    card.Show(name, sub, null, tag, selected, !assigned, () => Choose(index));
            }
            RunState state = work != null ? work.Run : null;
            if (hireLabel != null)
            {
                string text = HireButtonText(state);
                if (hireLabel.text != text) hireLabel.text = text;
            }
            if (hireButton != null && hireButton.targetGraphic != null)
                hireButton.targetGraphic.color = state != null && state.CheckHireArtisan() == ArtisanHireCheck.Ok ? tabOn : tabOff;
            bool picked = selectedOrder != null;
            if (moveEarlierButton != null) moveEarlierButton.interactable = picked;
            if (moveLaterButton != null) moveLaterButton.interactable = picked;
            if (cancelWorkButton != null) cancelWorkButton.interactable = picked;
        }

        /// <summary>작업 탭 글자: "작업 3/4" = 장인 수 / 지금 대장간 레벨의 장인 상한.</summary>
        public static string WorkTabText(RunState state) =>
            state != null && state.PrepTime != null ? $"작업 {state.ArtisanCount}/{state.ArtisanCap}" : "작업";

        /// <summary>장인 고용 단추 글자. 상한이면 대장간 강화 안내나 최대 표시.</summary>
        public static string HireButtonText(RunState state)
        {
            if (state == null || state.PrepTime == null) return "장인 고용";
            if (state.ArtisanCount < state.ArtisanCap) return $"장인 고용 · {state.NextArtisanHireGold}골드";
            return state.ArtisanCap < state.PrepTime.ArtisanCap(int.MaxValue) ? "장인 가득 · 대장간 강화" : "장인 최대";
        }

        void Refresh()
        {
            PlacementController placement = hud != null ? hud.Placement : null;
            int gold = economy != null && economy.Economy != null ? economy.Economy.Wallet.Gold : int.MaxValue;
            if (shown == Category.Work) RefreshWork();
            for (int i = 0; shown != Category.Work && i < cards.Count && i < pieces.Count; i++)
            {
                string name; int cost; bool selected;
                if (pieces[i] is SiegeWeaponDefinition weapon)
                {
                    name = weapon.DisplayName;
                    cost = economy != null && economy.Table != null ? economy.Table.CostOf(weapon) : 0;
                    selected = placement != null && placement.SelectedWeapon == weapon;
                }
                else
                {
                    var piece = (BuildPieceDefinition)pieces[i];
                    name = piece.DisplayName;
                    cost = economy != null && economy.Table != null ? economy.Table.CostOf(piece) : 0;
                    selected = placement != null && placement.Selected == piece;
                }
                int index = i;
                FacilityTileView card = cards[i];
                if (card.NameText != name || card.IsSelected != selected || card.SubText != CostText(cost))
                    card.Show(name, CostText(cost), null, null, selected, gold < cost, () => Choose(index));
                else card.SetDim(gold < cost && !selected);
            }
            Paint(wallsTab, shown == Category.Walls);
            Paint(weaponsTab, shown == Category.Weapons);
            Paint(workTab, shown == Category.Work);
            if (workTabLabel != null)
            {
                string text = WorkTabText(Work != null ? Work.Run : null);
                if (workTabLabel.text != text) workTabLabel.text = text;
            }
            if (repairLabel != null)
            {
                int repair = economy != null ? economy.FullRepairCost : 0;
                string text = repair > 0 ? $"전체 수리 · {repair}골드" : "전체 수리";
                if (repairLabel.text != text) repairLabel.text = text;
            }
            if (noticeLabel != null)
            {
                string text = economy != null ? economy.Message : string.Empty;
                // 작업 탭에서 안내가 비어 있으면 일하는 주문·대기 주문 수를 보인다.
                if (shown == Category.Work && string.IsNullOrEmpty(text) && Work != null) text = WorkLine(Work);
                if (noticeLabel.text != text) noticeLabel.text = text;
            }
            // 해체 중에는 단추가 붉다(가리킨 조각도 붉게 칠해진다, GameBuildEconomy).
            if (dismantleButton != null && dismantleButton.targetGraphic != null)
                dismantleButton.targetGraphic.color = economy != null && economy.Dismantling ? dismantleOn : tabOff;
        }

        static string CostText(int cost) => $"{cost}골드";

        static string WorkLine(GameConstructionWork work)
        {
            int assigned = 0, waiting = 0;
            foreach (WorkOrder order in work.Queue.Orders)
            {
                if (order.IsComplete) continue;
                if (work.IsAssigned(order)) assigned++; else waiting++;
            }
            return assigned + waiting == 0 ? "진행 중인 작업이 없습니다" : $"작업 중 {assigned} · 대기 {waiting}";
        }

        void Paint(Button tab, bool on)
        {
            if (tab != null && tab.targetGraphic != null) tab.targetGraphic.color = on ? tabOn : tabOff;
        }
    }
}
