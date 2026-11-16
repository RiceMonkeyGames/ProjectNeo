using System;
using System.Collections.Generic;

namespace ProjectNeo.Stages
{
    /// <summary>시간표가 아는 웨이브 하나: 언제 시작하고 몇 명이 나오는지. 프리팹·씬은 모른다.</summary>
    public readonly struct WaveTiming
    {
        public float StartSeconds { get; }
        public int UnitCount { get; }
        public WaveTiming(float startSeconds, int unitCount)
        {
            StartSeconds = Math.Max(0f, startSeconds);
            UnitCount = Math.Max(0, unitCount);
        }
    }

    /// <summary>웨이브 시작 규칙이 판단에 쓰는 값.</summary>
    public readonly struct WaveStartContext
    {
        public int WaveIndex { get; }
        public float StartSeconds { get; }
        /// <summary>전투가 실제로 진행된 시간(초).</summary>
        public float ElapsedSeconds { get; }
        /// <summary>이미 나와서 살아 있는 적 수.</summary>
        public int EnemiesOnField { get; }
        public WaveStartContext(int waveIndex, float startSeconds, float elapsedSeconds, int enemiesOnField)
        {
            WaveIndex = waveIndex; StartSeconds = startSeconds; ElapsedSeconds = elapsedSeconds; EnemiesOnField = enemiesOnField;
        }
    }

    /// <summary>웨이브를 지금 내보낼지 정하는 규칙. 시간표와 분리해 두어 규칙만 바꿀 수 있다.</summary>
    public interface IWaveStartRule
    {
        bool IsDue(in WaveStartContext context);
    }

    /// <summary>정해진 시간이 되면 시작한다(Q-51·Q-52 기본 규칙). 전장이 비어도 당겨오지 않는다.</summary>
    public sealed class TimedWaveStartRule : IWaveStartRule
    {
        public static readonly TimedWaveStartRule Instance = new TimedWaveStartRule();
        public bool IsDue(in WaveStartContext context) => context.ElapsedSeconds >= context.StartSeconds;
    }

    /// <summary>
    /// 한 전투의 웨이브 시간표. 웨이브는 목록 순서대로, 전투마다 정확히 한 번씩만 나온다.
    /// 시간은 Advance로 넘겨준 만큼만 흐르므로 멈춘 동안에는 진행하지 않는다.
    /// </summary>
    public sealed class WaveSchedule
    {
        readonly WaveTiming[] waves;
        readonly IWaveStartRule rule;

        public WaveSchedule(IReadOnlyList<WaveTiming> timings, IWaveStartRule startRule = null)
        {
            int count = timings != null ? timings.Count : 0;
            waves = new WaveTiming[count];
            for (int i = 0; i < count; i++) waves[i] = timings[i];
            rule = startRule ?? TimedWaveStartRule.Instance;
        }

        public int WaveCount => waves.Length;
        /// <summary>지금까지 나온 웨이브 수. 다음에 나올 웨이브의 번호(0부터)이기도 하다.</summary>
        public int ReleasedWaves { get; private set; }
        public int PendingWaves => waves.Length - ReleasedWaves;
        public bool AllReleased => ReleasedWaves >= waves.Length;
        public float ElapsedSeconds { get; private set; }

        /// <summary>아직 나오지 않은 적 수. 승리 판정에서 '나올 예정인 적'으로 쓴다.</summary>
        public int PendingUnits
        {
            get { int total = 0; for (int i = ReleasedWaves; i < waves.Length; i++) total += waves[i].UnitCount; return total; }
        }

        /// <summary>다음 웨이브의 정해진 시작 시간까지 남은 초. 남은 웨이브가 없으면 -1.</summary>
        public float SecondsUntilNext => AllReleased ? -1f : Math.Max(0f, waves[ReleasedWaves].StartSeconds - ElapsedSeconds);

        /// <summary>
        /// 시간을 흘리고, 이번에 새로 나올 웨이브 번호를 released에 순서대로 담는다. 돌려주는 값은 그 수다.
        /// 음수 시간은 무시한다. 한 번에 긴 시간이 흘러도 각 웨이브는 한 번만 나온다.
        /// </summary>
        public int Advance(float deltaSeconds, int enemiesOnField, List<int> released)
        {
            if (deltaSeconds > 0f) ElapsedSeconds += deltaSeconds;
            int count = 0;
            while (ReleasedWaves < waves.Length)
            {
                var context = new WaveStartContext(ReleasedWaves, waves[ReleasedWaves].StartSeconds, ElapsedSeconds, enemiesOnField);
                if (!rule.IsDue(in context)) break;
                released?.Add(ReleasedWaves);
                // 방금 나온 적도 전장에 있는 것으로 보고 다음 웨이브를 판단한다.
                enemiesOnField += waves[ReleasedWaves].UnitCount;
                ReleasedWaves++;
                count++;
            }
            return count;
        }

        /// <summary>새 전투를 위해 처음으로 되돌린다.</summary>
        public void Reset()
        {
            ElapsedSeconds = 0f;
            ReleasedWaves = 0;
        }

        /// <summary>
        /// 공성 수단을 잃은 적의 후퇴(Q-30)를 지금 판정해도 되는지(Q-52). 나올 웨이브가 남아 있으면
        /// 설정으로 허용한 경우에만 판정한다.
        /// </summary>
        public static bool RetreatAllowed(int pendingWaves, bool retreatWhileWavesPending) =>
            pendingWaves <= 0 || retreatWhileWavesPending;
    }
}
