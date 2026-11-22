using System.Collections.Generic;
using ProjectNeo.Squads;
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
            if (morale != null) morale.text = PrepHudText.MoraleLine(moraleValue, moraleMax, state);
            SetFill(moraleFill, PrepHudText.Ratio(moraleValue, moraleMax));
            if (style == null) return;
            if (moraleFillImage != null) moraleFillImage.color = style.MoraleFill(band);
            if (background != null) background.color = selected ? style.PanelSelected : style.Panel;
            if (frame != null) frame.color = selected ? style.FrameSelected : style.Frame;
        }

        [Tooltip("UI-12 A안: 초상 왼쪽 위 분대 번호(선택 키와 같다).")]
        [SerializeField] TMP_Text number;
        [Tooltip("UI-12 A안: 이름 오른쪽 명령 상태 글자(정지/이동/공격). 버튼처럼 보이지 않게 판 없이 색만 바꾼다.")]
        [SerializeField] TMP_Text order;
        [SerializeField] Color orderHold = new Color(0.66f, 0.72f, 0.76f, 1f);
        [SerializeField] Color orderMove = new Color(0.55f, 0.78f, 1f, 1f);
        [SerializeField] Color orderAttack = new Color(1f, 0.55f, 0.47f, 1f);
        [Tooltip("UI-12 A안: 인원 칸 견본(숨김). 정원만큼 복제해 살아 있는 칸만 채운다. 비어 있으면 칸 게이지를 그리지 않는다.")]
        [SerializeField] Image segmentTemplate;
        [SerializeField] Color segmentOn = new Color(0.27f, 0.6f, 0.9f, 1f);
        [SerializeField] Color segmentOff = new Color(0.16f, 0.19f, 0.21f, 0.9f);
        readonly List<Image> segments = new List<Image>();

        public string NumberText => number != null ? number.text : string.Empty;
        public string OrderText => order != null ? order.text : string.Empty;
        public int SegmentCount { get { int n = 0; foreach (Image s in segments) if (s.gameObject.activeSelf) n++; return n; } }
        public int FilledSegments { get { int n = 0; foreach (Image s in segments) if (s.gameObject.activeSelf && s.color == segmentOn) n++; return n; } }

        /// <summary>UI-12 A안 부품(번호, 명령 상태 글자, 인원 칸 견본)과 칸 색을 붙인다.</summary>
        public void ConfigureDetails(TMP_Text numberLabel, TMP_Text orderLabel, Image headcountSegment, Color filled, Color empty)
        {
            number = numberLabel; order = orderLabel; segmentTemplate = headcountSegment; segmentOn = filled; segmentOff = empty;
        }

        /// <summary>번호, 명령, 인원 칸을 그린다. 칸은 정원만큼 두고 살아 있는 수만큼 채운다.</summary>
        public void ShowDetails(int squadNumber, SquadOrderKind orderKind, int alive, int capacity)
        {
            if (number != null) number.text = squadNumber.ToString();
            if (order != null)
            {
                order.text = SquadCommandHud.OrderName(orderKind);
                order.color = orderKind == SquadOrderKind.Attack ? orderAttack : orderKind == SquadOrderKind.Move ? orderMove : orderHold;
            }
            if (segmentTemplate == null) return;
            int count = Mathf.Max(0, capacity);
            while (segments.Count < count)
            {
                Image segment = Instantiate(segmentTemplate, segmentTemplate.transform.parent, false);
                segment.name = "Segment" + (segments.Count + 1);
                segment.transform.SetSiblingIndex(segmentTemplate.transform.GetSiblingIndex() + 1 + segments.Count);
                segments.Add(segment);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                bool used = i < count;
                if (segments[i].gameObject.activeSelf != used) segments[i].gameObject.SetActive(used);
                if (used) segments[i].color = i < alive ? segmentOn : segmentOff;
            }
        }

        static void SetFill(RectTransform fill, float ratio)
        {
            if (fill != null) fill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
