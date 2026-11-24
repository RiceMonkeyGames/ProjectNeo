using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 시설 팝업(UI-13 C안)의 타일 하나: 남색 썸네일(아이콘이 있으면 그림), 이름, 아래 작은 줄(값·등급), 오른쪽 위 금색 딱지
    /// (잠금·강화 단계), 선택 금테. 타일을 누르면 고르기만 하고 실제 행동은 상세 칸의 단추가 한다.
    /// </summary>
    public sealed class FacilityTileView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text subLabel;
        [SerializeField] GameObject tag;
        [SerializeField] TMP_Text tagLabel;
        [SerializeField] GameObject selection;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.45f;

        public Button Button => button;
        public string NameText => nameLabel != null ? nameLabel.text : string.Empty;
        public string SubText => subLabel != null ? subLabel.text : string.Empty;
        public string TagText => tag != null && tag.activeSelf && tagLabel != null ? tagLabel.text : string.Empty;
        public bool IsSelected => selection != null && selection.activeSelf;

        public void Configure(Button tile, CanvasGroup fade, Image iconImage, TMP_Text label, TMP_Text sub, GameObject tagRoot,
            TMP_Text tagText, GameObject selected)
        {
            button = tile; group = fade; icon = iconImage; nameLabel = label; subLabel = sub;
            tag = tagRoot; tagLabel = tagText; selection = selected;
        }

        /// <summary>흐림만 바꾼다(건설 막대가 매 프레임 골드에 맞춘다).</summary>
        public void SetDim(bool dim)
        {
            if (group != null) group.alpha = dim ? dimAlpha : 1f;
        }

        /// <summary>타일을 채운다. <paramref name="dim"/>이면 흐리게(지금은 할 수 없는 것) 보이지만 눌러서 고를 수는 있다.</summary>
        public void Show(string text, string sub, Sprite picture, string tagText, bool selected, bool dim,
            UnityEngine.Events.UnityAction onClick)
        {
            if (nameLabel != null) nameLabel.text = text;
            if (subLabel != null) subLabel.text = sub ?? string.Empty;
            if (icon != null)
            {
                icon.sprite = picture;
                icon.enabled = picture != null;
            }
            if (tag != null) tag.SetActive(!string.IsNullOrEmpty(tagText));
            if (tagLabel != null) tagLabel.text = tagText ?? string.Empty;
            if (selection != null) selection.SetActive(selected);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(onClick);
                button.interactable = onClick != null;
            }
            if (group != null) group.alpha = dim && !selected ? dimAlpha : 1f;
        }
    }
}
