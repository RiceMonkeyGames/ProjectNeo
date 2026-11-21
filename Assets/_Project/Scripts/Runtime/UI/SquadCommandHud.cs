using System.Text;
using ProjectNeo.Squads;
using ProjectNeo.Morale;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.UI
{
    /// <summary>시험용 부대 지휘 표시. 선택한 부대와 현재 지시, 남은 인원을 보여준다. 정식 전투 HUD는 아니다.</summary>
    public sealed class SquadCommandHud : MonoBehaviour
    {
        [SerializeField] SquadCommandCenter center;
        [SerializeField] SquadCommandInput input;
        [SerializeField] TMP_Text squads;
        [SerializeField] TMP_Text help;
        readonly StringBuilder builder = new StringBuilder();
        GameSquadMorale morale;
        public string DisplayText => squads != null ? squads.text : "";
        /// <summary>키 안내 상자가 보이는지. 준비 단계에서는 준비 안내 줄이 부대 키를 대신 보여준다(PREP-05).</summary>
        public bool HelpVisible => help != null && help.gameObject.activeSelf;

        /// <summary>키 안내 글과 그 배경을 켜고 끈다. 부대 목록은 그대로 둔다.</summary>
        public void SetHelpVisible(bool visible)
        {
            if (help == null) return;
            help.gameObject.SetActive(visible);
            Transform backing = transform.Find(help.name + "Backing");
            if (backing != null) backing.gameObject.SetActive(visible);
        }

        /// <summary>분대 목록 글이 보이는지. 준비 화면에서는 분대 카드(UI-06)가 대신 보인다.</summary>
        public bool ListVisible => squads != null && squads.gameObject.activeSelf;

        /// <summary>분대 목록 글과 그 배경을 켜고 끈다. 키 안내 상자는 그대로 둔다.</summary>
        public void SetListVisible(bool visible)
        {
            if (squads == null || squads.gameObject.activeSelf == visible) return;
            squads.gameObject.SetActive(visible);
            Transform backing = transform.Find(squads.name + "Backing");
            if (backing != null) backing.gameObject.SetActive(visible);
        }

        /// <summary>부대 키를 한 줄로 적는다. 준비 안내처럼 다른 곳에서 같은 키를 보여줄 때 쓴다.</summary>
        public static string KeyLine(SquadCommandSettings settings)
        {
            if (settings == null) return "";
            return $"{SelectKeys(settings)} 부대 선택 - {settings.Move.GetBindingDisplayString()} 이동 - "
                + $"{settings.Attack.GetBindingDisplayString()} 공격 - {settings.Hold.GetBindingDisplayString()} 정지 - "
                + $"{Display(settings.DragFormation, "우클릭")} 끌어 전열 - {Display(settings.CycleFormation, "G")} 진형";
        }

        // 예전 설정 에셋에는 진형 키가 비어 있을 수 있어 기본 키 이름을 쓴다(COMBAT-08).
        static string Display(UnityEngine.InputSystem.InputAction action, string fallback) =>
            action != null && action.bindings.Count > 0 ? action.GetBindingDisplayString() : fallback;

        /// <summary>부대 선택 키 범위와 전체 선택 키("F1~F10·`"). 키 수는 설정 에셋에서 읽는다(PREP-09).</summary>
        public static string SelectKeys(SquadCommandSettings settings)
        {
            if (settings == null) return "";
            string first = settings.SelectSquad.Length > 0 ? settings.SelectSquad[0].GetBindingDisplayString() : "";
            string last = settings.SelectSquad.Length > 0 ? settings.SelectSquad[settings.SelectSquad.Length - 1].GetBindingDisplayString() : "";
            return $"{first}~{last}·{settings.SelectAll.GetBindingDisplayString()}";
        }

        public void Configure(SquadCommandCenter commandCenter, SquadCommandInput commandInput,
            TMP_Text squadLabel, TMP_Text helpLabel, string helpCaption)
        {
            center = commandCenter; input = commandInput; squads = squadLabel; help = helpLabel;
            if (help != null) help.text = helpCaption;
        }

        public static string OrderName(SquadOrderKind kind)
        {
            switch (kind)
            {
                case SquadOrderKind.Move: return "이동";
                case SquadOrderKind.Attack: return "공격";
                default: return "정지";
            }
        }

        /// <summary>"1번 부대 · 근접"처럼 번호와 종류를 붙인 이름. 종류는 부대원의 공격 방식에서 읽는다.</summary>
        public static string SquadName(SquadCommandCenter center, int squad)
        {
            string name = "부대 " + (squad + 1);
            if (center == null || squad < 0 || squad >= center.Squads.Count) return name;
            bool melee = false, ranged = false;
            foreach (ProjectNeo.Combat.UnitCombatAI member in center.Squads[squad].Members)
            {
                if (member == null || member.Actor == null || member.Actor.Profile == null) continue;
                if (member.Actor.Profile.AttackKind == ProjectNeo.Combat.AttackKind.Projectile) ranged = true;
                else melee = true;
            }
            if (melee && ranged) return name + " - 혼성";
            return melee ? name + " - 근접" : ranged ? name + " - 궁수" : name;
        }

        void Start()
        {
            foreach (var candidate in FindObjectsByType<GameSquadMorale>())
                if (candidate.gameObject.scene == gameObject.scene) { morale = candidate; break; }
        }

        void Update()
        {
            if (center.Commander == null) return;
            builder.Clear();
            for (int i = 0; i < center.Commander.SquadCount; i++)
            {
                builder.Append(center.Commander.IsSelected(i) ? "> " : "    ");
                builder.Append(SquadName(center, i)).Append("  ").Append(OrderName(center.Commander.OrderOf(i).Kind));
                builder.Append("  ").Append(center.AliveCount(i)).Append("명");
                SquadMorale value = morale != null ? morale.MoraleOf(i) : null;
                if (value != null)
                    builder.Append("  사기 ").Append(Mathf.RoundToInt(value.Value)).Append(" · ").Append(morale.StateOf(i));
                builder.Append('\n');
            }
            builder.Append(input.Message);
            if (morale != null && !string.IsNullOrEmpty(morale.LastNotice))
                builder.Append('\n').Append(morale.LastNotice);
            squads.text = builder.ToString();
        }

    }
}
