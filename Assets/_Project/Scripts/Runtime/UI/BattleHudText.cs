using System;

namespace ProjectNeo.UI
{
    /// <summary>전투 상황판(UI-13 전투 HUD A안) 글. 화면 없이 시험할 수 있게 글 만들기만 모았다.</summary>
    public static class BattleHudText
    {
        /// <summary>"웨이브 2/3": 나온 웨이브 수와 전체 수. "/전체"는 체력 숫자처럼 조금 흐리게. 웨이브가 없으면 1/1로 본다.</summary>
        public static string WaveNumber(int released, int count)
        {
            int total = Math.Max(1, count);
            int shown = Math.Min(total, Math.Max(1, released));
            return $"웨이브 {shown}<alpha=#99>/{total}";
        }

        /// <summary>웨이브 줄 오른쪽 글: 후퇴 중이면 "후퇴 중", 다음 웨이브까지 남은 초, 없으면 "마지막 웨이브".</summary>
        public static string WaveState(float secondsUntilNext, bool retreating)
        {
            if (retreating) return "후퇴 중";
            return secondsUntilNext >= 0f ? $"다음 {(int)Math.Ceiling(secondsUntilNext)}초" : "마지막 웨이브";
        }

        public static string Allies(int alive) => $"아군 {Math.Max(0, alive)}";

        /// <summary>성문 칸 글: 부서졌으면 "성문 파괴", 아니면 "성문".</summary>
        public static string Gate(bool destroyed) => destroyed ? "성문 파괴" : "성문";

        /// <summary>
        /// 키 가이드 소모품 줄(UI-13): "회복 물약 ×2", 다시 쓰기까지 남은 시간이 있으면 "(3초)"를 붙인다. 하나도 없으면 "소모품 없음".
        /// </summary>
        public static string Consumable(string name, int count, float cooldown)
        {
            if (count <= 0 || string.IsNullOrEmpty(name)) return "소모품 없음";
            string text = $"{name} ×{count}";
            return cooldown > 0f ? text + $" ({(int)Math.Ceiling(cooldown)}초)" : text;
        }
    }
}
