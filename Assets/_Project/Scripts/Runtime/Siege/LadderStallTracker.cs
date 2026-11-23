namespace ProjectNeo.Siege
{
    /// <summary>
    /// 고른 사다리에서 일이 진행되지 않는지 잰다(SIEGE-17). 사다리 밑까지 거리가 줄었거나 사다리에서 무언가 진행되면
    /// (세우는 중 진행도 증가, 누군가 오르는 중) 진행으로 본다. 정해진 시간 동안 진행이 없으면 막힌 것이다.
    /// </summary>
    public sealed class LadderStallTracker
    {
        readonly float minimumApproach;
        float lastProgress;
        float closest;
        bool started;

        public LadderStallTracker(float minimumApproach = 0.5f)
        {
            this.minimumApproach = minimumApproach > 0f ? minimumApproach : 0.01f;
        }

        public bool IsTracking => started;

        public void Reset(float now, float distance)
        {
            started = true;
            lastProgress = now;
            closest = distance;
        }

        public void Stop() => started = false;

        public void Observe(float now, float distance, bool ladderAdvanced)
        {
            if (!started) { Reset(now, distance); return; }
            if (ladderAdvanced || distance <= closest - minimumApproach)
            {
                lastProgress = now;
                if (distance < closest) closest = distance;
            }
        }

        public bool IsStalled(float now, float stallSeconds) =>
            started && stallSeconds > 0f && now - lastProgress >= stallSeconds;
    }
}
