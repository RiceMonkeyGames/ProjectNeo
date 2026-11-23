using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 부대 편성 창(UI-07)의 줄 하나: 썸네일 칸, 이름, 오른쪽 수, 부대장 표시, 선택 테두리.
    /// 줄을 누르면 편성 창이 정한 동작(편입·해제·선택)을 한다. 눌 수 없는 줄은 흐리게 보인다.
    /// 부대장 줄도 다른 줄과 크기가 같고 금색 썸네일 테두리와 표시로만 구분한다(사용자 2026-11-23).
    /// 썸네일은 얼굴 그림이 생기기 전까지 빈 칸이다.
    /// </summary>
    public sealed class OrganizationRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform thumbnail;
        [SerializeField] Image thumbnailFrame;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] GameObject leaderTag;
        [SerializeField] GameObject selection;
        [SerializeField] Color thumbnailFrameColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
        [SerializeField] Color leaderFrameColor = new Color32(0xE3, 0xBB, 0x71, 0xFF);
        [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.35f;

        public Button Button => button;
        public string NameText => nameLabel != null ? nameLabel.text : string.Empty;
        public bool IsClickable => button != null && button.interactable;
        public bool IsSelected => selection != null && selection.activeSelf;
        public bool IsLeader => leaderTag != null && leaderTag.activeSelf;

        public void Configure(Button row, CanvasGroup fade, RectTransform thumb, Image thumbFrame,
            TMP_Text label, TMP_Text count, GameObject tag, GameObject selected)
        {
            button = row; group = fade; thumbnail = thumb; thumbnailFrame = thumbFrame;
            nameLabel = label; countLabel = count; leaderTag = tag; selection = selected;
        }

        /// <summary>
        /// 줄을 채운다. <paramref name="hasThumbnail"/>이 false면 썸네일 칸을 감춘다(부대 목록).
        /// <paramref name="onClick"/>이 null이거나 <paramref name="clickable"/>이 false면 누를 수 없고,
        /// <paramref name="dimWhenIdle"/>이면 흐리게 보인다.
        /// </summary>
        public void Show(string text, string count, bool hasThumbnail, bool leader, bool selected,
            UnityEngine.Events.UnityAction onClick, bool clickable = true, bool dimWhenIdle = true)
        {
            if (nameLabel != null) nameLabel.text = text;
            if (countLabel != null)
            {
                countLabel.text = count ?? string.Empty;
                countLabel.gameObject.SetActive(!string.IsNullOrEmpty(count));
            }
            if (thumbnail != null) thumbnail.gameObject.SetActive(hasThumbnail);
            if (thumbnailFrame != null) thumbnailFrame.color = leader ? leaderFrameColor : thumbnailFrameColor;
            if (leaderTag != null) leaderTag.SetActive(leader);
            if (selection != null) selection.SetActive(selected);
            bool canClick = onClick != null && clickable;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(onClick);
                button.interactable = canClick;
            }
            if (group != null) group.alpha = canClick || !dimWhenIdle ? 1f : disabledAlpha;
        }
    }
}
