using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 자동 사격 목표 고르기 전략. 후보 위치 목록에서 쏠 대상의 순번을 고르고, 없으면 -1.
    /// 가장 가까운 적 외의 방식(가장 뭉친 곳, 성문·사다리 우선 등)은 이 인터페이스로 추가한다.
    /// </summary>
    public interface ICannonTargetSelector
    {
        int Select(in CannonAimContext context, IReadOnlyList<Vector3> candidates);
    }
}
