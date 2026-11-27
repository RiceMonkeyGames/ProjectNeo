using System.IO;
using NUnit.Framework;
using ProjectNeo.Audio;

namespace ProjectNeo.Tests
{
    public class AudioSettingsTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ProjectNeoAudioSettingsTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0.01f, -40f)]
        public void LinearToDecibels_FollowsTwentyLog10(float linear, float expected)
        {
            Assert.AreEqual(expected, AudioVolumeMath.LinearToDecibels(linear), 0.001f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(0.00001f)]
        [TestCase(float.NaN)]
        public void LinearToDecibels_SilentInputsAreMinus80(float linear)
        {
            Assert.AreEqual(AudioVolumeMath.SilentDecibels, AudioVolumeMath.LinearToDecibels(linear));
        }

        [Test]
        public void LinearToDecibels_ClampsAboveOne()
        {
            Assert.AreEqual(0f, AudioVolumeMath.LinearToDecibels(3f));
        }

        [TestCase(0.75f)]
        [TestCase(0.3f)]
        [TestCase(0.02f)]
        public void DecibelsToLinear_RoundTrips(float linear)
        {
            Assert.AreEqual(linear, AudioVolumeMath.DecibelsToLinear(AudioVolumeMath.LinearToDecibels(linear)), 0.0001f);
        }

        [Test]
        public void DecibelsToLinear_ClampsEnds()
        {
            Assert.AreEqual(0f, AudioVolumeMath.DecibelsToLinear(-80f));
            Assert.AreEqual(0f, AudioVolumeMath.DecibelsToLinear(-200f));
            Assert.AreEqual(1f, AudioVolumeMath.DecibelsToLinear(6f));
        }

        [Test]
        public void SetVolume_ClampsAndMuteSilencesBus()
        {
            var data = new AudioSettingsData();
            data.SetVolume(AudioBus.Sfx, 1.7f);
            data.SetVolume(AudioBus.Bgm, -0.2f);
            Assert.AreEqual(1f, data.GetVolume(AudioBus.Sfx));
            Assert.AreEqual(0f, data.GetVolume(AudioBus.Bgm));

            data.SetVolume(AudioBus.Ui, 0.5f);
            Assert.AreEqual(-6.0206f, data.Decibels(AudioBus.Ui), 0.001f);
            data.SetMuted(AudioBus.Ui, true);
            Assert.AreEqual(AudioVolumeMath.SilentDecibels, data.Decibels(AudioBus.Ui));
            Assert.IsTrue(data.IsMuted(AudioBus.Ui));
            Assert.AreEqual(0.5f, data.GetVolume(AudioBus.Ui), "음소거해도 슬라이더 값은 남는다.");
        }

        [Test]
        public void EffectiveLinear_MultipliesMasterAndRespectsMasterMute()
        {
            var data = new AudioSettingsData();
            data.SetVolume(AudioBus.Master, 0.5f);
            data.SetVolume(AudioBus.Sfx, 0.4f);
            Assert.AreEqual(0.2f, data.EffectiveLinear(AudioBus.Sfx), 0.0001f);
            Assert.AreEqual(0.5f, data.EffectiveLinear(AudioBus.Master), 0.0001f);
            data.SetMuted(AudioBus.Master, true);
            Assert.AreEqual(0f, data.EffectiveLinear(AudioBus.Sfx));
        }

        [Test]
        public void Json_RoundTripsEveryBus()
        {
            var data = new AudioSettingsData();
            data.SetVolume(AudioBus.Master, 0.9f);
            data.SetVolume(AudioBus.Bgm, 0.3f);
            data.SetVolume(AudioBus.Sfx, 0.6f);
            data.SetVolume(AudioBus.Ui, 0.2f);
            data.SetVolume(AudioBus.Voice, 0.1f);
            data.SetMuted(AudioBus.Bgm, true);
            data.SetMuted(AudioBus.Voice, true);

            AudioSettingsData loaded = AudioSettingsData.FromJson(data.ToJson());
            foreach (AudioBus bus in (AudioBus[])System.Enum.GetValues(typeof(AudioBus)))
            {
                Assert.AreEqual(data.GetVolume(bus), loaded.GetVolume(bus), 0.00001f, bus.ToString());
                Assert.AreEqual(data.IsMuted(bus), loaded.IsMuted(bus), bus.ToString());
            }
        }

        [Test]
        public void FromJson_SanitizesOutOfRangeValues()
        {
            AudioSettingsData loaded = AudioSettingsData.FromJson("{\"version\":1,\"masterVolume\":4.0,\"sfxVolume\":-2.0}");
            Assert.AreEqual(1f, loaded.GetVolume(AudioBus.Master));
            Assert.AreEqual(0f, loaded.GetVolume(AudioBus.Sfx));
            Assert.AreEqual(AudioSettingsData.DefaultVolume, loaded.GetVolume(AudioBus.Bgm), "빠진 값은 기본값이다.");
        }

        [Test]
        public void FromJson_RejectsEmptyOrBrokenText()
        {
            Assert.IsNull(AudioSettingsData.FromJson(""));
            Assert.IsNull(AudioSettingsData.FromJson("{not json"));
        }

        [Test]
        public void Store_MissingFileGivesDefaults()
        {
            var store = new AudioSettingsStore(directory);
            AudioSettingsData data = store.Load();
            Assert.AreEqual(AudioSettingsData.DefaultVolume, data.GetVolume(AudioBus.Master));
            Assert.AreEqual(string.Empty, store.LastError);
            Assert.IsFalse(Directory.Exists(directory), "읽기만 하면 폴더를 만들지 않는다.");
        }

        [Test]
        public void Store_SavesAndLoads()
        {
            var store = new AudioSettingsStore(directory);
            var data = new AudioSettingsData();
            data.SetVolume(AudioBus.Sfx, 0.25f);
            data.SetMuted(AudioBus.Bgm, true);
            Assert.IsTrue(store.Save(data));
            Assert.IsTrue(store.Save(data), "있는 파일 위에 다시 저장한다.");
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));

            AudioSettingsData loaded = new AudioSettingsStore(directory).Load();
            Assert.AreEqual(0.25f, loaded.GetVolume(AudioBus.Sfx), 0.00001f);
            Assert.IsTrue(loaded.IsMuted(AudioBus.Bgm));
        }

        [Test]
        public void Store_BrokenFileGivesDefaultsWithError()
        {
            Directory.CreateDirectory(directory);
            var store = new AudioSettingsStore(directory);
            File.WriteAllText(store.FilePath, "garbage{");
            AudioSettingsData data = store.Load();
            Assert.AreEqual(AudioSettingsData.DefaultVolume, data.GetVolume(AudioBus.Sfx));
            Assert.IsNotEmpty(store.LastError);
        }
    }
}
