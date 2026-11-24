using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 시설 팝업(UI-13 C안) 화면 부품 모음. 에디터 설정(<c>FacilityWindowLayout</c>)이 만들어 이 칸들에 걸고,
    /// <see cref="ProjectNeo.Flow.GameFacilities"/>가 내용과 동작을 채운다. 규칙은 들고 있지 않다.
    /// 위 줄: 시설 이름·레벨과 자금·업그레이드 판 단추. 왼쪽 칸: 제목(또는 매물/보관함 탭)과 타일 격자 두 벌.
    /// 오른쪽 칸: 고른 타일의 이름·값·썸네일(옆에 능력치 아이콘)·설명, 안내 한 줄.
    /// 아래 줄: 왼쪽 리롤(상점), 오른쪽에 둘째 행동·행동·닫기(사용자 2026-11-24: 행동 단추는 닫기 옆).
    /// </summary>
    public sealed class FacilityWindowView : MonoBehaviour
    {
        [Header("Title row")]
        public TMP_Text title;
        public TMP_Text status;
        public Button upgradeButton;
        public TMP_Text upgradeLabel;

        [Header("Left column")]
        public TMP_Text header;
        public TMP_Text count;
        public Button offersTab;
        public TMP_Text offersTabLabel;
        public Button storageTab;
        public TMP_Text storageTabLabel;
        public GameObject offerScroll;
        public RectTransform offerGrid;
        public GameObject storageScroll;
        public RectTransform storageGrid;
        public TMP_Text emptyLabel;
        public FacilityTileView tileTemplate;

        [Header("Detail column")]
        public TMP_Text detailTitle;
        public TMP_Text detailPrice;
        public GameObject detailThumbnail;
        public Image detailIcon;
        /// <summary>썸네일 오른쪽의 체력·공격력·방어력 아이콘 줄(영웅·병사). 값은 같은 순서다.</summary>
        public GameObject statsRoot;
        public TMP_Text[] statValues;
        public TMP_Text detailBody;
        public TMP_Text notice;
        public Button primaryButton;
        public TMP_Text primaryLabel;
        public Button secondaryButton;
        public TMP_Text secondaryLabel;

        [Header("Bottom row")]
        public Button rerollButton;
        public TMP_Text rerollLabel;
        public Button closeButton;

        [Header("Tabs")]
        public Color tabOn = new Color32(0x6A, 0x52, 0x24, 0xFF);
        public Color tabOff = new Color32(0x24, 0x4A, 0x66, 0xFF);

        /// <summary>탭 판 색을 고른 쪽에 맞춘다.</summary>
        public void MarkTab(bool storage)
        {
            Paint(offersTab, storage ? tabOff : tabOn);
            Paint(storageTab, storage ? tabOn : tabOff);
        }

        static void Paint(Button tab, Color color)
        {
            if (tab != null && tab.targetGraphic != null) tab.targetGraphic.color = color;
        }
    }
}
