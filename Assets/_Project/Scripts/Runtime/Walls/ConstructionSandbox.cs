using ProjectNeo.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Walls
{
    /// <summary>배치 시험 씬 전용. 정식 부대 지휘와 전투 모드는 B-5 이후 연결한다.</summary>
    public sealed class ConstructionSandbox : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] CharacterActor unit;
        public CharacterActor Unit => unit;
        public string Message { get; private set; } = "성벽과 계단을 배치해 보세요.";
        public void Configure(ConstructionWorld owner, CharacterActor testUnit) { world = owner; unit = testUnit; }
        void Start() => unit.gameObject.SetActive(true);

        public void WalkToWall()
        {
            if (world.IsRebuilding || world.RebuildPending) { Message = "지형을 반영하고 있습니다. 잠시 뒤 다시 눌러 주세요."; return; }
            var agent = unit.GetComponent<NavMeshAgent>();
            if (!agent.enabled || !agent.isOnNavMesh) { Message = "유닛이 착지한 뒤 다시 눌러 주세요."; return; }
            var path = new NavMeshPath();
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure.Placement.Definition.Kind != BuildPieceKind.Wall) continue;
                Vector3 top = structure.transform.position + Vector3.up * structure.Placement.Definition.Height;
                if (!NavMesh.SamplePosition(top, out var hit, 0.5f, NavMesh.AllAreas)
                    || Mathf.Abs(hit.position.y - top.y) > 0.5f) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.SetPath(path);
                agent.isStopped = false;
                Message = "유닛이 계단을 통해 성벽 위로 이동합니다.";
                return;
            }
            Message = "걸어 올라갈 성벽이 없습니다. 계단의 높은 쪽을 성벽에 연결하세요.";
        }

        public void CollapseUnderUnit()
        {
            if (unit.Health.IsDead) { Message = "시험 유닛이 사망했습니다. Play를 다시 시작하면 초기화됩니다."; return; }
            PlacedStructure support = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(unit.transform.position + Vector3.up * 0.4f,
                Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<PlacedStructure>();
                if (candidate == null || hit.distance >= nearest) continue;
                support = candidate; nearest = hit.distance;
            }
            if (support == null) { Message = "유닛을 먼저 성벽이나 계단 위로 이동시키세요."; return; }
            support.ReceiveDamage(support.Health.Current);
            Message = "발밑 조각이 무너졌습니다. 낙하와 피해를 확인하세요.";
        }
    }
}
