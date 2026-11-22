using System.Collections.Generic;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 대규모 전투(PERF-ai-query-budget)에서 판단 주기(Tick)를 여러 프레임으로 나누고,
    /// 한 프레임에 실행하는 동기 길찾기(NavMesh.CalculatePath) 횟수를 제한하는 순수 규칙.
    /// </summary>
    public sealed class AIDecisionScheduler
    {
        int currentFrame = -1;
        int queriesThisFrame;
        int totalQueries;

        /// <summary>이번 프레임에 소비한 길찾기 토큰 수.</summary>
        public int QueriesThisFrame => queriesThisFrame;

        /// <summary>누적 길찾기 토큰 소비 수(벤치마크·검증용).</summary>
        public int TotalQueries => totalQueries;

        /// <summary>
        /// 동시에 생성되거나 부대 지시를 받은 유닛들이 같은 프레임에 모두 길찾기를 부르지 않도록
        /// 유닛 순번(<paramref name="unitIndex"/>)을 <paramref name="bucketCount"/>개의 버킷으로 나눈 초기 대기 시간(초)을 돌려준다.
        /// 0번 버킷은 지연 없이(0초) 바로 첫 판단을 수행한다.
        /// </summary>
        public static float InitialOffset(int unitIndex, int bucketCount, float interval)
        {
            if (bucketCount <= 1 || interval <= 0f || unitIndex <= 0) return 0f;
            int bucket = unitIndex % bucketCount;
            return bucket * (interval / bucketCount);
        }

        /// <summary>새 프레임이 시작될 때 프레임당 카운터를 초기화한다. 같은 프레임 번호면 유지한다.</summary>
        public void BeginFrame(int frameCount)
        {
            if (currentFrame == frameCount) return;
            currentFrame = frameCount;
            queriesThisFrame = 0;
        }

        /// <summary>누적 통계를 비운다(벤치마크·테스트 초기화용).</summary>
        public void ResetStats()
        {
            currentFrame = -1;
            queriesThisFrame = 0;
            totalQueries = 0;
            UnitCombatAI.ResetStaggerCounter();
        }

        /// <summary>
        /// 이번 프레임의 길찾기 예산이 남아 있는지 확인한다(토큰은 소비하지 않는다).
        /// <paramref name="maxPerFrame"/>이 0 이하면 제한 없음으로 보고 언제나 true를 돌려준다.
        /// </summary>
        public bool HasPathBudget(int frameCount, int maxPerFrame)
        {
            BeginFrame(frameCount);
            return maxPerFrame <= 0 || queriesThisFrame < maxPerFrame;
        }

        /// <summary>
        /// 길찾기 토큰 하나를 소비한다. 예산 안이면 카운터를 올리고 true, 예산을 넘겼으면 false를 돌려준다.
        /// <paramref name="maxPerFrame"/>이 0 이하면 제한 없이 카운터만 기록하고 true를 돌려준다.
        /// </summary>
        public bool TryConsumePathToken(int frameCount, int maxPerFrame)
        {
            BeginFrame(frameCount);
            if (maxPerFrame > 0 && queriesThisFrame >= maxPerFrame) return false;
            queriesThisFrame++;
            totalQueries++;
            return true;
        }

        /// <summary>실제로 실행된 NavMesh.CalculatePath 호출 1회를 기록한다.</summary>
        public void RecordPathQuery(int frameCount)
        {
            BeginFrame(frameCount);
            queriesThisFrame++;
            totalQueries++;
        }
    }

    /// <summary>
    /// 전체 적 후보를 훑을 때 길찾기(CalculatePath)를 바로 부르지 않고,
    /// 점수가 가장 낮은(우선순위가 가장 높은) 상위 K개 후보만 오름차순으로 추리는 무할당 버퍼(PERF-ai-query-budget).
    /// </summary>
    public sealed class TargetCandidateBuffer<T>
    {
        readonly List<T> items;
        readonly List<float> scores;
        int capacity;

        public TargetCandidateBuffer(int capacity = 4)
        {
            this.capacity = capacity > 1 ? capacity : 1;
            items = new List<T>(this.capacity);
            scores = new List<float>(this.capacity);
        }

        public int Count => items.Count;
        public int Capacity => capacity;

        public T this[int index] => items[index];
        public float ScoreAt(int index) => scores[index];

        /// <summary>버퍼를 비우고 이번 탐색에서 유지할 최대 후보 수(<paramref name="maxCount"/>)를 정한다.</summary>
        public void Reset(int maxCount)
        {
            capacity = maxCount > 1 ? maxCount : 1;
            items.Clear();
            scores.Clear();
        }

        /// <summary>
        /// 지금 버퍼가 꽉 찼을 때 새 후보가 들어갈 수 있는 점수 상한.
        /// 버퍼가 아직 차지 않았으면 <see cref="float.PositiveInfinity"/>를 돌려준다.
        /// </summary>
        public float WorstScore => items.Count < capacity ? float.PositiveInfinity : scores[items.Count - 1];

        /// <summary>
        /// 후보 하나를 점수 오름차순으로 넣는다. 버퍼가 꽉 찼고 가장 나쁜 후보보다 점수가 크거나 같으면 무시한다.
        /// 동점이면 먼저 들어온 후보 순서를 지킨다.
        /// </summary>
        public bool TryAdd(T candidate, float score)
        {
            if (float.IsNaN(score) || float.IsPositiveInfinity(score)) return false;
            if (items.Count >= capacity && score >= scores[items.Count - 1]) return false;

            int insertAt = items.Count;
            while (insertAt > 0 && score < scores[insertAt - 1])
                insertAt--;

            if (items.Count == capacity)
            {
                items.RemoveAt(capacity - 1);
                scores.RemoveAt(capacity - 1);
            }
            items.Insert(insertAt, candidate);
            scores.Insert(insertAt, score);
            return true;
        }
    }
}
