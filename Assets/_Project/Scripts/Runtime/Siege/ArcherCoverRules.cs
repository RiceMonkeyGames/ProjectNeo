using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 적 궁수의 엄호 사격 우선순위(SIEGE-34). 세워졌거나 세우는 중인 사다리 위쪽 끝 둘레, 또는 마치콜레이션 구멍에 선 수비병은
    /// 제곱 거리에 배율을 곱해 더 가깝게 본다. 씬을 알지 않는다.
    /// </summary>
    public static class ArcherCoverRules
    {
        /// <summary>대상 비교 점수. 낮을수록 먼저 쏜다.</summary>
        public static float Score(float sqrDistance, bool coverTarget, float coverFactor) =>
            coverTarget ? sqrDistance * Mathf.Clamp(coverFactor, 0.01f, 1f) : sqrDistance;

        /// <summary>사다리가 엄호할 만한 상태인지: 세워졌거나 세우는 중이다.</summary>
        public static bool IsActiveLadder(LadderPhase phase) => phase == LadderPhase.Up || phase == LadderPhase.Raising;

        /// <summary>수비병이 사다리 위쪽 끝 둘레(수평 반경, 높이 차 허용치 안)에 있는지.</summary>
        public static bool IsNearLadderTop(Vector3 defender, Vector3 top, float radius, float heightTolerance) =>
            HoldDutyRules.IsNear(defender, top, radius, heightTolerance);
    }
}
