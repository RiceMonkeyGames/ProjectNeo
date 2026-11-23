using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 배정 계산에 쓰는 수비병 한 명(SIEGE-31). <see cref="Post"/>는 지킬 자리(감지 반경의 기준),
    /// <see cref="Position"/>은 지금 위치(가까운 순서의 기준), <see cref="Current"/>는 지금 맡은 임무다.
    /// <see cref="ExcludedSites"/>는 길이 없어 거절된 사다리 번호의 비트(0~63번)다.
    /// </summary>
    public readonly struct LadderDefender
    {
        public readonly Vector3 Post;
        public readonly Vector3 Position;
        public readonly LadderDuty Current;
        public readonly ulong ExcludedSites;

        public LadderDefender(Vector3 post, Vector3 position, LadderDuty current, ulong excludedSites = 0UL)
        {
            Post = post;
            Position = position;
            Current = current;
            ExcludedSites = excludedSites;
        }

        public bool Excludes(int site) => site >= 0 && site < 64 && (ExcludedSites & (1UL << site)) != 0UL;
    }
}
