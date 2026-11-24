using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대포 자동 사격 후보 한 명(SIEGE-33). 위치와 위협 종류를 담는다.
    /// Excluded면 이번 탐색에서 겨눌 수 없다(포물선이 막힘 등). 그래도 다른 후보의 무리 점수에는 센다.
    /// </summary>
    public readonly struct CannonTargetCandidate
    {
        public Vector3 Position { get; }
        public TargetThreat Threat { get; }
        public bool Excluded { get; }

        public CannonTargetCandidate(Vector3 position, TargetThreat threat = TargetThreat.None, bool excluded = false)
        {
            Position = position;
            Threat = threat;
            Excluded = excluded;
        }

        public CannonTargetCandidate WithExcluded() => new CannonTargetCandidate(Position, Threat, true);
    }
}
