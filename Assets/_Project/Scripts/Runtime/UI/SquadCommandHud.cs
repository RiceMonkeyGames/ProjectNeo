using System.Text;
using ProjectNeo.Squads;
using ProjectNeo.Morale;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>시험용 부대 지휘 표시. 선택한 부대와 현재 지시, 남은 인원을 보여준다. 정식 전투 HUD는 아니다.</summary>
    public sealed class SquadCommandHud : MonoBehaviour
    {
        [SerializeField] SquadCommandCenter center;
        [SerializeField] SquadCommandInput input;
        [SerializeField] TMP_Text squads;
        [SerializeField] TMP_Text help;
        [SerializeField, TextArea] string helpText;
        readonly StringBuilder builder = new StringBuilder();
        TMP_FontAsset runtimeFont;
        GameSquadMorale morale;
        public string DisplayText => squads != null ? squads.text : "";

        public void Configure(SquadCommandCenter commandCenter, SquadCommandInput commandInput,
            TMP_Text squadLabel, TMP_Text helpLabel, string helpCaption)
        {
            center = commandCenter; input = commandInput; squads = squadLabel; help = helpLabel; helpText = helpCaption;
        }

        void Awake()
        {
            // 시스템 글꼴을 런타임에 사용한다. 운영체제 글꼴 파일을 프로젝트에 복사하지 않는다.
            runtimeFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (runtimeFont == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
            help.text = helpText;
        }

        public static string OrderName(SquadOrderKind kind)
        {
            switch (kind)
            {
                case SquadOrderKind.Move: return "이동";
                case SquadOrderKind.Attack: return "공격";
                default: return "중지";
            }
        }

        /// <summary>"1번 부대 · 근접"처럼 번호와 종류를 붙인 이름. 종류는 부대원의 공격 방식에서 읽는다.</summary>
        public static string SquadName(SquadCommandCenter center, int squad)
        {
            string name = (squad + 1) + "번 부대";
            if (center == null || squad < 0 || squad >= center.Squads.Count) return name;
            bool melee = false, ranged = false;
            foreach (ProjectNeo.Combat.UnitCombatAI member in center.Squads[squad].Members)
            {
                if (member == null || member.Actor == null || member.Actor.Profile == null) continue;
                if (member.Actor.Profile.AttackKind == ProjectNeo.Combat.AttackKind.Projectile) ranged = true;
                else melee = true;
            }
            if (melee && ranged) return name + " · 혼성";
            return melee ? name + " · 근접" : ranged ? name + " · 궁수" : name;
        }

        void Start()
        {
            foreach (var candidate in FindObjectsByType<GameSquadMorale>())
                if (candidate.gameObject.scene == gameObject.scene) { morale = candidate; break; }
        }

        void Update()
        {
            if (runtimeFont == null || center.Commander == null) return;
            builder.Clear();
            for (int i = 0; i < center.Commander.SquadCount; i++)
            {
                builder.Append(center.Commander.IsSelected(i) ? "▶ " : "    ");
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

        void OnDestroy()
        {
            if (runtimeFont == null) return;
            foreach (Texture2D texture in runtimeFont.atlasTextures) if (texture != null) Destroy(texture);
            if (runtimeFont.material != null) Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }
    }
}
