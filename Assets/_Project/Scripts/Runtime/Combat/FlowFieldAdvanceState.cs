using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 원거리(LOD 2) 지상 유닛이 공유 Flow Field로 나아가는 중인지와 그 방향(PERF-ai-flow-field)을 담는 순수 부품
    /// (PERF-ai-alloc-and-split). 멈출 때 방향까지 함께 비워 다시 켜진 유닛이 지난 방향으로 미끄러지지 않게 한다.
    /// </summary>
    public sealed class FlowFieldAdvanceState
    {
        public bool IsActive { get; private set; }
        public Vector3 Direction { get; private set; }

        public void Begin(Vector3 direction)
        {
            IsActive = true;
            Direction = direction;
        }

        public void Stop()
        {
            IsActive = false;
            Direction = Vector3.zero;
        }
    }
}
