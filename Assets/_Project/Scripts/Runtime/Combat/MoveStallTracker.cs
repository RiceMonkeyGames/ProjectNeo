using System;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 목적지 근처에서 더 나아가지 못하는지 판단한다. 경로 탐색 없이 남은 거리만 본다.
    /// 길이 끊긴 이동에서 여러 유닛의 경로 끝점이 같으면 한 명만 그 점에 설 수 있으므로,
    /// 끝점 근처에서 일정 시간 줄지 않는 유닛을 도착한 것으로 보아 이동 지시가 끝나지 않는 문제를 막는다.
    /// </summary>
    public sealed class MoveStallTracker
    {
        float best = float.PositiveInfinity;
        float stalled;

        public float StalledSeconds => stalled;

        public void Reset()
        {
            best = float.PositiveInfinity;
            stalled = 0f;
        }

        /// <param name="remaining">목적지까지 남은 거리</param>
        /// <param name="deltaTime">지난 프레임 시간</param>
        /// <param name="nearDistance">이 거리 안에서만 막힘을 센다. 멀리서 잠깐 막힌 것은 도착으로 보지 않는다.</param>
        /// <param name="minProgress">이만큼 더 가까워지면 아직 나아가는 중으로 본다.</param>
        /// <param name="stallSeconds">이 시간 동안 나아가지 못하면 막힌 것으로 본다.</param>
        public bool IsStuck(float remaining, float deltaTime, float nearDistance, float minProgress, float stallSeconds)
        {
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining > nearDistance)
            {
                Reset();
                return false;
            }
            if (remaining < best - Math.Max(0f, minProgress))
            {
                best = remaining;
                stalled = 0f;
                return false;
            }
            if (deltaTime > 0f && !float.IsNaN(deltaTime)) stalled += deltaTime;
            return stalled >= stallSeconds;
        }
    }
}
