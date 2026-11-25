using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Town
{
    /// <summary>
    /// 성 안 주민 한 명(SIEGE-36): 시설 NPC는 입구 옆에 서 있고, 시민은 산책 지점을 걸어 다닌다.
    /// 목적지·쉬는 시간은 <see cref="TownDirector"/>가 정하고, 이 컴포넌트는 길찾기 몸과 걷기 애니메이션만 맡는다.
    /// 모델과 Animator는 Visual 자식에 있다. 길찾기 몸은 NavMesh가 준비된 뒤에 켠다.
    /// </summary>
    public sealed class TownResident : MonoBehaviour
    {
        public const string SpeedParameter = "Speed";
        static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

        [SerializeField] NavMeshAgent agent;
        [SerializeField] Animator animator;
        [Tooltip("시설 NPC면 제자리에 서 있고 산책하지 않는다.")]
        [SerializeField] bool stationNpc;
        [SerializeField] Vector3 homePosition;
        [SerializeField] float homeYaw;

        public TownResidentState State { get; private set; } = TownResidentState.Hidden;
        public bool IsStationNpc => stationNpc;
        public Vector3 HomePosition => homePosition;
        /// <summary>지금 가고 있는 산책 지점 번호(없으면 -1).</summary>
        public int Waypoint { get; set; } = -1;
        /// <summary>쉬는 남은 시간 또는 달아나기 남은 시간(초).</summary>
        public float Timer { get; set; }
        public NavMeshAgent Agent => agent;

        public void Configure(NavMeshAgent body, Animator model, bool isStationNpc, Vector3 home, float yaw)
        {
            agent = body; animator = model; stationNpc = isStationNpc; homePosition = home; homeYaw = yaw;
        }

        /// <summary>제자리에 다시 나타난다. NavMesh 위에 놓지 못하면 false(다음 프레임에 다시 시도한다).</summary>
        public bool ShowAtHome(float idleSeconds)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(homePosition, Quaternion.Euler(0f, homeYaw, 0f));
            if (!NavMesh.SamplePosition(homePosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                if (agent != null) agent.enabled = false;
                State = stationNpc ? TownResidentState.Standing : TownResidentState.Idle;
                Timer = idleSeconds;
                return false;
            }
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                agent.isStopped = true;
                // 제자리 방향은 길찾기 몸이 돌리지 않게 한다(서 있는 NPC가 입구 바깥을 본다).
                transform.rotation = Quaternion.Euler(0f, homeYaw, 0f);
            }
            State = stationNpc ? TownResidentState.Standing : TownResidentState.Idle;
            Waypoint = -1;
            Timer = idleSeconds;
            return true;
        }

        /// <summary>걷거나 달려 <paramref name="point"/>로 간다. 길찾기 몸이 없으면 false.</summary>
        public bool MoveTo(Vector3 point, float speed, TownResidentState next)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            agent.speed = speed;
            agent.isStopped = false;
            if (!agent.SetDestination(point)) return false;
            State = next;
            return true;
        }

        public void Rest(float seconds)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            State = TownResidentState.Idle;
            Timer = seconds;
        }

        public bool HasArrived(float distance) => agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending
            && agent.remainingDistance <= distance;

        /// <summary>건물에 숨는다(보이지 않음).</summary>
        public void Hide()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            State = TownResidentState.Hidden;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (animator == null) return;
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedHash, speed);
        }
    }
}
