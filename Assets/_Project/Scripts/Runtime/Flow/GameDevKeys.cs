using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 시험 키(에디터·개발 빌드 전용, SIEGE-19). 정식 빌드에서는 아무것도 하지 않는다.
    /// F9 골드 +<see cref="goldStep"/>, F10 준비 중 남은 건설·수리 주문을 모두 바로 끝낸다(준비 시간은 쓰지 않는다).
    /// F11 화면 가운데로 가리킨 성벽·보루·계단·망루를 전투에서 무너진 것처럼 무너뜨린다(SIEGE-30, 준비·전투 모두).
    /// </summary>
    public sealed class GameDevKeys : MonoBehaviour
    {
        [SerializeField, Min(1)] int goldStep = 1000;

        GameConstructionWork work;
        ConstructionWorld world;
        readonly RaycastHit[] hits = new RaycastHit[32];

        void Update()
        {
            if (!Debug.isDebugBuild) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            GameFlow flow = GameSession.Flow;
            if (flow == null || flow.Run == null || flow.Run.State == null) return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                flow.Run.State.Wallet.AddGold(goldStep);
                Debug.Log($"[GameDevKeys] 골드 +{goldStep} (합계 {flow.Run.State.Wallet.Gold})");
            }
            if (keyboard.f10Key.wasPressedThisFrame && flow.Phase == GamePhase.Preparation)
            {
                if (work == null) work = FindAnyObjectByType<GameConstructionWork>();
                int finished = work != null ? work.FinishAllForTesting() : 0;
                Debug.Log($"[GameDevKeys] 작업 주문 {finished}개를 바로 끝냄");
            }
            if (keyboard.f11Key.wasPressedThisFrame)
            {
                if (world == null) world = FindAnyObjectByType<ConstructionWorld>();
                PlacedStructure target = world != null ? CastleKitTestKeys.Aimed(Camera.main, hits) : null;
                if (target == null) Debug.Log("[GameDevKeys] 가리킨 성벽·보루·계단·망루가 없음");
                else
                {
                    BuildPlacement placement = target.Placement;
                    Debug.Log($"[GameDevKeys] {placement.Definition.Kind} 무너뜨림 (칸 {placement.Cell}, {placement.Level + 1}층)");
                    world.Collapse(target);
                }
            }
        }
    }
}
