using UnityEngine;

namespace ProjectNeo.Flow
{
    public sealed class BattleStartStation : PreparationStation
    {
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameBattleController battle;
        public override GamePhaseView PhaseView => phaseView;
        public override string Prompt => battle.CanStart ? "전투 시작" : "전장 준비 중";
        public void Configure(GamePhaseView view, GameBattleController controller) { phaseView = view; battle = controller; }
        public override bool Interact() => battle != null && battle.TryStartBattle();
    }
}
