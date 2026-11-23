using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 배정 계산에 쓰는 세워진 사다리 하나(SIEGE-31). 쓸 수 있는(NavMesh 위) 밀기 자리와 길목 자리만 담는다.
    /// 매 계산마다 새로 만들지 않도록 다시 채워 쓴다.
    /// </summary>
    public sealed class LadderDefenseSite
    {
        public Vector3 Top;
        public bool NeedsChokepoint;
        public readonly List<Vector3> PushSpots = new List<Vector3>();
        public readonly List<Vector3> ChokeSpots = new List<Vector3>();

        public void Reset(Vector3 top, bool needsChokepoint)
        {
            Top = top;
            NeedsChokepoint = needsChokepoint;
            PushSpots.Clear();
            ChokeSpots.Clear();
        }
    }
}
