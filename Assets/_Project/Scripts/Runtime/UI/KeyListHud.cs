using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 전체 키 목록 창(UI-06). 준비 중 1인칭 탐색에서 H로 열고 닫는다. 평소에는 오른쪽 아래에 "H 키 안내" 한 줄만 보인다.
    /// 키 이름은 매번 입력 동작에서 읽으므로 키를 바꾸면(UI-11) 목록도 따라 바뀐다. 화면을 막지 않으며 조작 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class KeyListHud : MonoBehaviour
    {
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameBattleController battle;
        [SerializeField] SquadCommandCenter center;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text body;
        [Tooltip("목록이 닫혀 있을 때 보이는 짧은 안내(\"H 키 안내\").")]
        [SerializeField] TMP_Text toggleHint;

        readonly List<KeyHint> hints = new List<KeyHint>();
        PreparationInteraction listening;

        /// <summary>목록을 열어 두었는가. 단계가 바뀌어도 기억한다.</summary>
        public bool ListOpen { get; private set; }
        public bool IsShowing => content != null && content.activeSelf;
        public bool HintShowing => toggleHint != null && toggleHint.gameObject.activeSelf;
        public string Body => body != null ? body.text : string.Empty;
        public string HintText => toggleHint != null ? toggleHint.text : string.Empty;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;

        public void Configure(PreparationInteraction input, GamePhaseView view, GameBattleController controller,
            SquadCommandCenter squads, GameObject panel, TMP_Text lines, TMP_Text hint)
        {
            Unlisten();
            interaction = input; phaseView = view; battle = controller; center = squads;
            content = panel; body = lines; toggleHint = hint;
            if (isActiveAndEnabled) Listen();
        }

        /// <summary>목록을 열거나 닫는다. H 키와 같다.</summary>
        public void Toggle() => ListOpen = !ListOpen;

        void OnEnable() => Listen();

        void OnDisable()
        {
            Unlisten();
            if (content != null) content.SetActive(false);
            if (toggleHint != null) toggleHint.gameObject.SetActive(false);
        }

        void Listen()
        {
            Unlisten();
            if (interaction == null) return;
            interaction.KeyListToggled += Toggle;
            listening = interaction;
        }

        void Unlisten()
        {
            if (listening != null) listening.KeyListToggled -= Toggle;
            listening = null;
        }

        void LateUpdate()
        {
            if (content == null || interaction == null) return;
            bool exploration = GameSession.Flow.Phase == GamePhase.Preparation && phaseView != null && phaseView.IsExplorationView;
            bool show = exploration && ListOpen;
            if (content.activeSelf != show) content.SetActive(show);
            if (toggleHint != null)
            {
                if (toggleHint.gameObject.activeSelf != exploration) toggleHint.gameObject.SetActive(exploration);
                if (exploration) toggleHint.text = $"{interaction.KeyListKey} " + (ListOpen ? "키 안내 닫기" : "키 안내");
            }
            if (show && body != null) body.text = PrepHudText.KeyList(Build());
        }

        /// <summary>지금 키로 만든 목록. 준비 중 1인칭 탐색에서 쓰는 키를 모두 담는다.</summary>
        public IReadOnlyList<KeyHint> Build()
        {
            hints.Clear();
            hints.Add(new KeyHint("WASD", "이동"));
            hints.Add(new KeyHint(interaction.InteractKey, "상호작용"));
            hints.Add(new KeyHint(interaction.EquipmentKey, "장비"));
            hints.Add(new KeyHint(interaction.OrganizationKey, "부대 편성"));
            hints.Add(new KeyHint(interaction.PlacementKey, "부대 배치 화면"));
            hints.Add(new KeyHint(interaction.EnemyPreviewKey, "적 정보"));
            if (battle != null && battle.PerkWindow != null) hints.Add(new KeyHint(battle.PerkWindow.KeyText, "특성"));
            SquadCommandSettings squads = center != null ? center.Settings : null;
            if (squads != null)
            {
                hints.Add(new KeyHint(SquadCommandHud.SelectKeys(squads), "부대 선택 · 전체 선택"));
                hints.Add(new KeyHint(squads.Move.GetBindingDisplayString(), "부대 이동"));
                hints.Add(new KeyHint(squads.Attack.GetBindingDisplayString(), "부대 공격"));
                hints.Add(new KeyHint(squads.Hold.GetBindingDisplayString(), "부대 정지"));
            }
            hints.Add(new KeyHint(interaction.BattleStartKey, "길게 누르기 - 전투 시작"));
            if (battle != null && battle.RunLink.State != null && battle.RunLink.State.PrepTime != null)
                hints.Add(new KeyHint(interaction.WaitKey, $"길게 누르기 - {battle.RunLink.State.PrepTime.WaitHours:0.#}시간 기다리기"));
            // 여닫는 H 키는 오른쪽 아래 안내 줄에 늘 보이므로 목록에는 넣지 않는다(UI-06 A안 피드백).
            return hints;
        }
    }
}
