using ProjectNeo.Facilities;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>안뜰 시설 건물 몸통에 붙이는 준비 단계 상호작용 대상. F로 그 시설의 팝업을 연다(D-0).</summary>
    public sealed class FacilityStation : PreparationStation
    {
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameFacilities facilities;
        [SerializeField] FacilityType type;

        public FacilityType Type => type;
        public override GamePhaseView PhaseView => phaseView;
        public override string Prompt => facilities != null ? facilities.NameOf(type) + " - Use" : type + " - Use";

        public void Configure(GamePhaseView view, GameFacilities controller, FacilityType facility)
        { phaseView = view; facilities = controller; type = facility; }

        public override bool Interact() => facilities != null && facilities.Open(type);
    }
}
