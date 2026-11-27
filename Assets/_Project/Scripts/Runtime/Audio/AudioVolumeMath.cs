using System;

namespace ProjectNeo.Audio
{
    /// <summary>슬라이더 값(0~1)과 믹서 데시벨 사이의 변환. 0은 완전한 무음(-80dB)이다.</summary>
    public static class AudioVolumeMath
    {
        /// <summary>믹서가 받는 가장 작은 값. 이 값이면 들리지 않는다.</summary>
        public const float SilentDecibels = -80f;

        /// <summary>이보다 작은 크기는 무음으로 본다(-80dB에 해당).</summary>
        public const float SilentLinear = 0.0001f;

        /// <summary>0~1 크기를 데시벨로 바꾼다. 범위를 벗어나거나 숫자가 아니면 가까운 끝값으로 본다.</summary>
        public static float LinearToDecibels(float linear)
        {
            float value = Clamp01(linear);
            if (value <= SilentLinear) return SilentDecibels;
            return Math.Max(SilentDecibels, 20f * (float)Math.Log10(value));
        }

        /// <summary>데시벨을 0~1 크기로 바꾼다. -80dB 이하는 0, 0dB 이상은 1이다.</summary>
        public static float DecibelsToLinear(float decibels)
        {
            if (float.IsNaN(decibels) || decibels <= SilentDecibels) return 0f;
            if (decibels >= 0f) return 1f;
            return Clamp01((float)Math.Pow(10.0, decibels / 20.0));
        }

        /// <summary>0~1로 자른다. 숫자가 아니면 0.</summary>
        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}
