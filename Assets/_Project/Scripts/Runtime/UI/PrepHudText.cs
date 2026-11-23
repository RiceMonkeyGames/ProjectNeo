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

        /// <summary>제목 오른쪽 승천 표시(UI-12 사이드바). 예: "승천 1". 승천 0도 "승천 0"으로 보인다(사용자 2026-11-23).</summary>
        public static string AscensionTag(int ascension) => $"승천 {Math.Max(0, ascension)}";

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

        /// <summary>체력 숫자(UI-12 초상화 모양). 현재값과 "/최대"는 같은 크기이고 "/최대"만 조금 흐리다. 예: "80&lt;alpha=#99&gt;/100".</summary>
        public static string HealthNumber(int current, int maximum) =>
            $"{Math.Max(0, current)}<alpha=#99>/{Math.Max(0, maximum)}";

        /// <summary>남은 준비 시간 숫자(UI-12 모래시계 모양). 전체 시간 "/예산"만 조금 흐리다. 예: "5.5&lt;alpha=#99&gt;/8".</summary>
        public static string ClockNumber(float remaining, float budget) =>
            $"{Math.Max(0f, remaining).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}<alpha=#99>/{Math.Max(0f, budget).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}";

        /// <summary>칸 체력바의 칸 수. 최대 체력을 한 칸 체력으로 나눠 올림하되 1~maxSegments 사이다. 최대 체력이 0 이하면 0.</summary>
        public static int HealthSegments(float maximum, float perSegment, int maxSegments)
        {
            if (maximum <= 0f) return 0;
            int count = (int)Math.Ceiling(maximum / Math.Max(1f, perSegment) - 1e-4);
            return Math.Max(1, Math.Min(maxSegments, count));
        }

        /// <summary>조준점 링 아래 글(UI-12). 예: "전투 시작 62%", "1시간 기다리기 40%".</summary>
        public static string HoldRingLabel(bool waiting, float ratio, float waitHours)
        {
            int percent = (int)Math.Floor(Math.Max(0f, Math.Min(1f, ratio)) * 100f);
            return waiting ? $"{waitHours:0.#}시간 기다리기 {percent}%" : $"전투 시작 {percent}%";
        }

        /// <summary>
        /// 아래 가운데 경고 한 줄(UI-12 조준점 링 안). 막힌 전투 시작 알림(누른 뒤 잠깐), 미완료 작업, 성문 알림을
        /// " · "로 잇는다. 알릴 것이 없으면 빈 글.
        /// </summary>
        public static string Notice(string blockedStart, string workWarning, string gateMessage)
        {
            var text = new StringBuilder();
            foreach (string part in new[] { blockedStart, workWarning, gateMessage })
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (text.Length > 0) text.Append(" · ");
                text.Append(part);
            }
            return text.ToString();
        }

        /// <summary>
        /// 전투를 시작할 수 없을 때 시작 키를 누르면 잠깐 띄우는 글(UI-12). 커서가 풀려 있으면 화면을 클릭하라고,
        /// 그 밖(성벽 계산·전투에서 넘어오는 중)에는 잠시 뒤에 다시 누르라고 한다.
        /// </summary>
        public static string BlockedStart(bool cursorLocked) =>
            cursorLocked ? "잠시 뒤에 다시 누르세요" : "화면을 클릭한 뒤 다시 누르세요";

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

        /// <summary>UI-12 분대 카드 사기 줄(현재/최대). 최대가 0 이하면 현재만 적는다. 예: "사기 120/150", "사기 40/150 · 붕괴: 공포".</summary>
        public static string MoraleLine(float value, float max, string state)
        {
            if (max <= 0f) return MoraleLine(value, state);
            string text = $"사기 {Round(value)}/{Round(max)}";
            return string.IsNullOrEmpty(state) || state == "정상" ? text : text + " · " + state;
        }

        static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>사기 게이지 색 구간. 한계점 이하면 낮음, 높은 구간 문턱 이상이면 높음.</summary>
        public static MoraleBand Band(float value, float breakingPoint, float highBand)
        {
            if (value <= breakingPoint) return MoraleBand.Low;
            if (value >= highBand) return MoraleBand.High;
            return MoraleBand.Normal;
        }

        /// <summary>
        /// 키 목록 글. 한 줄에 "키 - 하는 일"(길게 누르는 키는 "키 - 길게 누르기 - 하는 일"). 키가 비어 있는 줄은 뺀다.
        /// </summary>
        public static string KeyList(IEnumerable<KeyHint> hints)
        {
            var text = new StringBuilder();
            if (hints == null) return string.Empty;
            foreach (KeyHint hint in hints)
            {
                if (hint.Key.Length == 0 || hint.Action.Length == 0) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(hint.Key);
                if (hint.SecondKey.Length > 0) text.Append(hint.Joiner.Length > 0 ? hint.Joiner : " ").Append(hint.SecondKey);
                text.Append(" - ");
                if (hint.Hold) text.Append("길게 누르기 - ");
                text.Append(hint.Action);
            }
            return text.ToString();
        }

        /// <summary>조준점 아래 상호작용 안내. 예: "F · 상점 - 사용". 대상이 없으면 빈 글.</summary>
        public static string ContextPrompt(string key, string prompt)
            => string.IsNullOrEmpty(prompt) ? string.Empty : $"{key} · {prompt}";
    }
}
