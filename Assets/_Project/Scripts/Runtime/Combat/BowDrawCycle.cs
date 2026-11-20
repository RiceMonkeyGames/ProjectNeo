using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 활 쏘는 모델의 장전(Load) → 겨누고 유지(Hold) → 발사(Release) 순서를 정하는 순수 규칙(ART-04).
    /// 게임 판정은 바꾸지 않는다. 공격 시작 신호(<see cref="Fire"/>)를 받으면 화살이 실제로 나가는 순간(선딜레이 뒤)에
    /// 발사 동작이 시작되도록 장전을 서두르고, 발사 뒤 교전 중이면 다시 장전해 활을 당긴 채 기다린다.
    /// </summary>
    public sealed class BowDrawCycle
    {
        readonly float loadSeconds;
        readonly float releaseSeconds;
        float remaining;
        float drawProgress;
        float impactIn = -1f;

        public BowDrawPhase Phase { get; private set; } = BowDrawPhase.Lowered;
        /// <summary>지금 단계 클립의 재생 배속. 장전을 선딜레이 안에 끝내야 하면 1보다 커진다.</summary>
        public float PhaseSpeed { get; private set; } = 1f;

        public BowDrawCycle(float loadSeconds, float releaseSeconds)
        {
            this.loadSeconds = Mathf.Max(0.01f, loadSeconds);
            this.releaseSeconds = Mathf.Max(0.01f, releaseSeconds);
        }

        /// <summary>공격이 시작됐다. <paramref name="impactDelay"/>초 뒤 화살이 나간다.</summary>
        public void Fire(float impactDelay)
        {
            impactDelay = Mathf.Max(0f, impactDelay);
            impactIn = impactDelay;
            if (Phase == BowDrawPhase.Holding) return;
            if (Phase != BowDrawPhase.Drawing) BeginDrawing();
            // 남은 장전이 화살 나가는 순간보다 길면 빠르게 재생한다.
            float needed = (1f - drawProgress) * loadSeconds;
            if (impactDelay <= 0f) { drawProgress = 1f; remaining = 0f; return; }
            remaining = Mathf.Min(needed / PhaseSpeedFloor(), impactDelay);
            PhaseSpeed = remaining > 0f ? Mathf.Max(1f, needed / remaining) : 1f;
        }

        /// <summary>시간을 흘린다. 단계가 바뀌었으면 true.</summary>
        /// <param name="engaged">교전 중(최근에 쐈거나 겨눈 채 기다리라는 지시)인지. 아니면 발사 뒤 활을 내린다.</param>
        public bool Tick(float deltaTime, bool engaged)
        {
            BowDrawPhase before = Phase;
            float speed = PhaseSpeed;
            if (impactIn >= 0f)
            {
                impactIn -= deltaTime;
                if (impactIn <= 0f)
                {
                    impactIn = -1f;
                    Enter(BowDrawPhase.Releasing, releaseSeconds);
                    return true;
                }
            }
            switch (Phase)
            {
                case BowDrawPhase.Drawing:
                    remaining -= deltaTime;
                    drawProgress = Mathf.Min(1f, drawProgress + deltaTime * speed / loadSeconds);
                    if (remaining <= 0f) Enter(BowDrawPhase.Holding, 0f);
                    break;
                case BowDrawPhase.Releasing:
                    remaining -= deltaTime;
                    if (remaining <= 0f)
                    {
                        if (engaged) BeginDrawing();
                        else Enter(BowDrawPhase.Lowered, 0f);
                    }
                    break;
                case BowDrawPhase.Holding:
                    if (!engaged && impactIn < 0f) Enter(BowDrawPhase.Lowered, 0f);
                    break;
                case BowDrawPhase.Lowered:
                    if (engaged) BeginDrawing();
                    break;
            }
            return Phase != before;
        }

        void BeginDrawing()
        {
            drawProgress = 0f;
            Enter(BowDrawPhase.Drawing, loadSeconds);
        }

        void Enter(BowDrawPhase phase, float seconds)
        {
            Phase = phase;
            remaining = seconds;
            PhaseSpeed = 1f;
            if (phase != BowDrawPhase.Drawing) drawProgress = phase == BowDrawPhase.Holding ? 1f : 0f;
        }

        // 이미 빠르게 장전 중이면 그 배속을 기준으로 남은 시간을 잰다.
        float PhaseSpeedFloor() => Mathf.Max(1f, PhaseSpeed);
    }
}
