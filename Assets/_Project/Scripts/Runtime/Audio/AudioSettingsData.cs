using System;
using UnityEngine;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 통로별 음량(0~1)과 음소거. 저장 슬롯과 상관없는 전역 설정이며 옵션 창(UI-11) 슬라이더가 바로 쓴다.
    /// JsonUtility로 저장하므로 필드 이름을 바꾸지 않는다.
    /// </summary>
    [Serializable]
    public sealed class AudioSettingsData
    {
        public const int CurrentVersion = 1;
        public const float DefaultVolume = 1f;

        public int version = CurrentVersion;
        public float masterVolume = DefaultVolume;
        public float bgmVolume = DefaultVolume;
        public float sfxVolume = DefaultVolume;
        public float uiVolume = DefaultVolume;
        public float voiceVolume = DefaultVolume;
        public bool masterMuted;
        public bool bgmMuted;
        public bool sfxMuted;
        public bool uiMuted;
        public bool voiceMuted;

        public float GetVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return masterVolume;
                case AudioBus.Bgm: return bgmVolume;
                case AudioBus.Sfx: return sfxVolume;
                case AudioBus.Ui: return uiVolume;
                case AudioBus.Voice: return voiceVolume;
                default: return DefaultVolume;
            }
        }

        /// <summary>음량을 바꾼다. 0~1 밖의 값은 잘라서 넣는다.</summary>
        public void SetVolume(AudioBus bus, float volume)
        {
            float value = AudioVolumeMath.Clamp01(volume);
            switch (bus)
            {
                case AudioBus.Master: masterVolume = value; break;
                case AudioBus.Bgm: bgmVolume = value; break;
                case AudioBus.Sfx: sfxVolume = value; break;
                case AudioBus.Ui: uiVolume = value; break;
                case AudioBus.Voice: voiceVolume = value; break;
            }
        }

        public bool IsMuted(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return masterMuted;
                case AudioBus.Bgm: return bgmMuted;
                case AudioBus.Sfx: return sfxMuted;
                case AudioBus.Ui: return uiMuted;
                case AudioBus.Voice: return voiceMuted;
                default: return false;
            }
        }

        public void SetMuted(AudioBus bus, bool muted)
        {
            switch (bus)
            {
                case AudioBus.Master: masterMuted = muted; break;
                case AudioBus.Bgm: bgmMuted = muted; break;
                case AudioBus.Sfx: sfxMuted = muted; break;
                case AudioBus.Ui: uiMuted = muted; break;
                case AudioBus.Voice: voiceMuted = muted; break;
            }
        }

        /// <summary>믹서 통로에 넣을 데시벨. 음소거면 무음이다. 마스터는 믹서가 따로 곱하므로 여기서 섞지 않는다.</summary>
        public float Decibels(AudioBus bus) =>
            IsMuted(bus) ? AudioVolumeMath.SilentDecibels : AudioVolumeMath.LinearToDecibels(GetVolume(bus));

        /// <summary>
        /// 마스터까지 곱한 실제 크기(0~1). 믹서가 없을 때 소리 크기에 직접 곱한다.
        /// </summary>
        public float EffectiveLinear(AudioBus bus)
        {
            if (masterMuted) return 0f;
            float master = AudioVolumeMath.Clamp01(masterVolume);
            if (bus == AudioBus.Master) return master;
            return IsMuted(bus) ? 0f : master * AudioVolumeMath.Clamp01(GetVolume(bus));
        }

        /// <summary>파일에서 읽은 뒤 범위를 벗어난 값을 바로잡는다.</summary>
        public void Sanitize()
        {
            masterVolume = Fix(masterVolume);
            bgmVolume = Fix(bgmVolume);
            sfxVolume = Fix(sfxVolume);
            uiVolume = Fix(uiVolume);
            voiceVolume = Fix(voiceVolume);
            version = CurrentVersion;
        }

        public AudioSettingsData Clone() => (AudioSettingsData)MemberwiseClone();

        public void CopyFrom(AudioSettingsData other)
        {
            if (other == null) return;
            masterVolume = other.masterVolume;
            bgmVolume = other.bgmVolume;
            sfxVolume = other.sfxVolume;
            uiVolume = other.uiVolume;
            voiceVolume = other.voiceVolume;
            masterMuted = other.masterMuted;
            bgmMuted = other.bgmMuted;
            sfxMuted = other.sfxMuted;
            uiMuted = other.uiMuted;
            voiceMuted = other.voiceMuted;
            version = other.version;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>JSON을 읽는다. 비었거나 읽을 수 없으면 null.</summary>
        public static AudioSettingsData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<AudioSettingsData>(json);
                data?.Sanitize();
                return data;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static float Fix(float value) => float.IsNaN(value) ? DefaultVolume : AudioVolumeMath.Clamp01(value);
    }
}
