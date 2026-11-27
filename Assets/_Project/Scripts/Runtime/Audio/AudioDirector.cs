using System;
using System.Collections.Generic;
using System.IO;
using ProjectNeo.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 게임의 모든 소리를 내는 곳(AUDIO-01). 처음 Instance를 부를 때 한 번 만들어지고 씬이 바뀌어도 남는다.
    /// AudioSource는 처음에 정해진 수만 만들어 돌려 쓰므로 소리를 낼 때 오브젝트를 새로 만들지 않는다.
    /// 클립이 없거나 오디오 장치가 없어도 예외 없이 false를 돌려줄 뿐이다. AudioListener는 만들지 않는다(카메라 쪽 몫).
    /// 시간은 Time.unscaled*를 써서 일시정지 중에도 UI 소리와 배경음 전환이 움직인다.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const float DefaultBgmFadeSeconds = 1.5f;

        sealed class VoicePool
        {
            public AudioSource[] Sources;
            public VoiceAllocator Allocator;
        }

        static AudioDirector instance;

        /// <summary>하나뿐인 Director. 없으면 Resources의 기본 설정으로 만든다.</summary>
        public static AudioDirector Instance => instance != null ? instance : CreateDefault();

        /// <summary>이미 만들어져 있는지. 만들지 않고 확인만 할 때 쓴다.</summary>
        public static bool HasInstance => instance != null;

        public static string DefaultSettingsDirectory => Path.Combine(Application.persistentDataPath, "Settings");

        AudioDirectorConfig config;
        AudioSettingsStore store;
        IRandomSource random;
        AudioSettingsData settings = new AudioSettingsData();
        VoicePool spatial;
        VoicePool flat;
        readonly AudioSource[] bgm = new AudioSource[2];
        readonly AudioCueDefinition[] bgmCues = new AudioCueDefinition[2];
        AudioSource stinger;
        AudioCueDefinition stingerCue;
        readonly BgmCrossfade crossfade = new BgmCrossfade();
        readonly CueCooldownGate cooldowns = new CueCooldownGate();
        readonly Dictionary<AudioCueDefinition, int> cueKeys = new Dictionary<AudioCueDefinition, int>();
        readonly Dictionary<int, int> lastClip = new Dictionary<int, int>();
        AudioListener listener;
        int listenerSearchFrame = -1;

        /// <summary>기본 설정(Resources), 기본 저장 폴더, 시계 기반 연출 랜덤으로 만든다.</summary>
        public static AudioDirector CreateDefault()
        {
            var loaded = Resources.Load<AudioDirectorConfig>(AudioDirectorConfig.ResourcePath);
            if (loaded == null) Debug.LogWarning($"AudioDirector: Resources/{AudioDirectorConfig.ResourcePath} not found. Playing without a mixer.");
            return Create(loaded, new AudioSettingsStore(DefaultSettingsDirectory), new SeededRandom(Environment.TickCount));
        }

        /// <summary>
        /// 설정과 저장 위치, 연출 랜덤을 정해 새로 만든다. 이미 있던 Director는 없앤다. 테스트가 임시 폴더를 넘길 때 쓴다.
        /// store가 null이면 음량 설정을 파일에서 읽거나 저장하지 않는다.
        /// </summary>
        public static AudioDirector Create(AudioDirectorConfig config, AudioSettingsStore store, IRandomSource random)
        {
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance.gameObject);
                else DestroyImmediate(instance.gameObject);
                instance = null;
            }
            var root = new GameObject("AudioDirector");
            var director = root.AddComponent<AudioDirector>();
            director.Initialize(config != null ? config : AudioDirectorConfig.Create(), store, random);
            if (Application.isPlaying) DontDestroyOnLoad(root);
            instance = director;
            return director;
        }

        public AudioDirectorConfig Config => config;
        public bool HasMixer => config != null && config.Mixer != null;
        public int SpatialVoiceCapacity => spatial.Allocator.Capacity;
        public int FlatVoiceCapacity => flat.Allocator.Capacity;
        public int ActiveSpatialVoices => spatial.Allocator.ActiveCount;
        public int ActiveFlatVoices => flat.Allocator.ActiveCount;

        /// <summary>이 소리가 지금 몇 자리에서 나고 있는지(3D와 2D 합).</summary>
        public int ActiveVoicesOf(AudioCueDefinition cue)
        {
            if (cue == null || !cueKeys.TryGetValue(cue, out int key)) return 0;
            return spatial.Allocator.CountOf(key) + flat.Allocator.CountOf(key);
        }

        void Initialize(AudioDirectorConfig directorConfig, AudioSettingsStore settingsStore, IRandomSource randomSource)
        {
            config = directorConfig;
            store = settingsStore;
            random = randomSource;
            if (store != null) settings = store.Load();
            spatial = CreatePool("Spatial Voice", config.SpatialVoices);
            flat = CreatePool("Flat Voice", config.FlatVoices);
            for (int i = 0; i < bgm.Length; i++)
            {
                bgm[i] = CreateSource($"Bgm {i}");
                bgm[i].loop = true;
                bgm[i].priority = 0;
                bgm[i].outputAudioMixerGroup = config.GroupFor(AudioBus.Bgm);
            }
            stinger = CreateSource("Stinger");
            stinger.priority = 0;
            stinger.outputAudioMixerGroup = config.GroupFor(AudioBus.Bgm);
            ApplyMixer();
        }

        void Start() => ApplyMixer();

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Update()
        {
            Reclaim(spatial);
            Reclaim(flat);
            crossfade.Tick(Time.unscaledDeltaTime);
            ApplyBgm();
        }

        // ---------- 효과음 ----------

        /// <summary>화면 소리(2D)로 낸다. 낼 수 없으면(클립 없음, 대기 중, 자리 없음) false.</summary>
        public bool Play(AudioCueDefinition cue) => PlayVoice(flat, cue, Vector3.zero, false);

        /// <summary>
        /// 위치에서 낸다. 2D 소리는 화면 소리로 낸다. 듣는 사람에게서 최대 거리보다 먼 3D 소리는 내지 않는다.
        /// </summary>
        public bool PlayAt(AudioCueDefinition cue, Vector3 position) =>
            cue != null && cue.IsSpatial ? PlayVoice(spatial, cue, position, true) : Play(cue);

        bool PlayVoice(VoicePool pool, AudioCueDefinition cue, Vector3 position, bool positional)
        {
            if (cue == null || !cue.HasClips) return false;
            int key = KeyOf(cue);
            float now = Time.unscaledTime;
            if (!cooldowns.IsReady(key, now)) return false;
            float distanceSq = 0f;
            if (positional)
            {
                distanceSq = (position - ListenerPosition()).sqrMagnitude;
                float max = cue.MaxDistance;
                if (distanceSq > max * max) return false;
            }
            AudioClip clip = PickClip(cue, key);
            if (clip == null) return false;
            int slot = pool.Allocator.Allocate(key, cue.Priority, distanceSq, cue.MaxVoices, out bool stolen);
            if (slot < 0) return false;
            cooldowns.Mark(key, now, cue.CooldownSeconds);

            AudioSource source = pool.Sources[slot];
            if (stolen) source.Stop();
            source.clip = clip;
            source.volume = AudioCueRules.JitterVolume(cue.Volume, cue.VolumeJitter, random) * BusGain(cue.Bus);
            source.pitch = AudioCueRules.JitterPitch(cue.Pitch, cue.PitchJitter, random);
            source.spatialBlend = positional ? cue.SpatialBlend : 0f;
            source.minDistance = cue.MinDistance;
            source.maxDistance = cue.MaxDistance;
            source.priority = AudioCueDefinition.MaxPriority - cue.Priority;
            source.outputAudioMixerGroup = config.GroupFor(cue.Bus);
            source.transform.position = positional ? position : transform.position;
            source.Play();
            return true;
        }

        void Reclaim(VoicePool pool)
        {
            for (int i = 0; i < pool.Sources.Length; i++)
                if (pool.Allocator.IsBusy(i) && !pool.Sources[i].isPlaying) pool.Allocator.Release(i);
        }

        /// <summary>모든 효과음을 바로 멈춘다(배경음은 그대로).</summary>
        public void StopAllSfx()
        {
            StopPool(spatial);
            StopPool(flat);
            stinger.Stop();
            stingerCue = null;
        }

        static void StopPool(VoicePool pool)
        {
            for (int i = 0; i < pool.Sources.Length; i++) pool.Sources[i].Stop();
            pool.Allocator.ReleaseAll();
        }

        // ---------- 배경음 ----------

        /// <summary>지금 나오는(또는 커지는 중인) 배경음. 없으면 null.</summary>
        public AudioCueDefinition CurrentBgm => crossfade.ActiveChannel >= 0 ? bgmCues[crossfade.ActiveChannel] : null;

        public AudioSource BgmSource(int channel) => bgm[channel];
        public float BgmChannelVolume(int channel) => crossfade.VolumeOf(channel);
        public bool IsBgmFading => crossfade.IsFading;

        /// <summary>
        /// 배경음을 바꾼다. 앞 곡은 fadeSeconds 동안 작아지고 새 곡은 같은 시간 동안 커진다. 0초면 바로 바뀐다.
        /// 같은 곡이 이미 나오고 있으면 다시 시작하지 않고 true.
        /// </summary>
        public bool PlayBgm(AudioCueDefinition cue, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            if (cue == null || !cue.HasClips) return false;
            if (CurrentBgm == cue) return true;
            AudioClip clip = PickClip(cue, KeyOf(cue));
            if (clip == null) return false;
            int channel = crossfade.CrossfadeToNew(fadeSeconds);
            AudioSource source = bgm[channel];
            source.Stop();
            source.clip = clip;
            source.pitch = cue.Pitch;
            source.outputAudioMixerGroup = config.GroupFor(cue.Bus);
            bgmCues[channel] = cue;
            ApplyBgm();
            source.Play();
            return true;
        }

        /// <summary>배경음을 fadeSeconds 동안 줄여서 끈다.</summary>
        public void StopBgm(float fadeSeconds = DefaultBgmFadeSeconds)
        {
            crossfade.FadeOut(fadeSeconds);
            ApplyBgm();
        }

        /// <summary>
        /// 단계 전환 알림처럼 짧은 곡을 배경음 통로로 한 번 낸다. 배경음은 끊지 않는다. 앞의 알림은 끊는다.
        /// </summary>
        public bool PlayStinger(AudioCueDefinition cue)
        {
            if (cue == null || !cue.HasClips) return false;
            AudioClip clip = PickClip(cue, KeyOf(cue));
            if (clip == null) return false;
            stinger.Stop();
            stinger.clip = clip;
            stinger.volume = AudioCueRules.JitterVolume(cue.Volume, cue.VolumeJitter, random) * BusGain(cue.Bus);
            stinger.pitch = AudioCueRules.JitterPitch(cue.Pitch, cue.PitchJitter, random);
            stinger.outputAudioMixerGroup = config.GroupFor(cue.Bus);
            stingerCue = cue;
            stinger.Play();
            return true;
        }

        public bool IsStingerPlaying => stingerCue != null && stinger.isPlaying;

        void ApplyBgm()
        {
            for (int i = 0; i < bgm.Length; i++)
            {
                AudioCueDefinition cue = bgmCues[i];
                if (cue == null) continue;
                if (crossfade.IsSilent(i))
                {
                    bgm[i].Stop();
                    bgm[i].clip = null;
                    bgmCues[i] = null;
                    continue;
                }
                bgm[i].volume = crossfade.VolumeOf(i) * cue.Volume * BusGain(cue.Bus);
            }
        }

        // ---------- 음량 설정 ----------

        /// <summary>지금 음량 설정의 복사본. 바꾸려면 SetVolume/SetMuted/ApplySettings를 쓴다.</summary>
        public AudioSettingsData Settings => settings.Clone();

        public float GetVolume(AudioBus bus) => settings.GetVolume(bus);
        public bool IsMuted(AudioBus bus) => settings.IsMuted(bus);

        /// <summary>음량을 바꾸고 믹서에 바로 반영한다. 파일 저장은 SaveSettings가 한다.</summary>
        public void SetVolume(AudioBus bus, float volume)
        {
            settings.SetVolume(bus, volume);
            ApplyMixer();
            ApplyBgm();
        }

        public void SetMuted(AudioBus bus, bool muted)
        {
            settings.SetMuted(bus, muted);
            ApplyMixer();
            ApplyBgm();
        }

        /// <summary>설정 전체를 바꾼다(옵션 창의 취소·기본값 복원).</summary>
        public void ApplySettings(AudioSettingsData data)
        {
            if (data == null) return;
            settings.CopyFrom(data);
            settings.Sanitize();
            ApplyMixer();
            ApplyBgm();
        }

        /// <summary>지금 설정을 파일에 저장한다. 저장 위치가 없거나 실패하면 false.</summary>
        public bool SaveSettings() => store != null && store.Save(settings);

        /// <summary>믹서에 실제로 들어간 통로 음량(dB). 믹서가 없거나 값을 읽지 못하면 false.</summary>
        public bool TryGetMixerDecibels(AudioBus bus, out float decibels)
        {
            decibels = 0f;
            return HasMixer && config.Mixer.GetFloat(AudioDirectorConfig.VolumeParameter(bus), out decibels);
        }

        void ApplyMixer()
        {
            if (!HasMixer) return;
            AudioMixer mixer = config.Mixer;
            foreach (AudioBus bus in (AudioBus[])Enum.GetValues(typeof(AudioBus)))
                mixer.SetFloat(AudioDirectorConfig.VolumeParameter(bus), settings.Decibels(bus));
        }

        /// <summary>믹서가 있으면 믹서가 음량을 맡고, 없으면 설정값을 소리 크기에 직접 곱한다.</summary>
        float BusGain(AudioBus bus) => HasMixer ? 1f : settings.EffectiveLinear(bus);

        // ---------- 내부 도움 ----------

        int KeyOf(AudioCueDefinition cue)
        {
            if (!cueKeys.TryGetValue(cue, out int key))
            {
                key = cueKeys.Count;
                cueKeys.Add(cue, key);
            }
            return key;
        }

        AudioClip PickClip(AudioCueDefinition cue, int key)
        {
            int previous = lastClip.TryGetValue(key, out int last) ? last : -1;
            int index = AudioCueRules.PickClipIndex(cue.ClipCount, previous, random);
            if (index < 0) return null;
            lastClip[key] = index;
            return cue.ClipAt(index);
        }

        Vector3 ListenerPosition()
        {
            if ((listener == null || !listener.isActiveAndEnabled) && listenerSearchFrame != Time.frameCount)
            {
                listenerSearchFrame = Time.frameCount;
                listener = FindAnyObjectByType<AudioListener>();
            }
            return listener != null && listener.isActiveAndEnabled ? listener.transform.position : transform.position;
        }

        VoicePool CreatePool(string prefix, int count)
        {
            var pool = new VoicePool { Sources = new AudioSource[count], Allocator = new VoiceAllocator(count) };
            for (int i = 0; i < count; i++) pool.Sources[i] = CreateSource($"{prefix} {i:00}");
            return pool;
        }

        AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
