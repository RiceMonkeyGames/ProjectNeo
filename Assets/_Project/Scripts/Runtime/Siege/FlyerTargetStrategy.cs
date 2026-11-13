using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>비행 적이 노릴 아군을 고른다. 후보 인덱스를 돌려주고, 고르지 않으면 -1.</summary>
    public abstract class FlyerTargetStrategy : ScriptableObject
    {
        public abstract int Select(IReadOnlyList<FlyerTargetCandidate> candidates);
    }
}
