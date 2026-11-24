using System.Collections.Generic;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 자동 사격 목표 고르기 전략. 후보 목록에서 겨눌 대상의 순번을 고르고, 없으면 -1.
    /// Excluded 후보는 고르지 않는다. 기본은 무리·위협 점수(SIEGE-33), 가장 가까운 적만 고르는 전략도 남아 있다.
    /// </summary>
    public interface ICannonTargetSelector
    {
        int Select(in CannonAimContext context, IReadOnlyList<CannonTargetCandidate> candidates);
    }
}
