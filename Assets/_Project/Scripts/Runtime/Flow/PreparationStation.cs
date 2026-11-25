using UnityEngine;

namespace ProjectNeo.Flow
{
    public abstract class PreparationStation : MonoBehaviour
    {
        [Tooltip("이 시설을 맡은 NPC(SIEGE-36). 있으면 F는 이 NPC에게만 걸리고 건물 충돌체로는 열리지 않는다.")]
        [SerializeField] StationNpc npc;

        public abstract GamePhaseView PhaseView { get; }
        public abstract string Prompt { get; }
        public abstract bool Interact();

        public StationNpc Npc => npc;
        public void SetNpc(StationNpc target) => npc = target;
    }
}
