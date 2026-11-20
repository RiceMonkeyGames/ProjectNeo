using ProjectNeo.Flow;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 시간 시계 표시(PREP-06). 화면 맨 위 가운데(단계 표시 상자 바로 위) 한 줄로, 준비 중에는 1인칭·건설·시설 창·배치 화면
    /// 어디서나 보인다. 시설 창보다 위에 그려진다. 시간 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class PrepClockHud : MonoBehaviour
    {
        [SerializeField] GameBattleController battle;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text label;
        [Tooltip("열려 있는 동안 시계를 숨기는 화면. 거의 화면 전체를 덮어 시계 줄이 제목에 닿는 창(부대 편성)만 넣는다.")]
        [SerializeField] GameObject[] hideWhileOpen = new GameObject[0];

        public string Text => label != null ? label.text : string.Empty;
        public bool IsShowing => content != null && content.activeSelf;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;

        public void Configure(GameBattleController controller, GameObject line, TMP_Text text, params GameObject[] hidden)
        {
            battle = controller;
            content = line;
            label = text;
            hideWhileOpen = hidden ?? new GameObject[0];
        }

        bool Covered()
        {
            foreach (GameObject window in hideWhileOpen)
                if (window != null && window.activeInHierarchy) return true;
            return false;
        }

        void LateUpdate()
        {
            if (content == null || label == null) return;
            RunState state = battle != null ? battle.RunLink.State : null;
            bool show = state != null && state.PrepTime != null && GameSession.Flow.Phase == GamePhase.Preparation
                && state.ClockPreparation == state.BattlesFought && !Covered();
            if (content.activeSelf != show) content.SetActive(show);
            if (show) label.text = Describe(state.Clock);
        }

        /// <summary>시계 한 줄. 예: "Prep time 7h left / 12h". 다 쓰면 끝에 안내를 붙인다.</summary>
        public static string Describe(PrepClock clock)
        {
            string text = $"준비 시간 {clock.Remaining:0.#}시간 남음 / {clock.Budget:0.#}시간";
            return clock.IsExhausted ? text + " - 시간 없음" : text;
        }

        void OnDisable()
        {
            if (content != null) content.SetActive(false);
        }
    }
}
