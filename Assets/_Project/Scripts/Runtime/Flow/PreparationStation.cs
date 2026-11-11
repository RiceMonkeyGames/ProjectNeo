using UnityEngine;

namespace ProjectNeo.Flow
{
    public abstract class PreparationStation : MonoBehaviour
    {
        public abstract GamePhaseView PhaseView { get; }
        public abstract string Prompt { get; }
        public abstract bool Interact();
    }
}
