using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 글자 줄 메뉴 버튼의 마우스 강조(UI-07 Esc 메뉴). 마우스를 올리면 글자를 강조색으로 바꾸고 왼쪽 선택 표시를 켠다.
    /// 꺼지거나 내려가면 원래대로 돌린다.
    /// </summary>
    public sealed class MenuRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TMP_Text label;
        GameObject marker;
        Color normal;
        Color highlighted;

        public bool IsHighlighted { get; private set; }

        public void Configure(TMP_Text rowLabel, GameObject selectionMarker, Color normalColor, Color highlightedColor)
        {
            label = rowLabel;
            marker = selectionMarker;
            normal = normalColor;
            highlighted = highlightedColor;
            Set(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => Set(true);
        public void OnPointerExit(PointerEventData eventData) => Set(false);
        void OnDisable() => Set(false);

        public void Set(bool on)
        {
            IsHighlighted = on;
            if (label != null) label.color = on ? highlighted : normal;
            if (marker != null) marker.SetActive(on);
        }
    }
}
