using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>준비 HUD 분대 카드 한 장(UI-06): 이름, 인원 게이지, 사기 게이지. 값만 받아 그린다.</summary>
    public sealed class PrepSquadCard : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image frame;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text headcount;
        [SerializeField] RectTransform headcountFill;
        [SerializeField] TMP_Text morale;
        [SerializeField] RectTransform moraleFill;
        [SerializeField] Image moraleFillImage;

        public string TitleText => title != null ? title.text : string.Empty;
        public string HeadcountText => headcount != null ? headcount.text : string.Empty;
        public string MoraleText => morale != null ? morale.text : string.Empty;
        public float HeadcountRatio => headcountFill != null ? headcountFill.anchorMax.x : 0f;
        public float MoraleRatio => moraleFill != null ? moraleFill.anchorMax.x : 0f;
        [Tooltip("선택된 동안만 켜는 외곽선(UI-06 A안). 비어 있으면 바탕·테두리 색으로만 구분한다.")]
        [SerializeField] GameObject selectionOutline;

        public bool Selected { get; private set; }
        public bool OutlineShowing => selectionOutline != null && selectionOutline.activeSelf;

        /// <summary>테두리 그림(선택 시 색이 바뀐다). 스프라이트 없는 단색 모양이면 비워 둔다.</summary>
        public void ConfigureFrame(Image border) => frame = border;

        /// <summary>선택된 동안만 켜는 외곽선을 붙인다.</summary>
        public void ConfigureSelectionOutline(GameObject outline) => selectionOutline = outline;

        public void Configure(Image panel, TMP_Text name, TMP_Text people, RectTransform peopleFill,
            TMP_Text moraleLabel, RectTransform moraleBar, Image moraleBarImage)
        {
            background = panel; title = name; headcount = people; headcountFill = peopleFill;
            morale = moraleLabel; moraleFill = moraleBar; moraleFillImage = moraleBarImage;
        }

        public void Show(HudStyle style, string name, int alive, int capacity, float moraleValue, float moraleMax,
            MoraleBand band, string state, bool selected)
        {
            Selected = selected;
            if (selectionOutline != null && selectionOutline.activeSelf != selected) selectionOutline.SetActive(selected);
            if (title != null) title.text = name;
            if (headcount != null) headcount.text = PrepHudText.Headcount(alive, capacity);
            SetFill(headcountFill, PrepHudText.Ratio(alive, capacity));
            if (morale != null) morale.text = PrepHudText.MoraleLine(moraleValue, state);
            SetFill(moraleFill, PrepHudText.Ratio(moraleValue, moraleMax));
            if (style == null) return;
            if (moraleFillImage != null) moraleFillImage.color = style.MoraleFill(band);
            if (background != null) background.color = selected ? style.PanelSelected : style.Panel;
            if (frame != null) frame.color = selected ? style.FrameSelected : style.Frame;
        }

        static void SetFill(RectTransform fill, float ratio)
        {
            if (fill != null) fill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
