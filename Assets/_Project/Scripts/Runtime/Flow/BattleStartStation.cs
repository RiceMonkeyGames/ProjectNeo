using UnityEngine;

namespace ProjectNeo.Flow
{
    public sealed class BattleStartStation : PreparationStation
    {
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameBattleController battle;
        public override GamePhaseView PhaseView => phaseView;
        public override string Prompt => "여기서 B 길게 누르기 - 전투 시작";
        public void Configure(GamePhaseView view, GameBattleController controller) { phaseView = view; battle = controller; }
        // Legacy banner remains as scenery; FLOW-01 starts only through the preparation hold input.
        public override bool Interact() => false;
    }
}
