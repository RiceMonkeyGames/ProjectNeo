using System;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽 높이(층수)에 따른 사다리 시간과 기울기 규칙(SIEGE-21). 씬을 알지 않는다.
    /// 오르기는 층마다 같은 시간, 세우기는 기본 시간에 추가 층마다 시간을 더하고, 발은 정한 기울기가 되도록 성벽에서 뗀다.
    /// </summary>
    public static class LadderScaleRules
    {
        /// <summary>성벽 꼭대기 높이를 한 층 높이로 나눈 층수. 최소 1층이며, 소수점 오차는 가까운 층으로 맞춘다.</summary>
        public static int Storeys(float wallTopHeight, float storeyHeight)
        {
            if (storeyHeight <= 0f || float.IsNaN(wallTopHeight) || wallTopHeight <= 0f) return 1;
            return Math.Max(1, (int)Math.Round(wallTopHeight / storeyHeight, MidpointRounding.AwayFromZero));
        }

        /// <summary>바닥부터 위쪽 끝까지 오르는 시간: 층당 시간 × 층수.</summary>
        public static float ClimbSeconds(int storeys, float secondsPerStorey)
            => Math.Max(0.01f, secondsPerStorey * Math.Max(1, storeys));

        /// <summary>바닥에서 끝까지 세우는 시간: 기본 시간 + 1층을 넘는 층마다 추가 시간.</summary>
        public static float RaiseSeconds(int storeys, float baseSeconds, float secondsPerExtraStorey)
            => Math.Max(0.01f, baseSeconds + Math.Max(0f, secondsPerExtraStorey) * (Math.Max(1, storeys) - 1));

        /// <summary>
        /// 위쪽 끝에서 발까지의 수평 거리. 높이 <paramref name="rise"/>를 땅과 <paramref name="leanDegrees"/>도로 기대게 한다.
        /// 각도는 1~89도로 자른다.
        /// </summary>
        public static float FootRun(float rise, float leanDegrees)
        {
            if (rise <= 0f || float.IsNaN(rise)) return 0f;
            double radians = Math.Max(1.0, Math.Min(89.0, leanDegrees)) * Math.PI / 180.0;
            return (float)(rise / Math.Tan(radians));
        }
    }
}
