using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>AI 아군의 사다리 밀기 강인도 등록과 피격 중단 재시도 쿨다운을 관리한다.</summary>
    public sealed class LadderPushInterruptHandle : IInterruptibleAction
    {
        readonly CharacterActor actor;
        bool isPushing;
        float poise;
        float interruptCooldown;

        public float CooldownRemaining { get; private set; }
        public bool IsOnCooldown => CooldownRemaining > 0f;
        public ActionPoise InterruptPoise => isPushing && !IsOnCooldown
            ? ActionPoise.Interruptible(poise)
            : ActionPoise.Uninterruptible;

        public LadderPushInterruptHandle(CharacterActor owner)
        {
            actor = owner;
            actor?.RegisterInterruptible(this);
        }

        public void SetPushing(bool active, float pushPoise, float cooldownSeconds)
        {
            isPushing = active;
            poise = Mathf.Max(0f, pushPoise);
            interruptCooldown = Mathf.Max(0f, cooldownSeconds);
        }

        public void Tick(float deltaTime)
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        }

        public void InterruptAction()
        {
            if (!isPushing) return;
            isPushing = false;
            CooldownRemaining = interruptCooldown;
        }

        public void Dispose()
        {
            isPushing = false;
            actor?.UnregisterInterruptible(this);
        }
    }
}
