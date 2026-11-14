using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>준비 단계에 성문 앞에서 F로 성문을 열고 닫는다(PREP-05). GamePrepGate가 성문 조각에 붙인다.</summary>
    public sealed class PreparationGateStation : PreparationStation
    {
        GamePrepGate owner;
        PlacedStructure gate;

        public PlacedStructure Gate => gate;
        public override GamePhaseView PhaseView => owner != null ? owner.PhaseView : null;
        public override string Prompt => gate != null && gate.Health != null && gate.Health.IsDead ? "부서진 성문 (수리 전까지 열려 있음)"
            : gate != null && gate.IsHeldOpen ? "성문 닫기" : "성문 열기";

        public void Bind(GamePrepGate manager, PlacedStructure structure) { owner = manager; gate = structure; }

        public override bool Interact() => owner != null && owner.Toggle(gate);
    }
}
