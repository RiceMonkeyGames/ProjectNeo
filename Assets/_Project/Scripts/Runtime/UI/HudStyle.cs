using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 HUD(UI-06) 공용 모양 값: 색, 글자 크기, 판·테두리·게이지 스프라이트. 화면 구조와 따로 두어
    /// 이 에셋만 바꾸고 설정 메뉴를 다시 실행하면 모양이 바뀐다. 판은 색을 입힌 바탕(panelSprite) 위에
    /// 테두리(frameSprite)를 겹쳐 그리고, 게이지는 바탕 모양(gaugeSprite)으로 채움(gaugeFillSprite)을 잘라 그린다.
    /// 스프라이트가 비어 있으면 단색 판으로 그린다. PixelsPerUnit 값은 Image.pixelsPerUnitMultiplier로,
    /// 클수록 9-슬라이스 테두리가 얇게 보인다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/UI/HUD Style")]
    public sealed class HudStyle : ScriptableObject
    {
        [Header("Panels")]
        [SerializeField] Color panel = new Color(0.035f, 0.045f, 0.06f, 0.88f);
        [SerializeField] Color panelSelected = new Color(0.16f, 0.14f, 0.08f, 0.92f);
        [SerializeField] Sprite panelSprite;
        [SerializeField, Min(0.1f)] float panelPixelsPerUnit = 1f;
        [Header("Frames")]
        [SerializeField] Sprite frameSprite;
        [SerializeField] Color frame = Color.white;
        [SerializeField] Color frameSelected = Color.white;
        [SerializeField, Min(0.1f)] float framePixelsPerUnit = 1f;
        [Header("Text")]
        [SerializeField] Color text = Color.white;
        [SerializeField] Color heading = new Color(1f, 0.86f, 0.55f, 1f);
        [SerializeField] Color muted = new Color(0.72f, 0.74f, 0.78f, 1f);
        [SerializeField] Color warning = new Color(1f, 0.55f, 0.35f, 1f);
        [SerializeField, Min(8f)] float titleSize = 18f;
        [SerializeField, Min(8f)] float bodySize = 17f;
        [SerializeField, Min(8f)] float smallSize = 15f;
        [Header("Gauges")]
        [SerializeField] Color gaugeTrack = new Color(0.12f, 0.13f, 0.15f, 1f);
        [SerializeField] Sprite gaugeSprite;
        [SerializeField] Sprite gaugeFillSprite;
        [SerializeField, Min(0.1f)] float gaugePixelsPerUnit = 1f;
        [SerializeField] Color timeFill = new Color(0.92f, 0.74f, 0.32f, 1f);
        [SerializeField] Color headcountFill = new Color(0.45f, 0.68f, 0.9f, 1f);
        [SerializeField] Color moraleLow = new Color(0.85f, 0.25f, 0.2f, 1f);
        [SerializeField] Color moraleNormal = new Color(0.55f, 0.78f, 0.4f, 1f);
        [SerializeField] Color moraleHigh = new Color(0.98f, 0.82f, 0.35f, 1f);

        public Color Panel => panel;
        public Color PanelSelected => panelSelected;
        public Sprite PanelSprite => panelSprite;
        public float PanelPixelsPerUnit => panelPixelsPerUnit;
        public Sprite FrameSprite => frameSprite;
        public Color Frame => frame;
        public Color FrameSelected => frameSelected;
        public float FramePixelsPerUnit => framePixelsPerUnit;
        public Color Text => text;
        public Color Heading => heading;
        public Color Muted => muted;
        public Color Warning => warning;
        public float TitleSize => titleSize;
        public float BodySize => bodySize;
        public float SmallSize => smallSize;
        public Color GaugeTrack => gaugeTrack;
        public Sprite GaugeSprite => gaugeSprite;
        public Sprite GaugeFillSprite => gaugeFillSprite;
        public float GaugePixelsPerUnit => gaugePixelsPerUnit;
        public Color TimeFill => timeFill;
        public Color HeadcountFill => headcountFill;

        public Color MoraleFill(MoraleBand band) => band == MoraleBand.Low ? moraleLow
            : band == MoraleBand.High ? moraleHigh : moraleNormal;

        public static HudStyle Create() => CreateInstance<HudStyle>();
    }
}
