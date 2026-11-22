using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// SIEGE-14 시험 씬 전용 키. 성문·쇠창살 상태와 성벽 무너짐을 직접 바꿔 본다.
    /// P 쇠창살 올림/내림, O 성문 열어 두기/닫기, B 성문 부수기/고치기, K 화면 가운데로 가리킨 성벽·보루·계단 무너뜨리기.
    /// </summary>
    public sealed class CastleKitTestKeys : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] CastleKitDresser dresser;

        readonly RaycastHit[] hits = new RaycastHit[32];

        public void Configure(ConstructionWorld owner, CastleKitDresser kit) { world = owner; dresser = kit; }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || world == null) return;
            if (keyboard.pKey.wasPressedThisFrame && dresser != null) dresser.PortcullisRaised = !dresser.PortcullisRaised;
            PlacedStructure gate = Gate();
            if (keyboard.oKey.wasPressedThisFrame && gate != null) world.SetGateHeldOpen(gate, !gate.IsHeldOpen);
            if (keyboard.bKey.wasPressedThisFrame && gate != null)
            {
                if (gate.IsDoorOpen && !gate.IsHeldOpen) world.NotifyGateRepaired(gate);
                else { if (gate.IsHeldOpen) world.SetGateHeldOpen(gate, false); world.BreachGate(gate); }
            }
            if (keyboard.kKey.wasPressedThisFrame)
            {
                PlacedStructure target = Aimed();
                if (target != null) world.Collapse(target);
            }
        }

        PlacedStructure Gate()
        {
            foreach (PlacedStructure structure in world.Structures)
                if (structure != null && structure.Placement != null && structure.Placement.Definition.Kind == BuildPieceKind.Gate) return structure;
            return null;
        }

        // 화면 가운데에서 가장 가까운 성벽·보루·계단(성문 제외).
        PlacedStructure Aimed()
        {
            Camera view = Camera.main;
            if (view == null) return null;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, hits, 200f, ~0, QueryTriggerInteraction.Ignore);
            PlacedStructure found = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var structure = hits[i].collider.GetComponentInParent<PlacedStructure>();
                if (structure == null || hits[i].distance >= nearest) continue;
                BuildPieceKind kind = structure.Placement.Definition.Kind;
                if (kind != BuildPieceKind.Wall && kind != BuildPieceKind.Bastion && kind != BuildPieceKind.Stairs) continue;
                found = structure; nearest = hits[i].distance;
            }
            return found;
        }
    }
}
