using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>움직이는 목표를 앞질러 겨누는 규칙(SIEGE-33). 물리는 쓰지 않는다.</summary>
    public static class CannonLead
    {
        /// <summary>
        /// 포탄이 날아가는 동안 목표가 velocity로 움직일 자리를 구한다. 비행 시간을 맞추려고 몇 번 다시 푼다.
        /// 멈춰 있거나 포물선을 풀 수 없으면 지금 위치를 돌려준다.
        /// </summary>
        public static Vector3 Predict(Vector3 muzzle, Vector3 position, Vector3 velocity, float speed, float gravity,
            int iterations = 2)
        {
            if (velocity.sqrMagnitude < 0.0001f) return position;
            Vector3 aim = position;
            for (int i = 0; i < iterations; i++)
            {
                if (!CannonBallistics.TrySolve(muzzle, aim, speed, gravity, out _, out float time)) return position;
                aim = position + velocity * time;
            }
            return aim;
        }

        /// <summary>
        /// 앞지른 자리가 사거리·회전 범위 안이면 그 자리, 아니면 지금 위치가 안이면 지금 위치(사용자 결정 2026-11-24).
        /// 둘 다 밖이면 false.
        /// </summary>
        public static bool TryResolve(in CannonAimContext context, Vector3 current, Vector3 led, out Vector3 aim)
        {
            if (CannonAim.CanTarget(context, led)) { aim = led; return true; }
            if (CannonAim.CanTarget(context, current)) { aim = current; return true; }
            aim = current;
            return false;
        }
    }
}
