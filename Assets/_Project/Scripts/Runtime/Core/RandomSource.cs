namespace ProjectNeo.Core
{
    /// <summary>게임의 모든 랜덤이 지나가는 통로. 같은 시드면 같은 결과가 나온다.</summary>
    public interface IRandomSource
    {
        /// <summary>0 이상 maxExclusive 미만의 정수.</summary>
        int NextInt(int maxExclusive);

        /// <summary>0 이상 1 미만의 실수.</summary>
        float NextFloat();
    }

    public sealed class SeededRandom : IRandomSource
    {
        readonly System.Random random;

        public SeededRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public int NextInt(int maxExclusive) => random.Next(maxExclusive);

        public float NextFloat() => (float)random.NextDouble();
    }
}
