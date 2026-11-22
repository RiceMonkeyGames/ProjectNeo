using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 자리 방향 복귀(COMBAT-14). 자리를 지키는 유닛이 싸울 대상 없이 자리에 서 있으면 대형이 바라보던 방향으로 다시 돈다.
    /// 각도는 y축 회전(도)이다.
    /// </summary>
    public static class HoldFacingRules
    {
        /// <summary>이 각도(도) 안이면 이미 그 방향을 보는 것으로 본다.</summary>
        public const float Tolerance = 1f;

        /// <summary>지금 방향에서 자리 방향까지 남은 각도(도, 0~180).</summary>
        public static float Remaining(float currentYaw, float postYaw) => Mathf.Abs(Mathf.DeltaAngle(currentYaw, postYaw));

        /// <summary>자리 방향으로 돌아야 하는지.</summary>
        public static bool NeedsTurn(float currentYaw, float postYaw) => Remaining(currentYaw, postYaw) > Tolerance;

        /// <summary>한 프레임에 최대 <paramref name="maxDegrees"/>만큼 자리 방향으로 돈 각도. 짧은 쪽으로 돈다.</summary>
        public static float Step(float currentYaw, float postYaw, float maxDegrees) =>
            Mathf.MoveTowardsAngle(currentYaw, postYaw, Mathf.Max(0f, maxDegrees));
    }
}
