using ProjectNeo.Core;

namespace ProjectNeo.Audio
{
    /// <summary>소리 한 번의 변형(클립 고르기, 크기·높이 흔들림). 연출 전용 랜덤만 쓰며 게임 규칙 랜덤과 섞지 않는다.</summary>
    public static class AudioCueRules
    {
        public const float MinPitch = 0.1f;
        public const float MaxPitch = 3f;

        /// <summary>
        /// 클립 번호를 고른다. 둘 이상이면 직전 번호를 바로 다시 고르지 않는다. 없으면 -1.
        /// 랜덤이 없으면 0번을 쓴다.
        /// </summary>
        public static int PickClipIndex(int count, int previous, IRandomSource random)
        {
            if (count <= 0) return -1;
            if (count == 1 || random == null) return 0;
            if (previous < 0 || previous >= count) return random.NextInt(count);
            int index = random.NextInt(count - 1);
            return index >= previous ? index + 1 : index;
        }

        /// <summary>기준값에서 ±range 안의 값을 고른다. range가 0 이하이거나 랜덤이 없으면 기준값.</summary>
        public static float Jitter(float value, float range, IRandomSource random)
        {
            if (range <= 0f || random == null) return value;
            return value + (random.NextFloat() * 2f - 1f) * range;
        }

        public static float JitterVolume(float volume, float range, IRandomSource random) =>
            AudioVolumeMath.Clamp01(Jitter(volume, range, random));

        public static float JitterPitch(float pitch, float range, IRandomSource random)
        {
            float value = Jitter(pitch, range, random);
            if (float.IsNaN(value) || value < MinPitch) return MinPitch;
            return value > MaxPitch ? MaxPitch : value;
        }
    }
}
