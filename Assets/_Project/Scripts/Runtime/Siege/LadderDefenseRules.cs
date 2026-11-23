using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>성벽 위 사다리 방어(SIEGE-31)의 순수 자리·판정 규칙. 씬과 유닛을 알지 않는다.</summary>
    public static class LadderDefenseRules
    {
        /// <summary>
        /// 위쪽 끝 둘레의 밀기 자리. 0번은 위쪽 끝, 그다음은 성벽을 따라 오른쪽·왼쪽으로 번갈아 간격만큼 벌어진다.
        /// </summary>
        public static Vector3 PushSpot(Vector3 top, Vector3 outward, int index, float spacing)
        {
            if (index <= 0) return top;
            Vector3 side = Side(outward);
            int step = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1f : -1f;
            return top + side * (sign * step * spacing);
        }

        /// <summary>
        /// 출구 안쪽 반원 위의 길목 자리. 안쪽 방향을 가운데로 -90도부터 +90도까지 고르게 나눈다(자리가 하나면 정면 안쪽).
        /// </summary>
        public static Vector3 ChokepointSlot(Vector3 top, Vector3 outward, float radius, int count, int index)
        {
            Vector3 inward = Flat(-outward);
            if (inward.sqrMagnitude < 0.0001f) inward = Vector3.forward;
            float angle = count <= 1 ? 0f : -90f + 180f * Mathf.Clamp(index, 0, count - 1) / (count - 1);
            return top + Quaternion.AngleAxis(angle, Vector3.up) * inward * radius;
        }

        /// <summary>남는 수비병이 출구를 둘러쌀 때인지: 오르는 적이 기준 높이 이상 올라왔거나, 올라온 적이 밀기를 막고 있다.</summary>
        public static bool NeedsChokepoint(bool hostileClimbing, float climbFraction, float threshold, bool hostileAtTop) =>
            hostileAtTop || (hostileClimbing && climbFraction >= threshold);

        /// <summary>오르는 유닛의 높이 비율(사다리 밑 0, 위쪽 끝 1).</summary>
        public static float ClimbFraction(float occupantY, float baseY, float edgeY)
        {
            float span = edgeY - baseY;
            if (span <= 0.0001f) return 1f;
            return Mathf.Clamp01((occupantY - baseY) / span);
        }

        /// <summary>
        /// 위쪽 끝 근처의 AI 아군을 밀기 인원으로 셀지. 공격 동작 중이면 세지 않는다. 대상이 없으면 센다.
        /// 원거리 유닛과 중지가 아닌(Engage·Move) 유닛은 대상이 있으면 세지 않는다(예전과 같다). 중지 중인 근접 유닛은
        /// 대상이 출구 둘레에서 싸우는 적일 때만 빠지고, 성벽 아래처럼 닿지 않거나 먼 대상 때문에 밀기를 멈추지 않는다.
        /// </summary>
        public static bool CountsAsAiPusher(bool attacking, bool hasTarget, bool ranged, bool holding, bool targetAtExit)
        {
            if (attacking) return false;
            if (!hasTarget) return true;
            if (ranged || !holding) return false;
            return !targetAtExit;
        }

        /// <summary>성벽을 따라 오른쪽 방향(바깥 방향 기준).</summary>
        public static Vector3 Side(Vector3 outward)
        {
            Vector3 flat = Flat(outward);
            if (flat.sqrMagnitude < 0.0001f) return Vector3.right;
            return Vector3.Cross(Vector3.up, flat).normalized;
        }

        static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
        }
    }
}
