using System.Collections;
using System.IO;
using NUnit.Framework;
using ProjectNeo.Audio;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// AUDIO-01: 미리 만든 AudioSource를 돌려 쓰는지, 배경음 전환, 믹서 통로 연결, 씬 전환 뒤에도 하나만 남는지 본다.
    /// 음량 설정은 임시 폴더에만 저장하고, 믹서에 넣은 값은 끝나면 지운다.
    /// </summary>
    public class AudioDirectorPlayTests
    {
        static int cleanup;
        bool loadedScenes;
        string settingsDirectory;
        AudioDirectorConfig testConfig;
        readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.Reset();
            settingsDirectory = Path.Combine(Path.GetTempPath(), "ProjectNeoAudioDirectorTests_" + System.Guid.NewGuid().ToString("N"));
            GameSession.UseSaveStore(new ProjectNeo.Meta.MetaSaveStore(Path.Combine(settingsDirectory, "Saves")));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioMixer mixer = AudioDirector.HasInstance ? AudioDirector.Instance.Config.Mixer : null;
            if (AudioDirector.HasInstance) Object.Destroy(AudioDirector.Instance.gameObject);
            if (mixer != null)
                foreach (AudioBus bus in (AudioBus[])System.Enum.GetValues(typeof(AudioBus)))
                    mixer.ClearFloat(AudioDirectorConfig.VolumeParameter(bus));
            foreach (Object item in created) if (item != null) Object.Destroy(item);
            created.Clear();
            if (testConfig != null) Object.Destroy(testConfig);
            testConfig = null;
            yield return null;

            // 씬을 직접 연 테스트만 씬을 정리한다. 처음 테스트 씬을 내리면 테스트 실행기가 멈춘다.
            if (loadedScenes)
            {
                loadedScenes = false;
                while (SceneSwitcher.IsLoading) yield return null;
                Scene empty = SceneManager.CreateScene("AudioDirectorCleanup" + cleanup++);
                SceneManager.SetActiveScene(empty);
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                    if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            }
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, true);
        }

        /// <summary>빈 테스트 씬에는 듣는 사람이 없어 Unity가 경고를 남긴다. 원점에 하나 둔다(씬을 바꾸면 함께 사라진다).</summary>
        void EnsureListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() != null) return;
            var listener = new GameObject("TestAudioListener", typeof(AudioListener));
            created.Add(listener);
        }

        AudioDirector CreateWithoutMixer(int spatialVoices = 8, int flatVoices = 4)
        {
            EnsureListener();
            testConfig = AudioDirectorConfig.Create(spatialVoices: spatialVoices, flatVoices: flatVoices);
            return AudioDirector.Create(testConfig, new AudioSettingsStore(settingsDirectory), new SeededRandom(1));
        }

        AudioDirector CreateWithProjectMixer()
        {
            EnsureListener();
            var config = Resources.Load<AudioDirectorConfig>(AudioDirectorConfig.ResourcePath);
            Assert.IsNotNull(config, "ProjectNeo/Setup/Audio Foundation (AUDIO-01) 메뉴로 만든 설정 에셋이 있어야 한다.");
            Assert.IsNotNull(config.Mixer);
            return AudioDirector.Create(config, new AudioSettingsStore(settingsDirectory), new SeededRandom(1));
        }

        AudioClip Tone(string clipName, float seconds)
        {
            const int rate = 44100;
            int samples = Mathf.Max(1, Mathf.RoundToInt(rate * seconds));
            var clip = AudioClip.Create(clipName, samples, 1, rate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = 0.05f * Mathf.Sin(2f * Mathf.PI * 440f * i / rate);
            clip.SetData(data, 0);
            created.Add(clip);
            return clip;
        }

        AudioCueDefinition Cue(AudioBus bus = AudioBus.Sfx, float seconds = 1f, float spatialBlend = 1f,
            float cooldown = 0f, int maxVoices = 0, int priority = AudioCueDefinition.DefaultPriority, float maxDistance = 60f)
        {
            var cue = AudioCueDefinition.Create(new[] { Tone("tone", seconds), Tone("tone2", seconds) }, bus,
                spatialBlend: spatialBlend, cooldownSeconds: cooldown, maxVoices: maxVoices, priority: priority, maxDistance: maxDistance);
            created.Add(cue);
            return cue;
        }

        static int SourceCount(AudioDirector director) => director.GetComponentsInChildren<AudioSource>(true).Length;

        [UnityTest]
        public IEnumerator Pool_ReusesFixedSourcesForManyShots()
        {
            AudioDirector director = CreateWithoutMixer(spatialVoices: 8, flatVoices: 4);
            int sources = SourceCount(director);
            Assert.AreEqual(8 + 4 + 3, sources, "3D 자리 + 2D 자리 + 배경음 2 + 알림 1");
            AudioCueDefinition cue = Cue();
            int childCount = director.transform.childCount;

            for (int i = 0; i < 40; i++) director.PlayAt(cue, new Vector3(i % 5, 0f, i % 7));
            yield return null;

            Assert.AreEqual(sources, SourceCount(director), "소리를 많이 내도 AudioSource를 새로 만들지 않는다.");
            Assert.AreEqual(childCount, director.transform.childCount);
            Assert.LessOrEqual(director.ActiveSpatialVoices, director.SpatialVoiceCapacity);
            Assert.AreEqual(0, director.ActiveFlatVoices, "3D 소리는 2D 자리를 쓰지 않는다.");
        }

        [UnityTest]
        public IEnumerator Pool_ReclaimsFinishedVoices()
        {
            AudioDirector director = CreateWithoutMixer();
            AudioCueDefinition cue = Cue(AudioBus.Ui, seconds: 0.05f, spatialBlend: 0f);
            Assert.IsTrue(director.Play(cue));
            Assert.AreEqual(1, director.ActiveFlatVoices);
            float end = Time.realtimeSinceStartup + 2f;
            while (director.ActiveFlatVoices > 0 && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(0, director.ActiveFlatVoices, "끝난 소리의 자리는 돌려받는다.");
        }

        [UnityTest]
        public IEnumerator Play_RespectsCooldownCapAndDistance()
        {
            AudioDirector director = CreateWithoutMixer(spatialVoices: 16);
            AudioCueDefinition slow = Cue(cooldown: 5f);
            Assert.IsTrue(director.PlayAt(slow, Vector3.zero));
            Assert.IsFalse(director.PlayAt(slow, Vector3.zero), "대기 시간 안에는 다시 내지 않는다.");

            AudioCueDefinition capped = Cue(maxVoices: 2);
            for (int i = 0; i < 5; i++) Assert.IsTrue(director.PlayAt(capped, Vector3.one * i));
            Assert.AreEqual(2, director.ActiveVoicesOf(capped), "같은 소리 상한을 넘지 않는다.");

            AudioCueDefinition near = Cue(maxDistance: 30f);
            Assert.IsFalse(director.PlayAt(near, new Vector3(0f, 0f, 500f)), "최대 거리 밖은 내지 않는다.");
            Assert.AreEqual(0, director.ActiveVoicesOf(near));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Play_FullPoolStealsLowPriorityOnly()
        {
            AudioDirector director = CreateWithoutMixer(spatialVoices: 2);
            AudioCueDefinition low = Cue(priority: 10);
            AudioCueDefinition mid = Cue(priority: 100);
            AudioCueDefinition high = Cue(priority: 200);
            Assert.IsTrue(director.PlayAt(low, Vector3.zero));
            Assert.IsTrue(director.PlayAt(mid, Vector3.zero));
            Assert.IsTrue(director.PlayAt(high, Vector3.zero), "가득 차면 덜 중요한 소리를 끊는다.");
            Assert.AreEqual(0, director.ActiveVoicesOf(low));
            Assert.IsFalse(director.PlayAt(low, Vector3.zero), "더 덜 중요한 소리는 버린다.");
            Assert.AreEqual(1, director.ActiveVoicesOf(mid));
            Assert.AreEqual(1, director.ActiveVoicesOf(high));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingCueOrClips_AreSilentNoOps()
        {
            AudioDirector director = CreateWithoutMixer();
            var empty = AudioCueDefinition.Create(new AudioClip[] { null });
            created.Add(empty);
            Assert.IsFalse(director.Play(null));
            Assert.IsFalse(director.PlayAt(null, Vector3.zero));
            Assert.IsFalse(director.Play(empty));
            Assert.IsFalse(director.PlayAt(empty, Vector3.zero));
            Assert.IsFalse(director.PlayBgm(null));
            Assert.IsFalse(director.PlayBgm(empty));
            Assert.IsFalse(director.PlayStinger(null));
            director.StopBgm(0f);
            director.StopAllSfx();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Bgm_CrossfadesAndFadesOut()
        {
            AudioDirector director = CreateWithoutMixer();
            AudioCueDefinition first = Cue(AudioBus.Bgm, seconds: 2f, spatialBlend: 0f);
            AudioCueDefinition second = Cue(AudioBus.Bgm, seconds: 2f, spatialBlend: 0f);

            Assert.IsTrue(director.PlayBgm(first, 0f));
            Assert.AreEqual(first, director.CurrentBgm);
            Assert.AreEqual(1f, director.BgmChannelVolume(0));

            Assert.IsTrue(director.PlayBgm(second, 0.4f));
            Assert.AreEqual(second, director.CurrentBgm);
            yield return WaitRealtime(0.15f);
            Assert.Greater(director.BgmChannelVolume(0), 0f, "앞 곡은 서서히 작아진다.");
            Assert.Less(director.BgmChannelVolume(0), 1f);
            Assert.Greater(director.BgmChannelVolume(1), 0f, "새 곡은 서서히 커진다.");
            Assert.IsTrue(director.BgmSource(0).isPlaying && director.BgmSource(1).isPlaying, "전환 중에는 두 곡이 겹친다.");

            yield return WaitRealtime(0.5f);
            Assert.AreEqual(1f, director.BgmChannelVolume(1));
            Assert.IsFalse(director.BgmSource(0).isPlaying, "다 작아진 앞 곡은 멈춘다.");
            Assert.IsNull(director.BgmSource(0).clip);

            Assert.IsTrue(director.PlayBgm(second, 0.4f), "같은 곡은 다시 시작하지 않는다.");
            Assert.IsFalse(director.IsBgmFading);

            director.StopBgm(0.2f);
            Assert.IsNull(director.CurrentBgm);
            yield return WaitRealtime(0.4f);
            Assert.IsFalse(director.BgmSource(0).isPlaying || director.BgmSource(1).isPlaying);
        }

        [UnityTest]
        public IEnumerator Mixer_RoutesBusesAndAppliesVolumes()
        {
            AudioDirector director = CreateWithProjectMixer();
            AudioDirectorConfig config = director.Config;
            yield return null;

            AudioCueDefinition sfx = Cue(AudioBus.Sfx);
            AudioCueDefinition ui = Cue(AudioBus.Ui, spatialBlend: 0f);
            AudioCueDefinition music = Cue(AudioBus.Bgm, seconds: 2f, spatialBlend: 0f);
            Assert.IsTrue(director.PlayAt(sfx, Vector3.zero));
            Assert.IsTrue(director.Play(ui));
            Assert.IsTrue(director.PlayBgm(music, 0f));

            AudioMixerGroup sfxGroup = config.GroupFor(AudioBus.Sfx), uiGroup = config.GroupFor(AudioBus.Ui);
            int sfxSources = 0, uiSources = 0;
            foreach (AudioSource source in director.GetComponentsInChildren<AudioSource>())
            {
                if (!source.isPlaying) continue;
                if (source.clip != null && source.outputAudioMixerGroup == sfxGroup) sfxSources++;
                if (source.clip != null && source.outputAudioMixerGroup == uiGroup) uiSources++;
            }
            Assert.AreEqual(1, sfxSources);
            Assert.AreEqual(1, uiSources);
            Assert.AreEqual(config.GroupFor(AudioBus.Bgm), director.BgmSource(0).outputAudioMixerGroup);

            director.SetVolume(AudioBus.Sfx, 0.5f);
            Assert.IsTrue(director.TryGetMixerDecibels(AudioBus.Sfx, out float sfxDb));
            Assert.AreEqual(-6.0206f, sfxDb, 0.01f);
            director.SetMuted(AudioBus.Bgm, true);
            Assert.IsTrue(director.TryGetMixerDecibels(AudioBus.Bgm, out float bgmDb));
            Assert.AreEqual(AudioVolumeMath.SilentDecibels, bgmDb, 0.01f);
            Assert.AreEqual(1f, director.BgmSource(0).volume, 0.0001f, "믹서가 있으면 음량 설정을 소리 크기에 곱하지 않는다.");

            Assert.IsTrue(director.SaveSettings());
            AudioSettingsData saved = new AudioSettingsStore(settingsDirectory).Load();
            Assert.AreEqual(0.5f, saved.GetVolume(AudioBus.Sfx), 0.0001f);
            Assert.IsTrue(saved.IsMuted(AudioBus.Bgm));
        }

        [UnityTest]
        public IEnumerator NoMixer_AppliesSettingsToSourceVolume()
        {
            AudioDirector director = CreateWithoutMixer();
            AudioCueDefinition music = Cue(AudioBus.Bgm, seconds: 2f, spatialBlend: 0f);
            Assert.IsTrue(director.PlayBgm(music, 0f));
            director.SetVolume(AudioBus.Master, 0.5f);
            director.SetVolume(AudioBus.Bgm, 0.5f);
            Assert.AreEqual(0.25f, director.BgmSource(0).volume, 0.0001f);
            director.SetMuted(AudioBus.Bgm, true);
            Assert.AreEqual(0f, director.BgmSource(0).volume);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneTransitions_KeepOneDirectorAndNoLeakedSources()
        {
            loadedScenes = true;
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
            AudioDirector director = CreateWithProjectMixer();
            AudioCueDefinition music = Cue(AudioBus.Bgm, seconds: 2f, spatialBlend: 0f);
            Assert.IsTrue(director.PlayBgm(music, 0f));
            int sources = SourceCount(director);

            foreach (string sceneName in new[] { "Game", "Title" })
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                yield return null;
                Assert.IsTrue(director != null, $"{sceneName}: 씬이 바뀌어도 Director는 남는다.");
                Assert.AreSame(director, AudioDirector.Instance);
                Assert.AreEqual(1, Object.FindObjectsByType<AudioDirector>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length,
                    $"{sceneName}: Director는 AudioListener를 더하지 않는다.");
                Assert.AreEqual(sources, SourceCount(director));
                Assert.AreEqual(music, director.CurrentBgm, "씬 전환 중에도 배경음이 이어진다.");
                Assert.IsTrue(director.BgmSource(0).isPlaying);
            }
        }

        static IEnumerator WaitRealtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
