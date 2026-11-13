using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>후보 목록에서 공격할 성벽의 인덱스를 선택한다. 선택하지 않으면 -1.</summary>
    public abstract class WallTargetStrategy : ScriptableObject
    {
        public abstract int Select(IReadOnlyList<WallTargetCandidate> candidates);
    }
}
