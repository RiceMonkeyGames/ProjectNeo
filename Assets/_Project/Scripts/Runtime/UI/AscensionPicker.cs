using System;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 타이틀에서 새 게임을 누른 뒤 승천 단계를 고르는 작은 창(FLOW-03, Q-78). 열린 단계 안에서만 고를 수 있고,
    /// 고른 단계까지 쌓인 규칙을 보여 준다. 시작을 누르면 고른 단계를 알려 주고 닫힌다.
    /// </summary>
    public sealed class AscensionPicker : MonoBehaviour
    {
        [SerializeField] AscensionTable table;
        [SerializeField] GameObject overlay;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text rulesLabel;

        Action<int> onStart;

        public AscensionTable Table => table;
        public bool IsOpen => overlay != null && overlay.activeSelf;
        /// <summary>지금 고른 단계(0 = 기본 게임).</summary>
        public int Selected { get; private set; }
        /// <summary>고를 수 있는 가장 높은 단계.</summary>
        public int Highest { get; private set; }
        public string LevelText => levelLabel != null ? levelLabel.text : string.Empty;
        public string RulesText => rulesLabel != null ? rulesLabel.text : string.Empty;

        public void Configure(AscensionTable ascension, GameObject root, TMP_Text level, TMP_Text rules)
        {
            table = ascension; overlay = root; levelLabel = level; rulesLabel = rules;
        }

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
            if (levelLabel != null)
                levelLabel.text = Selected == 0
                    ? $"< 승천 0 - 기본 게임 >   ({Highest}까지 해금)"
                    : $"< 승천 {Selected} >   ({Highest}까지 해금)";
            if (rulesLabel == null) return;
            if (Selected == 0) { rulesLabel.text = "추가 규칙 없음."; return; }
            float multiplier = AscensionEffects.For(table, Selected).RewardMultiplier;
            rulesLabel.text = string.Join("\n", table.DescribeUpTo(Selected)) + $"\n점수와 해금 포인트 x{multiplier:0.0#}";
        }
    }
}
