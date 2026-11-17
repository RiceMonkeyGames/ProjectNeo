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
        public string Message { get; private set; } = "Try building walls and stairs.";
        public void Configure(ConstructionWorld owner, CharacterActor testUnit) { world = owner; unit = testUnit; }
        public void ConfigureCommands(SquadCommandCenter center) => commandCenter = center;
        void Start() => unit.gameObject.SetActive(true);

        public void WalkToWall()
        {
            if (world.IsRebuilding || world.RebuildPending) { Message = "Updating terrain. Try again shortly."; return; }
            var agent = unit.GetComponent<NavMeshAgent>();
            if (!agent.enabled || !agent.isOnNavMesh) { Message = "Wait for the unit to land, then try again."; return; }
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
                Message = "The unit is climbing the stairs onto the wall.";
                return;
            }
            Message = "No reachable wall. Connect the top of the stairs to a wall.";
        }

        public void CollapseUnderWeapon()
        {
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                string name = weapon.Definition.DisplayName;
                weapon.Support.ReceiveDamage(weapon.Support.Health.Current);
                Message = name + " was destroyed when its supporting wall collapsed.";
                return;
            }
            Message = "Place a siege weapon on a wall first.";
        }

        public void CollapseUnderUnit()
        {
            if (unit.Health.IsDead) { Message = "The test unit died. Restart Play Mode to reset."; return; }
            PlacedStructure support = null;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(unit.transform.position + Vector3.up * 0.4f,
                Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<PlacedStructure>();
                if (candidate == null || hit.distance >= nearest) continue;
                support = candidate; nearest = hit.distance;
            }
            if (support == null) { Message = "Move the unit onto a wall or stairs first."; return; }
            support.ReceiveDamage(support.Health.Current);
            Message = "The supporting piece collapsed. Check the fall and damage.";
        }
    }
}
