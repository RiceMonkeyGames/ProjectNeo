using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 HUD(UI-06)에 쓰는 글과 게이지 비율. 장면 없이 시험할 수 있게 값만 받아 계산한다.
    /// 규칙 값은 바꾸지 않는다.
    /// </summary>
    public static class PrepHudText
    {
        /// <summary>위 자원 바 왼쪽의 단계 줄. 예: "준비 · 스테이지 2 · 승천 1".</summary>
        public static string StageLine(int stage, int ascension)
        {
            string text = $"준비 · 스테이지 {stage}";
            return ascension > 0 ? text + $" · 승천 {ascension}" : text;
        }

        /// <summary>왼쪽 위 깃발 옆 큰 제목(UI-06 A안). 예: "스테이지 2".</summary>
        public static string StageTitle(int stage) => $"스테이지 {stage}";

        /// <summary>전투 시작 판의 길게 누르기 게이지(UI-06 A안). 전투 시작이 차고 있으면 그것을, 아니면 기다리기를 보여 준다.</summary>
        public static float HoldRatio(float startProgress, float waitProgress, out bool waiting)
        {
            waiting = startProgress <= 0f && waitProgress > 0f;
            return Math.Max(0f, Math.Min(1f, waiting ? waitProgress : startProgress));
        }

        /// <summary>위 자원 바 오른쪽 줄. 골드 다음에 재료를 들어온 순서대로 적는다. 0개인 재료는 뺀다.</summary>
        public static string Resources(int gold, IEnumerable<KeyValuePair<string, int>> materials)
        {
            var text = new StringBuilder();
            text.Append("골드 ").Append(gold);
            if (materials == null) return text.ToString();
            foreach (KeyValuePair<string, int> material in materials)
            {
                if (material.Value <= 0 || string.IsNullOrEmpty(material.Key)) continue;
                text.Append("   ").Append(material.Key).Append(' ').Append(material.Value);
            }
            return text.ToString();
        }

        /// <summary>동전 아이콘 옆 골드 숫자(UI-06 A안). 천 단위 쉼표. 예: "1,250".</summary>
        public static string Gold(int gold) => gold.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>골드 옆 재료 줄(UI-06 A안). 들어온 순서대로, 0개인 재료는 뺀다. 재료가 없으면 빈 글.</summary>
        public static string Materials(IEnumerable<KeyValuePair<string, int>> materials)
        {
            var text = new StringBuilder();
            if (materials == null) return string.Empty;
            foreach (KeyValuePair<string, int> material in materials)
            {
                if (material.Value <= 0 || string.IsNullOrEmpty(material.Key)) continue;
                if (text.Length > 0) text.Append("   ");
                text.Append(material.Key).Append(' ').Append(material.Value);
            }
            return text.ToString();
        }

        /// <summary>게이지 채움 비율(0~1). 최댓값이 0 이하면 0이다.</summary>
        public static float Ratio(float value, float max)
        {
            if (max <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, value / max));
        }

        /// <summary>분대 카드 인원 줄. 예: "8/10명".</summary>
        public static string Headcount(int alive, int capacity) => $"{Math.Max(0, alive)}/{Math.Max(0, capacity)}명";

        /// <summary>분대 카드 사기 줄. 상태가 "정상"이 아니면 뒤에 붙인다. 예: "사기 120", "사기 40 · 붕괴: 공포".</summary>
        public static string MoraleLine(float value, string state)
        {
            string text = $"사기 {(int)Math.Round(value, MidpointRounding.AwayFromZero)}";
            return string.IsNullOrEmpty(state) || state == "정상" ? text : text + " · " + state;
        }

        /// <summary>사기 게이지 색 구간. 한계점 이하면 낮음, 높은 구간 문턱 이상이면 높음.</summary>
        public static MoraleBand Band(float value, float breakingPoint, float highBand)
        {
            if (value <= breakingPoint) return MoraleBand.Low;
            if (value >= highBand) return MoraleBand.High;
            return MoraleBand.Normal;
        }

        /// <summary>전체 키 목록 글. 한 줄에 "키 - 하는 일". 키가 비어 있는 줄은 뺀다.</summary>
        public static string KeyList(IEnumerable<KeyHint> hints)
        {
            var text = new StringBuilder();
            if (hints == null) return string.Empty;
            foreach (KeyHint hint in hints)
            {
                if (hint.Key.Length == 0 || hint.Action.Length == 0) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(hint.Key).Append(" - ").Append(hint.Action);
            }
            return text.ToString();
        }

        /// <summary>조준점 아래 상호작용 안내. 예: "F · 상점 - 사용". 대상이 없으면 빈 글.</summary>
        public static string ContextPrompt(string key, string prompt)
            => string.IsNullOrEmpty(prompt) ? string.Empty : $"{key} · {prompt}";
    }
}
