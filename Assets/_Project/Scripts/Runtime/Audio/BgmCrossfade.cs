namespace ProjectNeo.Audio
{
    /// <summary>
    /// 배경음 두 채널(0, 1)의 크기(0~1)를 정한다. 새 곡은 쉬던 채널에서 커지고 앞 곡은 같은 시간 동안 작아진다.
    /// 시간 흐름은 부르는 쪽이 Tick으로 넘긴다. 실제 AudioSource는 모른다.
    /// </summary>
    public sealed class BgmCrossfade
    {
        readonly float[] volume = new float[2];
        readonly float[] target = new float[2];
        readonly float[] rate = new float[2];

        /// <summary>지금 곡이 나오는(또는 커지는) 채널. 아무 곡도 시작하지 않았으면 -1.</summary>
        public int ActiveChannel { get; private set; } = -1;

        public float VolumeOf(int channel) => volume[channel];
        public float TargetOf(int channel) => target[channel];

        /// <summary>크기가 0이고 앞으로도 0이라 멈춰도 되는 채널인지.</summary>
        public bool IsSilent(int channel) => volume[channel] <= 0f && target[channel] <= 0f;

        public bool IsFading => volume[0] != target[0] || volume[1] != target[1];

        /// <summary>
        /// 새 곡을 쉬던 채널에서 0부터 시작한다. 그 채널이 아직 작아지는 중이었다면 바로 끊고 새로 시작한다.
        /// 돌려준 채널에 새 곡을 걸면 된다.
        /// </summary>
        public int CrossfadeToNew(float seconds)
        {
            int next = ActiveChannel == 0 ? 1 : 0;
            if (ActiveChannel >= 0) FadeTo(ActiveChannel, 0f, seconds);
            volume[next] = 0f;
            FadeTo(next, 1f, seconds);
            ActiveChannel = next;
            return next;
        }

        /// <summary>두 채널을 모두 작게 해서 끈다.</summary>
        public void FadeOut(float seconds)
        {
            FadeTo(0, 0f, seconds);
            FadeTo(1, 0f, seconds);
            ActiveChannel = -1;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = 0; i < 2; i++)
            {
                float step = rate[i] * deltaTime;
                if (volume[i] < target[i]) volume[i] = volume[i] + step >= target[i] ? target[i] : volume[i] + step;
                else if (volume[i] > target[i]) volume[i] = volume[i] - step <= target[i] ? target[i] : volume[i] - step;
            }
        }

        void FadeTo(int channel, float value, float seconds)
        {
            target[channel] = value;
            if (seconds <= 0f)
            {
                volume[channel] = value;
                rate[channel] = 0f;
                return;
            }
            float distance = value - volume[channel];
            rate[channel] = (distance < 0f ? -distance : distance) / seconds;
        }
    }
}
