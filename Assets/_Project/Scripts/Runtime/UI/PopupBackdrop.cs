using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 씬에 놓인 팝업 창(분대 편성, 장비, 시설) 뒤를 어둡게 덮는 막(UI-13, 사용자 2026-11-24: 특성창처럼 주변을 어둡게).
    /// 창에 붙이면 창을 위 그리기 순서로 올리고, 그 바로 아래 순서에 화면 전체를 덮는 반투명 검은 막을 깐다.
    /// 막은 창이 켜져 있는 동안만 켜지고 뒤 HUD 클릭도 막는다. 그리기 순서: HUD(0)·스킬(25)·보스 체력(30) &lt; 막 &lt; 창 &lt; 특성창(40)·Esc 메뉴(60).
    /// 씬 파일을 고치지 않도록 실행 중에 붙인다.
    /// </summary>
    public sealed class PopupBackdrop : MonoBehaviour
    {
        public static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.55f);
        public const int ShadeOrder = 35;
        public const int PanelOrder = 36;

        GameObject shade;

        /// <summary>지금 막이 보이는가(테스트용).</summary>
        public bool ShadeShowing => shade != null && shade.activeSelf;

        /// <summary>창에 막을 붙인다. 이미 붙어 있으면 그것을 돌려준다.</summary>
        public static PopupBackdrop Attach(GameObject panel)
        {
            if (panel == null) return null;
            var backdrop = panel.GetComponent<PopupBackdrop>();
            return backdrop != null ? backdrop : panel.AddComponent<PopupBackdrop>();
        }

        void OnEnable()
        {
            if (shade == null) Build();
            if (shade != null) shade.SetActive(true);
        }

        void OnDisable()
        {
            if (shade != null) shade.SetActive(false);
        }

        void OnDestroy()
        {
            if (shade != null) Destroy(shade);
        }

        void Build()
        {
            Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            if (parentCanvas == null) return;
            Canvas root = parentCanvas.rootCanvas;

            // 창을 자기 그리기 순서로 올린다. 클릭을 받도록 레이캐스터도 단다.
            var own = GetComponent<Canvas>();
            if (own == null) own = gameObject.AddComponent<Canvas>();
            own.overrideSorting = true;
            own.sortingOrder = PanelOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            shade = new GameObject(name + "_Shade", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            var rect = (RectTransform)shade.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = shade.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = ShadeOrder;
            var image = shade.GetComponent<Image>();
            image.color = ShadeColor;
            image.raycastTarget = true;
            shade.SetActive(false);
        }
    }
}
