using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 캐릭터 모델의 Animator에 수평 이동 속도(<c>Speed</c>)만 넘기는 외형 전용 부품. 전투 판단은 하지 않는다.
    /// 길찾기 에이전트가 있으면 그 속도를, 없으면(비행·사다리 등) 위치 변화를 쓴다. 죽으면 자세를 멈춘다.
    /// </summary>
    public sealed class CharacterModelAnimator : MonoBehaviour
    {
        public const string SpeedParameter = "Speed";
        static readonly int Speed = Animator.StringToHash(SpeedParameter);
        const float SpeedDamping = 0.1f;

        [SerializeField] CharacterActor actor;
        [SerializeField] Animator animator;
        NavMeshAgent agent;
        Vector3 lastPosition;

        public void Configure(CharacterActor owner, Animator target)
        {
            actor = owner;
            animator = target;
        }

        void Awake()
        {
            agent = actor.GetComponent<NavMeshAgent>();
            animator.applyRootMotion = false;
        }

        void OnEnable()
        {
            lastPosition = actor.transform.position;
            actor.Died += OnDeath;
        }

        void OnDisable() => actor.Died -= OnDeath;

        void Update()
        {
            if (!animator.enabled) return;
            float deltaTime = Time.deltaTime;
            Vector3 position = actor.transform.position;
            Vector3 velocity = agent != null && agent.enabled && agent.isOnNavMesh
                ? agent.velocity
                : deltaTime > 0f ? (position - lastPosition) / deltaTime : Vector3.zero;
            lastPosition = position;
            velocity.y = 0f;
            animator.SetFloat(Speed, velocity.magnitude, SpeedDamping, deltaTime);
        }

        // 사망 애니메이션이 없어 현재 자세로 멈추고, 눕히기는 PlaceholderCombatView가 맡는다.
        void OnDeath() => animator.enabled = false;
    }
}
