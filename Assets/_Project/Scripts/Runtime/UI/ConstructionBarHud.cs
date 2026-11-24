using System.Collections.Generic;
using ProjectNeo.Flow;
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
    /// 화면 부품은 에디터 설정(<c>ConstructionBarLayout</c>)이 만들어 건다. 배치 규칙과 값은 가지고 있지 않다.
    /// </summary>
    public sealed class ConstructionBarHud : MonoBehaviour
    {
        public enum Category { Walls = 0, Weapons = 1 }

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

        readonly List<FacilityTileView> cards = new List<FacilityTileView>();
        readonly List<Object> pieces = new List<Object>();
        Category shown = Category.Walls;
        Object lastSelected;
        bool built;

        public Category Shown => shown;
        public int CardCount => cards.Count;
        /// <summary>index번째 카드 이름(테스트용).</summary>
        public string CardName(int index) => index >= 0 && index < cards.Count ? cards[index].NameText : string.Empty;
        public bool IsCardSelected(int index) => index >= 0 && index < cards.Count && cards[index].IsSelected;

        public void Configure(ConstructionHud construction, GameBuildEconomy buildEconomy, Button walls, Button weapons, ScrollRect row,
            RectTransform content, FacilityTileView template, Button dismantle, TMP_Text repair, TMP_Text notice)
        {
            hud = construction; economy = buildEconomy; wallsTab = walls; weaponsTab = weapons; scroll = row;
            cardRow = content; cardTemplate = template; dismantleButton = dismantle; repairLabel = repair; noticeLabel = notice;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShowing() => showing = 0;

        void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (wallsTab != null) { wallsTab.onClick.RemoveAllListeners(); wallsTab.onClick.AddListener(() => Show(Category.Walls)); }
            if (weaponsTab != null) { weaponsTab.onClick.RemoveAllListeners(); weaponsTab.onClick.AddListener(() => Show(Category.Weapons)); }
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
            if (hud == null || cardTemplate == null || cardRow == null) return;
            if (category == Category.Walls)
                foreach (BuildPieceDefinition piece in new[] { hud.Wall, hud.Stairs, hud.Bastion, hud.Tower })
                    if (piece != null) pieces.Add(piece);
            if (category == Category.Weapons)
                foreach (SiegeWeaponDefinition weapon in hud.Weapons)
                    if (weapon != null) pieces.Add(weapon);
            for (int i = 0; i < pieces.Count; i++)
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

        void LateUpdate()
        {
            // 다른 곳(코드·시험)에서 다른 분류의 조각을 새로 고르면 그 분류로 넘어간다. 탭만 바꾼 경우에는 그대로 둔다.
            PlacementController placement = hud != null ? hud.Placement : null;
            Object current = placement == null ? null : placement.SelectedWeapon != null ? placement.SelectedWeapon : (Object)placement.Selected;
            if (current != lastSelected)
            {
                lastSelected = current;
                if (current is SiegeWeaponDefinition && shown != Category.Weapons) Show(Category.Weapons);
                else if (current is BuildPieceDefinition && shown != Category.Walls) Show(Category.Walls);
            }
            Refresh();
        }

        void Refresh()
        {
            PlacementController placement = hud != null ? hud.Placement : null;
            int gold = economy != null && economy.Economy != null ? economy.Economy.Wallet.Gold : int.MaxValue;
            for (int i = 0; i < cards.Count && i < pieces.Count; i++)
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
            if (repairLabel != null)
            {
                int repair = economy != null ? economy.FullRepairCost : 0;
                string text = repair > 0 ? $"전체 수리 · {repair}골드" : "전체 수리";
                if (repairLabel.text != text) repairLabel.text = text;
            }
            if (noticeLabel != null)
            {
                string text = economy != null ? economy.Message : string.Empty;
                if (noticeLabel.text != text) noticeLabel.text = text;
            }
            // 해체 중에는 단추가 붉다(가리킨 조각도 붉게 칠해진다, GameBuildEconomy).
            if (dismantleButton != null && dismantleButton.targetGraphic != null)
                dismantleButton.targetGraphic.color = economy != null && economy.Dismantling ? dismantleOn : tabOff;
        }

        static string CostText(int cost) => $"{cost}골드";

        void Paint(Button tab, bool on)
        {
            if (tab != null && tab.targetGraphic != null) tab.targetGraphic.color = on ? tabOn : tabOff;
        }
    }
}
