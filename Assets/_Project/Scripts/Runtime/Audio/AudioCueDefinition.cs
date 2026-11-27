using UnityEngine;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 소리 하나의 정의(AUDIO-01). 클립 후보, 크기·높이 흔들림, 2D/3D 정도, 들리는 거리, 우선순위,
    /// 재사용 대기 시간, 동시 재생 상한을 담는다. 게임 규칙은 이 에셋을 모르고 화면·연출 쪽만 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Audio/Audio Cue", fileName = "AudioCue")]
    public sealed class AudioCueDefinition : ScriptableObject
    {
        public const int MinPriority = 0;
        public const int MaxPriority = 255;
        public const int DefaultPriority = 128;

        [SerializeField] AudioClip[] clips = new AudioClip[0];
        [SerializeField] AudioBus bus = AudioBus.Sfx;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("크기를 ±이만큼 흔든다.")] float volumeJitter;
        [SerializeField, Range(AudioCueRules.MinPitch, AudioCueRules.MaxPitch)] float pitch = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("높이를 ±이만큼 흔든다.")] float pitchJitter;
        [SerializeField, Range(0f, 1f), Tooltip("0은 화면 소리(2D), 1은 위치가 있는 소리(3D).")] float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] float minDistance = 2f;
        [SerializeField, Min(0.01f), Tooltip("이보다 먼 곳의 3D 소리는 내지 않는다.")] float maxDistance = 60f;
        [SerializeField, Range(MinPriority, MaxPriority), Tooltip("클수록 중요하다. 자리가 모자라면 낮은 것부터 끊는다.")] int priority = DefaultPriority;
        [SerializeField, Min(0f), Tooltip("같은 소리를 다시 내기까지의 최소 초.")] float cooldownSeconds;
        [SerializeField, Min(0), Tooltip("같은 소리의 동시 재생 상한. 0은 제한 없음.")] int maxVoices;

        public int ClipCount => clips == null ? 0 : clips.Length;
        public AudioClip ClipAt(int index) => clips != null && index >= 0 && index < clips.Length ? clips[index] : null;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
                return false;
            }
        }

        public AudioBus Bus => bus;
        public float Volume => volume;
        public float VolumeJitter => volumeJitter;
        public float Pitch => pitch;
        public float PitchJitter => pitchJitter;
        public float SpatialBlend => spatialBlend;
        public bool IsSpatial => spatialBlend > 0f;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance < minDistance ? minDistance : maxDistance;
        public int Priority => priority;
        public float CooldownSeconds => cooldownSeconds;
        public int MaxVoices => maxVoices;

        public static AudioCueDefinition Create(
            AudioClip[] clips,
            AudioBus bus = AudioBus.Sfx,
            float volume = 1f,
            float volumeJitter = 0f,
            float pitch = 1f,
            float pitchJitter = 0f,
            float spatialBlend = 1f,
            float minDistance = 2f,
            float maxDistance = 60f,
            int priority = DefaultPriority,
            float cooldownSeconds = 0f,
            int maxVoices = 0)
        {
            var cue = CreateInstance<AudioCueDefinition>();
            cue.clips = clips ?? new AudioClip[0];
            cue.bus = bus;
            cue.volume = Mathf.Clamp01(volume);
            cue.volumeJitter = Mathf.Clamp01(volumeJitter);
            cue.pitch = Mathf.Clamp(pitch, AudioCueRules.MinPitch, AudioCueRules.MaxPitch);
            cue.pitchJitter = Mathf.Clamp01(pitchJitter);
            cue.spatialBlend = Mathf.Clamp01(spatialBlend);
            cue.minDistance = Mathf.Max(0.01f, minDistance);
            cue.maxDistance = Mathf.Max(0.01f, maxDistance);
            cue.priority = Mathf.Clamp(priority, MinPriority, MaxPriority);
            cue.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            cue.maxVoices = Mathf.Max(0, maxVoices);
            return cue;
        }
    }
}
