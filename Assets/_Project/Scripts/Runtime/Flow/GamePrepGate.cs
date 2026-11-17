using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 준비 단계 성문 열기(PREP-05). 성 밖에 남은 플레이어나 아군이 들어올 수 있도록 준비 중에는 성문 앞에서 F로
    /// 열고 닫는다. 전투가 시작되면 열어 둔 성문을 닫고, 문 자리에 서 있던 아군·플레이어를 성 안쪽으로 옮긴다.
    /// 부서진 성문은 이 도구와 상관없이 수리 전까지 열려 있다.
    /// </summary>
    public sealed class GamePrepGate : MonoBehaviour
    {
        ConstructionWorld world;
        GamePhaseView phaseView;
        Vector2Int outward;
        GameFlow flow;

        public GamePhaseView PhaseView => phaseView;
        public string Message { get; private set; } = "";

        public void Configure(ConstructionWorld castle, GamePhaseView view, Vector2Int outwardDirection)
        { world = castle; phaseView = view; outward = outwardDirection; }

        void Awake() => flow = GameSession.Flow;

        void Start()
        {
            if (world != null) world.Changed += EnsureStations;
            flow.PhaseChanged += OnPhaseChanged;
            EnsureStations();
        }

        void OnDestroy()
        {
            if (world != null) world.Changed -= EnsureStations;
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>문짝이 있는 성문마다 상호작용 자리를 붙인다. 이미 있으면 그대로 둔다.</summary>
        void EnsureStations()
        {
            if (world == null) return;
            foreach (PlacedStructure piece in world.Structures)
            {
                if (piece == null || piece.Placement == null || piece.Placement.Definition.Kind != BuildPieceKind.Gate
                    || piece.DoorCollider == null) continue;
                var station = piece.GetComponent<PreparationGateStation>();
                if (station == null) station = piece.gameObject.AddComponent<PreparationGateStation>();
                station.Bind(this, piece);
            }
        }

        /// <summary>준비 단계에서만 성문을 열거나 닫는다. 부서진 성문이면 false.</summary>
        public bool Toggle(PlacedStructure gate)
        {
            if (gate == null || flow.Phase != GamePhase.Preparation) return false;
            if (gate.Health == null || gate.Health.IsDead) { Message = "Repair the destroyed gate before closing it."; return false; }
            bool open = !gate.IsHeldOpen;
            if (!world.SetGateHeldOpen(gate, open)) return false;
            Message = open ? "Gate opened. It will close when battle starts." : "Gate closed.";
            return true;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            Message = "";
            if (phase != GamePhase.Battle || world == null) return;
            foreach (PlacedStructure piece in world.Structures)
            {
                if (piece == null || !piece.IsHeldOpen) continue;
                world.SetGateHeldOpen(piece, false);
                if (piece.DoorCollider != null && piece.DoorCollider.enabled) ClearDoorway(piece.DoorCollider.bounds);
            }
        }

        /// <summary>닫힌 문짝과 겹친 아군·플레이어를 성 안쪽(바깥 방향의 반대)으로 옮긴다.</summary>
        void ClearDoorway(Bounds door)
        {
            Vector3 inward = -world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;
            if (inward.sqrMagnitude < 0.0001f) return;
            Bounds area = door;
            area.Expand(new Vector3(1f, 0f, 1f));
            float depth = Mathf.Abs(Vector3.Dot(door.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                Vector3 position = actor.transform.position;
                if (!area.Contains(new Vector3(position.x, area.center.y, position.z))) continue;
                Vector3 target = new Vector3(position.x, position.y, position.z)
                    + inward * (Vector3.Dot(door.center - position, inward) + depth + 1.5f);
                if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas)) hit.position = target;
                var agent = actor.GetComponent<NavMeshAgent>();
                var motor = actor.GetComponent<CharacterController>();
                if (agent != null && agent.enabled) agent.Warp(hit.position);
                else if (motor != null) { motor.enabled = false; actor.transform.position = hit.position; motor.enabled = true; }
                else actor.transform.position = hit.position;
            }
        }
    }
}
