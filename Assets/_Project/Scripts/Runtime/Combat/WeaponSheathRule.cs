using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 허리에 찬 근접 무기를 언제 뽑고 넣을지 정하는 순수 규칙(ART-04). 게임 판정에는 쓰지 않는다.
    /// 뽑아야 할 이유(교전·가까운 대상)가 생기면 바로 뽑고, 그 이유가 <see cref="sheatheDelay"/>초 동안 없으면 넣는다.
    /// </summary>
    public sealed class WeaponSheathRule
    {
        readonly float sheatheDelay;
        float calmSeconds;

        public bool Armed { get; private set; }

        public WeaponSheathRule(float sheatheDelay)
        {
            this.sheatheDelay = Mathf.Max(0f, sheatheDelay);
        }

        /// <summary>시간을 흘린다. 뽑기·넣기가 바뀌었으면 true.</summary>
        public bool Tick(float deltaTime, bool wantsArmed)
        {
            if (wantsArmed)
            {
                calmSeconds = 0f;
                if (Armed) return false;
                Armed = true;
                return true;
            }
            if (!Armed) return false;
            calmSeconds += deltaTime;
            if (calmSeconds < sheatheDelay) return false;
            Armed = false;
            calmSeconds = 0f;
            return true;
        }

        /// <summary>공격이 먼저 시작돼 무기를 곧바로 손에 쥔 경우. 바뀌었으면 true.</summary>
        public bool ForceArmed()
        {
            calmSeconds = 0f;
            if (Armed) return false;
            Armed = true;
            return true;
        }

        /// <summary>대상이 이 거리 안이거나 교전 중이면 무기를 뽑아 둔다.</summary>
        public static bool WantsArmed(bool engaged, float targetDistance, float drawDistance)
            => engaged || targetDistance <= drawDistance;
    }
}
