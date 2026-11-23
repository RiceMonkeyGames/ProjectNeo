using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 시험 키(에디터·개발 빌드 전용, SIEGE-19). 정식 빌드에서는 아무것도 하지 않는다.
    /// F9 골드 +<see cref="goldStep"/>, F10 준비 중 남은 건설·수리 주문을 모두 바로 끝낸다(준비 시간은 쓰지 않는다).
    /// </summary>
    public sealed class GameDevKeys : MonoBehaviour
    {
        [SerializeField, Min(1)] int goldStep = 1000;

        GameConstructionWork work;

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
        }
    }
}
