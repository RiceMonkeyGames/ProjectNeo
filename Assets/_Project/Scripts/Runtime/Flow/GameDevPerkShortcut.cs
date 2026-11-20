#if UNITY_EDITOR
using System.Linq;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 에디터 전용 개발 단축키(META-02). 퍽 찍기 화면(META-09)이 생기기 전까지 Game 씬에서 퍽 효과를 확인하는 용도다.
    /// 준비 단계나 보상 화면에서 F11을 누르면 런 콘텐츠 목록의 퍽 중 아직 안 찍은 첫 퍽을 찍는다. 포인트가 모자라면
    /// 저장되지 않는 덤 포인트를 채운다(<see cref="PerkLoadout.BonusPoints"/>). 빌드에는 들어가지 않는다.
    /// </summary>
    public sealed class GameDevPerkShortcut : MonoBehaviour
    {
        GameBattleController battle;

        void Awake() => battle = GetComponent<GameBattleController>();

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f11Key.wasPressedThisFrame || battle == null) return;
            GamePhase phase = GameSession.Flow.Phase;
            if (phase != GamePhase.Preparation && phase != GamePhase.Reward) return;
            RunState state = battle.RunLink.State;
            if (state?.Perks == null || !(battle.RunLink.Content is RunContentCatalog catalog)) return;

            foreach (PerkDefinition perk in catalog.Perks)
            {
                if (perk == null || state.Perks.Allocated.Contains(perk)) continue;
                int missing = perk.PointCost - state.Perks.AvailablePoints;
                if (missing > 0) state.Perks.BonusPoints += missing;
                bool allocated = state.TryAllocatePerk(perk);
                Debug.Log(allocated ? $"[Dev F11] Perk allocated: {perk.DisplayName}" : $"[Dev F11] Could not allocate {perk.DisplayName}");
                return;
            }
            Debug.Log("[Dev F11] Every perk in the run content catalog is already allocated.");
        }
    }
}
#endif
