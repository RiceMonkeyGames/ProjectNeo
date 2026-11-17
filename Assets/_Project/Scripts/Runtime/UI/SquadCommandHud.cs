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

        /// <summary>부대 키를 한 줄로 적는다. 준비 안내처럼 다른 곳에서 같은 키를 보여줄 때 쓴다.</summary>
        public static string KeyLine(SquadCommandSettings settings)
        {
            if (settings == null) return "";
            string first = settings.SelectSquad.Length > 0 ? settings.SelectSquad[0].GetBindingDisplayString() : "";
            string last = settings.SelectSquad.Length > 0 ? settings.SelectSquad[settings.SelectSquad.Length - 1].GetBindingDisplayString() : "";
            return $"{first}~{last}·{settings.SelectAll.GetBindingDisplayString()} select squads - {settings.Move.GetBindingDisplayString()} move - "
                + $"{settings.Attack.GetBindingDisplayString()} attack - {settings.Hold.GetBindingDisplayString()} hold";
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
                case SquadOrderKind.Move: return "Move";
                case SquadOrderKind.Attack: return "Attack";
                default: return "Hold";
            }
        }

        /// <summary>"1번 부대 · 근접"처럼 번호와 종류를 붙인 이름. 종류는 부대원의 공격 방식에서 읽는다.</summary>
        public static string SquadName(SquadCommandCenter center, int squad)
        {
            string name = "Squad " + (squad + 1);
            if (center == null || squad < 0 || squad >= center.Squads.Count) return name;
            bool melee = false, ranged = false;
            foreach (ProjectNeo.Combat.UnitCombatAI member in center.Squads[squad].Members)
            {
                if (member == null || member.Actor == null || member.Actor.Profile == null) continue;
                if (member.Actor.Profile.AttackKind == ProjectNeo.Combat.AttackKind.Projectile) ranged = true;
                else melee = true;
            }
            if (melee && ranged) return name + " - Mixed";
            return melee ? name + " - Melee" : ranged ? name + " - Archers" : name;
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
                builder.Append("  ").Append(center.AliveCount(i)).Append(" units");
                SquadMorale value = morale != null ? morale.MoraleOf(i) : null;
                if (value != null)
                    builder.Append("  Morale ").Append(Mathf.RoundToInt(value.Value)).Append(" · ").Append(morale.StateOf(i));
                builder.Append('\n');
            }
            builder.Append(input.Message);
            if (morale != null && !string.IsNullOrEmpty(morale.LastNotice))
                builder.Append('\n').Append(morale.LastNotice);
            squads.text = builder.ToString();
        }

    }
}
