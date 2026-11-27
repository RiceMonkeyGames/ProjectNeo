using NUnit.Framework;
using ProjectNeo.Audio;
using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class AudioPlaybackRulesTests
    {
        // ---------- 재사용 대기 ----------

        [Test]
        public void Cooldown_BlocksUntilReadyTime()
        {
            var gate = new CueCooldownGate();
            Assert.IsTrue(gate.TryPass(1, 10f, 0.5f));
            Assert.IsFalse(gate.TryPass(1, 10.4f, 0.5f));
            Assert.IsTrue(gate.TryPass(2, 10.4f, 0.5f), "다른 소리는 따로 센다.");
            Assert.IsTrue(gate.TryPass(1, 10.5f, 0.5f));
        }

        [Test]
        public void Cooldown_ZeroNeverBlocks()
        {
            var gate = new CueCooldownGate();
            for (int i = 0; i < 5; i++) Assert.IsTrue(gate.TryPass(1, 3f, 0f));
        }

        [Test]
        public void Cooldown_IsReadyDoesNotRecord()
        {
            var gate = new CueCooldownGate();
            Assert.IsTrue(gate.IsReady(1, 0f));
            Assert.IsTrue(gate.IsReady(1, 0f));
            gate.Mark(1, 0f, 1f);
            Assert.IsFalse(gate.IsReady(1, 0.99f));
            gate.Clear();
            Assert.IsTrue(gate.IsReady(1, 0.99f));
        }

        // ---------- 재생 자리 ----------

        [Test]
        public void Allocator_UsesFreeSlotsFirst()
        {
            var voices = new VoiceAllocator(3);
            Assert.AreEqual(0, voices.Allocate(1, 100, 0f, 0, out bool stolen0));
            Assert.AreEqual(1, voices.Allocate(1, 100, 0f, 0, out bool stolen1));
            Assert.IsFalse(stolen0 || stolen1);
            voices.Release(0);
            Assert.AreEqual(0, voices.Allocate(2, 100, 0f, 0, out _));
            Assert.AreEqual(2, voices.ActiveCount);
        }

        [Test]
        public void Allocator_PerCueCapReplacesOldestOfSameCue()
        {
            var voices = new VoiceAllocator(8);
            int first = voices.Allocate(7, 100, 1f, 2, out _);
            int second = voices.Allocate(7, 100, 1f, 2, out _);
            int third = voices.Allocate(7, 100, 1f, 2, out bool stolen);
            Assert.IsTrue(stolen);
            Assert.AreEqual(first, third, "같은 소리 상한에 닿으면 가장 오래된 것을 바꾼다.");
            Assert.AreEqual(2, voices.CountOf(7));
            int fourth = voices.Allocate(7, 100, 1f, 2, out _);
            Assert.AreEqual(second, fourth);
        }

        [Test]
        public void Allocator_FullPoolStealsLowestPriority()
        {
            var voices = new VoiceAllocator(3);
            voices.Allocate(1, 200, 1f, 0, out _);
            int low = voices.Allocate(2, 50, 1f, 0, out _);
            voices.Allocate(3, 120, 1f, 0, out _);
            int index = voices.Allocate(4, 100, 1f, 0, out bool stolen);
            Assert.IsTrue(stolen);
            Assert.AreEqual(low, index);
            Assert.AreEqual(4, voices.CueAt(index));
        }

        [Test]
        public void Allocator_FullPoolRejectsLessImportantSound()
        {
            var voices = new VoiceAllocator(2);
            voices.Allocate(1, 150, 1f, 0, out _);
            voices.Allocate(2, 150, 1f, 0, out _);
            Assert.AreEqual(-1, voices.Allocate(3, 100, 1f, 0, out bool stolen));
            Assert.IsFalse(stolen);
            Assert.AreEqual(1, voices.CountOf(1));
            Assert.AreEqual(1, voices.CountOf(2));
        }

        [Test]
        public void Allocator_SamePriorityStealsFarthestOnlyForCloserSound()
        {
            var voices = new VoiceAllocator(3);
            voices.Allocate(1, 100, 4f, 0, out _);
            int far = voices.Allocate(2, 100, 900f, 0, out _);
            voices.Allocate(3, 100, 25f, 0, out _);

            Assert.AreEqual(-1, voices.Allocate(4, 100, 1600f, 0, out _), "더 먼 같은 우선순위 소리는 버린다.");
            int index = voices.Allocate(5, 100, 100f, 0, out bool stolen);
            Assert.IsTrue(stolen);
            Assert.AreEqual(far, index);
        }

        [Test]
        public void Allocator_TieBreaksByOldest()
        {
            var voices = new VoiceAllocator(2);
            int oldest = voices.Allocate(1, 100, 9f, 0, out _);
            voices.Allocate(2, 100, 9f, 0, out _);
            Assert.AreEqual(oldest, voices.Allocate(3, 100, 9f, 0, out bool stolen));
            Assert.IsTrue(stolen);
        }

        [Test]
        public void Allocator_HundredRequestsNeverExceedCapacity()
        {
            var voices = new VoiceAllocator(16);
            var random = new SeededRandom(5);
            for (int i = 0; i < 100; i++)
                voices.Allocate(random.NextInt(6), random.NextInt(256), random.NextFloat() * 3600f, 4, out _);
            Assert.AreEqual(16, voices.ActiveCount);
            for (int cue = 0; cue < 6; cue++) Assert.LessOrEqual(voices.CountOf(cue), 4);
        }

        // ---------- 변형 ----------

        [Test]
        public void PickClipIndex_HandlesEmptyAndSingle()
        {
            var random = new SeededRandom(1);
            Assert.AreEqual(-1, AudioCueRules.PickClipIndex(0, -1, random));
            Assert.AreEqual(0, AudioCueRules.PickClipIndex(1, 0, random));
            Assert.AreEqual(0, AudioCueRules.PickClipIndex(4, 2, null));
        }

        [Test]
        public void PickClipIndex_NeverRepeatsPreviousAndCoversOthers()
        {
            var random = new SeededRandom(42);
            var seen = new bool[4];
            int previous = -1;
            for (int i = 0; i < 200; i++)
            {
                int index = AudioCueRules.PickClipIndex(4, previous, random);
                Assert.AreNotEqual(previous, index);
                Assert.That(index, Is.InRange(0, 3));
                seen[index] = true;
                previous = index;
            }
            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void PickClipIndex_IsDeterministicForSeed()
        {
            var a = new SeededRandom(9);
            var b = new SeededRandom(9);
            int pa = -1, pb = -1;
            for (int i = 0; i < 20; i++)
            {
                pa = AudioCueRules.PickClipIndex(5, pa, a);
                pb = AudioCueRules.PickClipIndex(5, pb, b);
                Assert.AreEqual(pa, pb);
            }
        }

        [Test]
        public void Jitter_StaysInsideRangeAndClamps()
        {
            var random = new SeededRandom(3);
            for (int i = 0; i < 100; i++)
            {
                float volume = AudioCueRules.JitterVolume(0.8f, 0.1f, random);
                Assert.That(volume, Is.InRange(0.7f, 0.9f));
                float pitch = AudioCueRules.JitterPitch(1f, 0.2f, random);
                Assert.That(pitch, Is.InRange(0.8f, 1.2f));
            }
            Assert.AreEqual(0.99f, AudioCueRules.JitterVolume(0.99f, 0f, random), "흔들림이 0이면 그대로.");
            Assert.That(AudioCueRules.JitterVolume(0.95f, 0.5f, new MaxRandom()), Is.LessThanOrEqualTo(1f));
            Assert.AreEqual(AudioCueRules.MinPitch, AudioCueRules.JitterPitch(0.1f, 0.5f, new MinRandom()));
        }

        // ---------- 배경음 전환 ----------

        [Test]
        public void Crossfade_FirstTrackFadesIn()
        {
            var fade = new BgmCrossfade();
            Assert.AreEqual(-1, fade.ActiveChannel);
            int channel = fade.CrossfadeToNew(2f);
            Assert.AreEqual(0, channel);
            fade.Tick(1f);
            Assert.AreEqual(0.5f, fade.VolumeOf(0), 0.0001f);
            fade.Tick(5f);
            Assert.AreEqual(1f, fade.VolumeOf(0));
            Assert.IsFalse(fade.IsFading);
        }

        [Test]
        public void Crossfade_SwapsChannelsOverDuration()
        {
            var fade = new BgmCrossfade();
            fade.CrossfadeToNew(0f);
            Assert.AreEqual(1f, fade.VolumeOf(0));
            int next = fade.CrossfadeToNew(1f);
            Assert.AreEqual(1, next);
            fade.Tick(0.25f);
            Assert.AreEqual(0.75f, fade.VolumeOf(0), 0.0001f);
            Assert.AreEqual(0.25f, fade.VolumeOf(1), 0.0001f);
            fade.Tick(1f);
            Assert.IsTrue(fade.IsSilent(0));
            Assert.AreEqual(1f, fade.VolumeOf(1));
            Assert.AreEqual(0, fade.CrossfadeToNew(1f), "다음 곡은 다시 0번 채널.");
        }

        [Test]
        public void Crossfade_FadeOutSilencesBoth()
        {
            var fade = new BgmCrossfade();
            fade.CrossfadeToNew(0f);
            fade.CrossfadeToNew(2f);
            fade.Tick(1f);
            fade.FadeOut(1f);
            Assert.AreEqual(-1, fade.ActiveChannel);
            fade.Tick(1f);
            Assert.IsTrue(fade.IsSilent(0) && fade.IsSilent(1));
        }

        [Test]
        public void Crossfade_ZeroDurationIsImmediate()
        {
            var fade = new BgmCrossfade();
            fade.CrossfadeToNew(0f);
            fade.CrossfadeToNew(0f);
            Assert.IsTrue(fade.IsSilent(0));
            Assert.AreEqual(1f, fade.VolumeOf(1));
            fade.FadeOut(0f);
            Assert.IsTrue(fade.IsSilent(1));
        }

        // ---------- 정의 에셋 ----------

        [Test]
        public void CueCreate_ClampsAndReportsClips()
        {
            var empty = AudioCueDefinition.Create(new AudioClip[] { null });
            Assert.IsFalse(empty.HasClips);
            var cue = AudioCueDefinition.Create(new AudioClip[0], AudioBus.Ui, volume: 2f, spatialBlend: 0f,
                minDistance: 10f, maxDistance: 5f, priority: 999, cooldownSeconds: -1f, maxVoices: -3);
            Assert.AreEqual(1f, cue.Volume);
            Assert.IsFalse(cue.IsSpatial);
            Assert.AreEqual(10f, cue.MaxDistance, "최대 거리는 최소 거리보다 작지 않다.");
            Assert.AreEqual(AudioCueDefinition.MaxPriority, cue.Priority);
            Assert.AreEqual(0f, cue.CooldownSeconds);
            Assert.AreEqual(0, cue.MaxVoices);
            Object.DestroyImmediate(empty);
            Object.DestroyImmediate(cue);
        }

        [Test]
        public void ConfigGroups_FallBackToMasterAndNamesAreUnique()
        {
            var config = AudioDirectorConfig.Create();
            Assert.IsNull(config.GroupFor(AudioBus.Sfx));
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (AudioBus bus in (AudioBus[])System.Enum.GetValues(typeof(AudioBus)))
                Assert.IsTrue(names.Add(AudioDirectorConfig.VolumeParameter(bus)), bus.ToString());
            Object.DestroyImmediate(config);
        }

        sealed class MaxRandom : IRandomSource
        {
            public int NextInt(int maxExclusive) => maxExclusive - 1;
            public float NextFloat() => 0.99999f;
        }

        sealed class MinRandom : IRandomSource
        {
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => 0f;
        }
    }
}
