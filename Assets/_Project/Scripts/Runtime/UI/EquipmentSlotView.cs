using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 장비 화면(UI-07 B안)의 정사각형 칸 하나: 장비 칸 또는 보관함 격자 칸. 아이콘(없으면 이름 앞 글자)을 그리고,
    /// 클릭과 마우스 올림/내림을 알린다. 빈 칸은 점선 테두리만 보이고 누를 수 없다.
    /// </summary>
    public sealed class EquipmentSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text letters;
        [SerializeField] TMP_Text level;
        [SerializeField] GameObject filledFrame;
        [SerializeField] GameObject emptyFrame;
        [SerializeField] GameObject highlight;
        [SerializeField] Image placeholder;

        Action hovered, left;

        public void Configure(Button clickTarget, Image iconImage, TMP_Text lettersLabel, TMP_Text levelLabel,
            GameObject filled, GameObject empty, GameObject hoverHighlight)
        {
            button = clickTarget; icon = iconImage; letters = lettersLabel; level = levelLabel;
            filledFrame = filled; emptyFrame = empty; highlight = hoverHighlight;
        }

        /// <summary>빈 칸에 흐리게 띄우는 종류 그림 자리를 연결한다.</summary>
        public void ConfigurePlaceholder(Image image) => placeholder = image;

        /// <summary>칸에 든 것의 이름(테스트용). 빈 칸이면 빈 문자열.</summary>
        public string ItemName { get; private set; } = string.Empty;
        public bool IsEmpty => ItemName.Length == 0;

        /// <summary>아이템을 보여준다. <paramref name="click"/>은 누를 때, <paramref name="enter"/>·<paramref name="exit"/>는 마우스 올림/내림.</summary>
        public void Show(string name, Sprite sprite, int upgradeLevel, Action click, Action enter, Action exit)
        {
            ItemName = name ?? string.Empty;
            hovered = enter; left = exit;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (letters != null) letters.text = sprite == null ? Abbreviation(ItemName) : string.Empty;
            if (level != null) level.text = upgradeLevel > 0 ? $"+{upgradeLevel}" : string.Empty;
            SetFrames(true);
            if (placeholder != null) placeholder.enabled = false;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = click != null;
                if (click != null) button.onClick.AddListener(() => click());
            }
            SetHighlight(false);
        }

        /// <summary>빈 칸으로 둔다. <paramref name="kind"/>가 있으면 그 종류 그림을 흐리게 띄운다.</summary>
        public void ShowEmpty(Sprite kind)
        {
            if (placeholder != null) { placeholder.sprite = kind; placeholder.enabled = kind != null; }
            ItemName = string.Empty;
            hovered = left = null;
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (letters != null) letters.text = string.Empty;
            if (level != null) level.text = string.Empty;
            SetFrames(false);
            if (button != null) { button.onClick.RemoveAllListeners(); button.interactable = false; }
            SetHighlight(false);
        }

        void SetFrames(bool filled)
        {
            if (filledFrame != null) filledFrame.SetActive(filled);
            if (emptyFrame != null) emptyFrame.SetActive(!filled);
        }

        void SetHighlight(bool on) { if (highlight != null) highlight.SetActive(on); }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsEmpty) return;
            SetHighlight(true);
            hovered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
            left?.Invoke();
        }

        /// <summary>아이콘이 없을 때 보이는 이름 앞 두 글자(공백 제외).</summary>
        internal static string Abbreviation(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var text = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsWhiteSpace(c) || c == '+') continue;
                text.Append(c);
                if (text.Length == 2) break;
            }
            return text.ToString();
        }
    }
}
