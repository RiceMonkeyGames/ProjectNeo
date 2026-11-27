using UnityEngine;
using UnityEngine.Audio;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// AudioDirector가 처음 만들어질 때 읽는 설정: 공용 믹서와 통로, 미리 만들 재생 자리 수.
    /// 기본 에셋은 Resources/Audio/AudioDirectorConfig에 두고 ProjectNeo/Setup/Audio Foundation 메뉴가 만든다.
    /// 믹서가 비어 있어도 동작하며 그때는 음량 설정을 소리 크기에 직접 곱한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Audio/Audio Director Config", fileName = "AudioDirectorConfig")]
    public sealed class AudioDirectorConfig : ScriptableObject
    {
        /// <summary>Resources.Load 경로.</summary>
        public const string ResourcePath = "Audio/AudioDirectorConfig";

        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup masterGroup;
        [SerializeField] AudioMixerGroup bgmGroup;
        [SerializeField] AudioMixerGroup sfxGroup;
        [SerializeField] AudioMixerGroup uiGroup;
        [SerializeField] AudioMixerGroup voiceGroup;
        [SerializeField, Min(1), Tooltip("위치가 있는 효과음(3D)을 동시에 낼 수 있는 수.")] int spatialVoices = 32;
        [SerializeField, Min(1), Tooltip("화면 소리(2D, UI 등)를 동시에 낼 수 있는 수.")] int flatVoices = 8;

        public AudioMixer Mixer => mixer;
        public int SpatialVoices => Mathf.Max(1, spatialVoices);
        public int FlatVoices => Mathf.Max(1, flatVoices);

        public AudioMixerGroup GroupFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return masterGroup;
                case AudioBus.Bgm: return bgmGroup != null ? bgmGroup : masterGroup;
                case AudioBus.Sfx: return sfxGroup != null ? sfxGroup : masterGroup;
                case AudioBus.Ui: return uiGroup != null ? uiGroup : masterGroup;
                case AudioBus.Voice: return voiceGroup != null ? voiceGroup : masterGroup;
                default: return masterGroup;
            }
        }

        /// <summary>믹서에 노출한 통로 음량 이름. 믹서를 만드는 에디터 메뉴도 이 이름을 쓴다.</summary>
        public static string VolumeParameter(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return "MasterVolume";
                case AudioBus.Bgm: return "BgmVolume";
                case AudioBus.Sfx: return "SfxVolume";
                case AudioBus.Ui: return "UiVolume";
                case AudioBus.Voice: return "VoiceVolume";
                default: return "MasterVolume";
            }
        }

        public static AudioDirectorConfig Create(
            AudioMixer mixer = null,
            AudioMixerGroup masterGroup = null,
            AudioMixerGroup bgmGroup = null,
            AudioMixerGroup sfxGroup = null,
            AudioMixerGroup uiGroup = null,
            AudioMixerGroup voiceGroup = null,
            int spatialVoices = 32,
            int flatVoices = 8)
        {
            var config = CreateInstance<AudioDirectorConfig>();
            config.mixer = mixer;
            config.masterGroup = masterGroup;
            config.bgmGroup = bgmGroup;
            config.sfxGroup = sfxGroup;
            config.uiGroup = uiGroup;
            config.voiceGroup = voiceGroup;
            config.spatialVoices = Mathf.Max(1, spatialVoices);
            config.flatVoices = Mathf.Max(1, flatVoices);
            return config;
        }
    }
}
