using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 화면 키 가이드(UI-12, UI-06 전체 키 목록을 바꿈). 준비 중 1인칭 탐색에서 오른쪽 아래에 키캡과 하는 일을 늘 같은 순서로
    /// 늘어놓는다(상호작용, 부대 선택/해제·명령, 창 키, 길게 누르는 전투 시작·기다리기, 메뉴, H). 처음에는 펼쳐져 있고
    /// H로 접으면 H 한 줄만 남는다. 키 이름은 매번 입력 동작에서 읽으므로 키를 바꾸면(UI-11) 따라 바뀐다.
    /// 줄 견본(KeyGuideRow)이 없으면 예전처럼 글 한 덩어리(body)에 적는다. 화면을 막지 않으며 조작 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class KeyListHud : MonoBehaviour
    {
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameBattleController battle;
        [SerializeField] SquadCommandCenter center;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text body;
        [Tooltip("예전 \"H 키 안내\" 한 줄. 줄 견본을 쓰면 숨긴다.")]
        [SerializeField] TMP_Text toggleHint;
        [Tooltip("가이드 한 줄 견본(UI-12). 숨겨 두고 복제한다. 비어 있으면 body 글로 보여 준다.")]
        [SerializeField] KeyGuideRow rowTemplate;

        readonly List<KeyHint> hints = new List<KeyHint>();
        readonly List<KeyHint> guide = new List<KeyHint>();
        readonly List<KeyGuideRow> rows = new List<KeyGuideRow>();
        PreparationInteraction listening;

        /// <summary>가이드를 펼쳐 두었는가(처음에는 펼침). 단계가 바뀌어도 기억한다.</summary>
        public bool ListOpen { get; private set; } = true;
        public bool IsShowing => content != null && content.activeSelf;
        public bool HintShowing => toggleHint != null && toggleHint.gameObject.activeSelf;
        public string Body => body != null ? body.text : string.Empty;
        public string HintText => toggleHint != null ? toggleHint.text : string.Empty;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;
        /// <summary>가이드 줄 오브젝트(견본에서 복제, 쓰지 않는 줄은 꺼져 있다).</summary>
        public IReadOnlyList<KeyGuideRow> Rows => rows;
        /// <summary>지금 화면에 보이는 가이드 줄.</summary>
        public IReadOnlyList<KeyHint> Shown => guide;

        public void Configure(PreparationInteraction input, GamePhaseView view, GameBattleController controller,
            SquadCommandCenter squads, GameObject panel, TMP_Text lines, TMP_Text hint)
        {
            Unlisten();
            interaction = input; phaseView = view; battle = controller; center = squads;
            content = panel; body = lines; toggleHint = hint;
            if (isActiveAndEnabled) Listen();
        }

        /// <summary>가이드 한 줄 견본을 붙인다(UI-12).</summary>
        public void ConfigureRows(KeyGuideRow template) => rowTemplate = template;

        /// <summary>가이드를 접거나 펼친다. H 키와 같다.</summary>
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
            if (rowTemplate == null)
            {
                // 예전 모양(UI-06): 닫혀 있으면 "H 키 안내" 한 줄, 열면 전체 목록 글.
                bool open = exploration && ListOpen;
                if (content.activeSelf != open) content.SetActive(open);
                if (toggleHint != null)
                {
                    if (toggleHint.gameObject.activeSelf != exploration) toggleHint.gameObject.SetActive(exploration);
                    if (exploration) toggleHint.text = $"{interaction.KeyListKey} " + (ListOpen ? "키 안내 닫기" : "키 안내");
                }
                if (open && body != null) body.text = PrepHudText.KeyList(Build());
                return;
            }

            if (content.activeSelf != exploration) content.SetActive(exploration);
            if (toggleHint != null && toggleHint.gameObject.activeSelf) toggleHint.gameObject.SetActive(false);
            if (!exploration) return;
            BuildGuide();
            ShowRows();
            if (body != null) body.text = PrepHudText.KeyList(guide);
        }

        void ShowRows()
        {
            while (rows.Count < guide.Count)
            {
                KeyGuideRow row = Instantiate(rowTemplate, rowTemplate.transform.parent, false);
                row.name = "KeyRow" + (rows.Count + 1);
                row.transform.SetSiblingIndex(rowTemplate.transform.GetSiblingIndex() + 1 + rows.Count);
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < guide.Count;
                if (rows[i].gameObject.activeSelf != used) rows[i].gameObject.SetActive(used);
                if (!used) continue;
                rows[i].Show(guide[i]);
                rows[i].Align(!ListOpen);
            }
        }

        /// <summary>
        /// 가이드 줄. 상황과 상관없이 늘 같은 줄을 같은 순서로 보여 준다(UI-12 사용자 결정: 줄이 움직이지 않아야 눈이 익는다).
        /// 접혀 있으면 펼치는 H 한 줄뿐이다. 바라보는 대상의 이름은 조준점 아래 안내가 맡는다.
        /// </summary>
        public IReadOnlyList<KeyHint> BuildGuide()
        {
            guide.Clear();
            if (interaction == null) return guide;
            if (!ListOpen)
            {
                guide.Add(new KeyHint(interaction.KeyListKey, "키 가이드 보기"));
                return guide;
            }
            guide.Add(new KeyHint(interaction.InteractKey, "상호작용"));
            SquadCommandSettings squads = center != null ? center.Settings : null;
            if (squads != null)
            {
                // 선택 키 범위와 전체 선택 키(`)는 따로 한 줄씩이다(한 키캡에 붙이면 `가 찌꺼기처럼 보였다).
                guide.Add(new KeyHint(SelectRange(squads), "부대 선택/해제"));
                guide.Add(new KeyHint(squads.SelectAll.GetBindingDisplayString(), "전체 선택/해제"));
                guide.Add(PageHint(squads));
                guide.Add(new KeyHint(squads.Move.GetBindingDisplayString(), "부대 이동"));
                guide.Add(new KeyHint(squads.Attack.GetBindingDisplayString(), "부대 공격"));
                guide.Add(new KeyHint(squads.Hold.GetBindingDisplayString(), "부대 정지"));
            }
            guide.Add(new KeyHint(interaction.EquipmentKey, "장비"));
            guide.Add(new KeyHint(interaction.OrganizationKey, "부대 편성"));
            guide.Add(new KeyHint(interaction.PlacementKey, "부대 배치"));
            guide.Add(new KeyHint(interaction.EnemyPreviewKey, "적 정보"));
            if (battle != null && battle.PerkWindow != null) guide.Add(new KeyHint(battle.PerkWindow.KeyText, "특성"));
            guide.Add(new KeyHint(interaction.BattleStartKey, "전투 시작", true));
            if (battle != null && battle.RunLink.State != null && battle.RunLink.State.PrepTime != null)
                guide.Add(new KeyHint(interaction.WaitKey, $"{battle.RunLink.State.PrepTime.WaitHours:0.#}시간 기다리기", true));
            if (battle != null && battle.PauseMenu != null) guide.Add(new KeyHint(battle.PauseMenu.KeyText, "메뉴"));
            guide.Add(new KeyHint(interaction.KeyListKey, "가이드 숨기기"));
            for (int i = guide.Count - 1; i >= 0; i--)
                if (guide[i].Key.Length == 0 || guide[i].Action.Length == 0) guide.RemoveAt(i);
            return guide;
        }

        /// <summary>분대 카드 페이지 줄(UI-12): "["와 "]"를 따로 한 칸씩 그린다.</summary>
        static KeyHint PageHint(SquadCommandSettings squads) => new KeyHint(squads.PreviousPage.GetBindingDisplayString(),
            "부대 페이지", false, squads.NextPage.GetBindingDisplayString());

        /// <summary>부대 선택 키 범위("F1~F10"). 키가 하나면 그 키만.</summary>
        static string SelectRange(SquadCommandSettings squads)
        {
            if (squads.SelectSquad.Length == 0) return string.Empty;
            string first = squads.SelectSquad[0].GetBindingDisplayString();
            string last = squads.SelectSquad[squads.SelectSquad.Length - 1].GetBindingDisplayString();
            return first == last ? first : $"{first}~{last}";
        }

        /// <summary>지금 키로 만든 전체 목록. 준비 중 1인칭 탐색에서 쓰는 키를 모두 담는다(상황과 상관없이).</summary>
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
                hints.Add(PageHint(squads));
                hints.Add(new KeyHint(squads.Move.GetBindingDisplayString(), "부대 이동"));
                hints.Add(new KeyHint(squads.Attack.GetBindingDisplayString(), "부대 공격"));
                hints.Add(new KeyHint(squads.Hold.GetBindingDisplayString(), "부대 정지"));
            }
            hints.Add(new KeyHint(interaction.BattleStartKey, "전투 시작", true));
            if (battle != null && battle.RunLink.State != null && battle.RunLink.State.PrepTime != null)
                hints.Add(new KeyHint(interaction.WaitKey, $"{battle.RunLink.State.PrepTime.WaitHours:0.#}시간 기다리기", true));
            if (battle != null && battle.PauseMenu != null) hints.Add(new KeyHint(battle.PauseMenu.KeyText, "메뉴 (저장 후 타이틀로)"));
            return hints;
        }
    }
}
