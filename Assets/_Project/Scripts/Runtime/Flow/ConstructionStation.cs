using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>건설소 몸통에 붙이는 준비 단계 상호작용 대상.</summary>
    public sealed class ConstructionStation : PreparationStation
    {
        [SerializeField] GamePhaseView phaseView;
        public override GamePhaseView PhaseView => phaseView;
        public override string Prompt => "건설소 이용";
        public void Configure(GamePhaseView view) => phaseView = view;
        public override bool Interact() => phaseView != null && phaseView.EnterConstruction();
    }
}
