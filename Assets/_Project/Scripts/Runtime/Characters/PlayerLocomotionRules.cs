using System;
using UnityEngine;

namespace ProjectNeo.Characters
{
    public static class PlayerLocomotionRules
    {
        public static float SpeedMultiplier(bool crouching, bool sprinting, PlayerControlSettings settings)
        {
            if (crouching) return settings.CrouchSpeedMultiplier;
            return sprinting ? settings.SprintSpeedMultiplier : 1f;
        }

        /// <summary>주어진 중력에서 정확히 height만큼 올라가는 시작 속도(COMBAT-05).</summary>
        public static float JumpSpeed(float height, float gravity)
            => height <= 0f || gravity <= 0f ? 0f : (float)Math.Sqrt(2f * gravity * height);

        /// <summary>
        /// 점프 중 수평 속도. 뛸 때의 속도를 (1 - airControl)만큼 유지하고, 지금 입력 속도를 airControl만큼 섞는다.
        /// </summary>
        public static Vector3 AirHorizontal(Vector3 takeoff, Vector3 desired, float airControl)
        {
            float control = Mathf.Clamp01(airControl);
            return takeoff * (1f - control) + desired * control;
        }

        /// <summary>땅에 서 있고, 웅크리지 않고, 이동이 멈춰 있지 않으며 이미 뛰는 중이 아닐 때만 뛴다.</summary>
        public static bool CanJump(bool grounded, bool crouching, bool suspended, bool jumping)
            => grounded && !crouching && !suspended && !jumping;
    }
}
