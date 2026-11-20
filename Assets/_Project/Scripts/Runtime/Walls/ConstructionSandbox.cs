using ProjectNeo.Combat;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Walls
{
    /// <summary>배치 시험 씬 전용. 정식 부대 지휘와 전투 모드는 B-5 이후 연결한다.</summary>
    public sealed class ConstructionSandbox : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] CharacterActor unit;
        [SerializeField] SquadCommandCenter commandCenter;
        public CharacterActor Unit => unit;
        public string Message { get; private set; } = "성벽과 계단을 지어 보세요.";
        public void Configure(ConstructionWorld owner, CharacterActor testUnit) { world = owner; unit = testUnit; }
        public void ConfigureCommands(SquadCommandCenter center) => commandCenter = center;
        void Start() => unit.gameObject.SetActive(true);

        public void WalkToWall()
        {
            if (world.IsRebuilding || world.RebuildPending) { Message = "지형을 갱신하는 중입니다. 잠시 후 다시 시도하세요."; return; }
            var agent = unit.GetComponent<NavMeshAgent>();
            if (!agent.enabled || !agent.isOnNavMesh) { Message = "유닛이 내려앉을 때까지 기다린 뒤 다시 시도하세요."; return; }
            var path = new NavMeshPath();
            foreach (PlacedStructure structure in world.Structures)
            {
                if (structure.Placement.Definition.Kind != BuildPieceKind.Wall) continue;
                Vector3 top = structure.transform.position + Vector3.up * structure.Placement.Definition.Height;
                if (!NavMesh.SamplePosition(top, out var hit, 0.5f, NavMesh.AllAreas)
                    || Mathf.Abs(hit.position.y - top.y) > 0.5f) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (commandCenter != null)
                {
                    commandCenter.Commander.Select(0);
                    commandCenter.Commander.Issue(new SquadOrder(SquadOrderKind.Move, hit.position));
                }
                else
                {
                    agent.SetPath(path);
                    agent.isStopped = false;
                }
                Message = "유닛이 계단을 올라 성벽 위로 가고 있습니다.";
                return;
            }
            Message = "갈 수 있는 성벽이 없습니다. 계단 꼭대기를 성벽과 이으세요.";
        }

        public void CollapseUnderWeapon()
        {
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                string name = weapon.Definition.DisplayName;
                weapon.Support.ReceiveDamage(weapon.Support.Health.Current);
                Message = name + "이(가) 받치던 성벽이 무너져 파괴되었습니다.";
                return;
            }
            Message = "먼저 성벽 위에 공성 무기를 놓으세요.";
        }

        public void CollapseUnderUnit()
        {
            if (unit.Health.IsDead) { Message = "시험 유닛이 죽었습니다. 플레이 모드를 다시 시작하세요."; return; }
            PlacedStructure support = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(unit.transform.position + Vector3.up * 0.4f,
                Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<PlacedStructure>();
                if (candidate == null || hit.distance >= nearest) continue;
                support = candidate; nearest = hit.distance;
            }
            if (support == null) { Message = "먼저 유닛을 성벽이나 계단 위로 옮기세요."; return; }
            support.ReceiveDamage(support.Health.Current);
            Message = "받침 조각이 무너졌습니다. 낙하와 피해를 확인하세요.";
        }
    }
}
