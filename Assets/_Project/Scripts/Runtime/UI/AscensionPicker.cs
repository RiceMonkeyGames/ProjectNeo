using System;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 타이틀에서 새 게임을 누르면 열리는 출정 준비 화면(UI-05; FLOW-03, Q-78). 런 사이 해금 버튼과 함께 승천 단계를 고른다. 열린 단계 안에서만 고를 수 있고,
    /// 고른 단계까지 쌓인 규칙을 보여 준다. 시작을 누르면 고른 단계를 알려 주고 닫힌다.
    /// </summary>
    public sealed class AscensionPicker : MonoBehaviour
    {
        [SerializeField] AscensionTable table;
        [SerializeField] GameObject overlay;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text rulesLabel;
        [SerializeField, Tooltip("UI-06: separate line for how far ascension is unlocked. Without it the level label carries it.")]
        TMP_Text unlockedLabel;

        /// <summary>규칙이 이보다 많으면 한 줄에 하나씩이 아니라 이어 붙여 적는다(UI-06, 카드 안에 들어가게).</summary>
        const int RulesPerLineLimit = 5;

        Action<int> onStart;

        public AscensionTable Table => table;
        public bool IsOpen => overlay != null && overlay.activeSelf;
        /// <summary>지금 고른 단계(0 = 기본 게임).</summary>
        public int Selected { get; private set; }
        /// <summary>고를 수 있는 가장 높은 단계.</summary>
        public int Highest { get; private set; }
        public string LevelText => levelLabel != null ? levelLabel.text : string.Empty;
        public string RulesText => rulesLabel != null ? rulesLabel.text : string.Empty;
        public string UnlockedText => unlockedLabel != null ? unlockedLabel.text : string.Empty;

        public void Configure(AscensionTable ascension, GameObject root, TMP_Text level, TMP_Text rules)
        {
            table = ascension; overlay = root; levelLabel = level; rulesLabel = rules;
        }

        /// <summary>출정 준비 화면의 해금 단계 줄을 따로 둔다(UI-06). 두면 단계 글은 "승천 N"만 적는다.</summary>
        public void ConfigureUnlocked(TMP_Text unlocked) => unlockedLabel = unlocked;

        /// <summary>창을 연다. 처음 고른 단계는 열린 가장 높은 단계다. 표나 창이 없으면 false.</summary>
        public bool Open(int highestUnlocked, Action<int> start)
        {
            if (table == null || overlay == null) return false;
            Highest = table.Clamp(highestUnlocked);
            Selected = Highest;
            onStart = start;
            overlay.SetActive(true);
            Refresh();
            return true;
        }

        public void Previous() { if (IsOpen && Selected > 0) { Selected--; Refresh(); } }
        public void Next() { if (IsOpen && Selected < Highest) { Selected++; Refresh(); } }

        /// <summary>고른 단계로 시작한다. 창이 닫히고 열 때 받은 동작에 단계를 넘긴다.</summary>
        public void StartSelected()
        {
            if (!IsOpen) return;
            Action<int> start = onStart;
            Close();
            start?.Invoke(Selected);
        }

        public void Close()
        {
            onStart = null;
            if (overlay != null) overlay.SetActive(false);
        }

        void Refresh()
        {
            if (unlockedLabel != null)
            {
                // UI-06 카드: 단계 이름과 해금 정도를 두 줄로 나눈다. 좌우 화살표는 버튼이라 글에 넣지 않는다.
                if (levelLabel != null) levelLabel.text = Selected == 0 ? "승천 0 - 기본 게임" : $"승천 {Selected}";
                unlockedLabel.text = Highest == 0 ? "아직 열린 승천 단계가 없습니다" : $"승천 {Highest}까지 해금";
            }
            else if (levelLabel != null)
                levelLabel.text = Selected == 0
                    ? $"< 승천 0 - 기본 게임 >   ({Highest}까지 해금)"
                    : $"< 승천 {Selected} >   ({Highest}까지 해금)";
            if (rulesLabel == null) return;
            if (Selected == 0) { rulesLabel.text = "추가 규칙 없음."; return; }
            float multiplier = AscensionEffects.For(table, Selected).RewardMultiplier;
            var rules = table.DescribeUpTo(Selected);
            string body;
            if (unlockedLabel != null && rules.Count > RulesPerLineLimit)
            {
                // 이어 붙일 때는 규칙 하나가 줄 중간에서 끊기지 않게 묶는다.
                for (int i = 0; i < rules.Count; i++) rules[i] = "<nobr>" + rules[i] + "</nobr>";
                body = string.Join("  ·  ", rules);
            }
            else body = string.Join("\n", rules);
            rulesLabel.text = body + $"\n점수와 해금 포인트 x{multiplier:0.0#}";
        }
    }
}
