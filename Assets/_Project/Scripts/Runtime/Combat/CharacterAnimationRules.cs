using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>캐릭터 모델 애니메이션의 순수 계산(ART-04). 게임 판정에는 쓰지 않는다.</summary>
    public static class CharacterAnimationRules
    {
        /// <summary>
        /// 근접 공격 클립을 어디서부터 재생할지(초). 클립에서 무기가 닿는 순간(<paramref name="contactSeconds"/>)이
        /// 공격 판정 시각(<paramref name="impactDelay"/>)과 겹치도록 앞부분을 건너뛴다.
        /// </summary>
        public static float MeleeStartSeconds(float contactSeconds, float impactDelay, float clipSeconds)
        {
            float start = Mathf.Max(0f, contactSeconds - Mathf.Max(0f, impactDelay));
            return Mathf.Min(start, Mathf.Max(0f, clipSeconds - 0.05f));
        }

        /// <summary>방향의 높이각(도). 위가 +, 아래가 -. 수평 성분이 거의 없으면 ±90도, 길이가 0이면 0.</summary>
        public static float PitchDegrees(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-8f) return 0f;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            return Mathf.Atan2(direction.y, Mathf.Max(horizontal, 0.0001f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 화살이 떠날 높이각(<paramref name="degrees"/>)을 활 조준 섞기 값 -1(아래)~0(정면)~1(위)로 바꾼다(COMBAT-11).
        /// 각 클립의 화살 선 각도(<paramref name="down"/>, <paramref name="middle"/>, <paramref name="up"/>) 사이를 구간마다
        /// 따로 나눠, 섞인 자세의 화살 선이 그 각도를 향하게 한다. 클립 범위 밖이면 끝값이다.
        /// </summary>
        public static float BowPitchBlend(float degrees, float down, float middle, float up)
        {
            if (degrees >= middle)
                return up > middle ? Mathf.Clamp01((degrees - middle) / (up - middle)) : 0f;
            return middle > down ? -Mathf.Clamp01((middle - degrees) / (middle - down)) : 0f;
        }

        /// <summary>교전 자세(전투 대기·활 당김)를 유지할지. 마지막 공격 뒤 일정 시간 또는 겨눈 채 기다리라는 지시가 있으면 유지한다.</summary>
        public static bool IsEngaged(float secondsSinceAttack, float engagedSeconds, bool holdsDrawnBow)
            => holdsDrawnBow || (secondsSinceAttack >= 0f && secondsSinceAttack < engagedSeconds);

        /// <summary>
        /// 궁수가 활을 당긴 자세를 이어 갈지. 교전 중이라도 빠르게 움직이면 내리고, 겨눈 채 기다리라는 지시가 있으면 유지한다.
        /// 이미 시작한 사격(장전→발사)은 이 값과 관계없이 끝까지 재생한다.
        /// </summary>
        public static bool KeepsBowDrawn(bool engaged, float moveSpeed, float lowerSpeed, bool holdsDrawnBow)
            => holdsDrawnBow || (engaged && moveSpeed < lowerSpeed);

        /// <summary>
        /// 방향을 수평면에 눕혀 정규화한다. 거의 수직이면(수평 성분이 없으면) <paramref name="fallback"/>을 눕혀 쓴다.
        /// 죽은 유닛의 무기를 바닥에 눕힐 방향에 쓴다.
        /// </summary>
        public static Vector3 FlatDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude >= 0.01f) return direction.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude >= 0.0001f ? fallback.normalized : Vector3.forward;
        }

        /// <summary>
        /// 0~count-1 중 무작위 번호. 둘 이상이면 <paramref name="previous"/>는 고르지 않는다(같은 공격 반복 방지).
        /// </summary>
        public static int PickOther(Core.IRandomSource random, int count, int previous)
        {
            if (count <= 0) return -1;
            if (count == 1 || previous < 0 || previous >= count) return random.NextInt(count);
            int pick = random.NextInt(count - 1);
            return pick >= previous ? pick + 1 : pick;
        }

        /// <summary>
        /// 화살 선(시위 손→활 손)이 모델 정면에서 수평으로 몇 도 돌아가 있는지(위에서 봐 시계 방향 +). 수평 성분이 없으면 0.
        /// </summary>
        public static float AimOffset(Vector3 modelForward, Vector3 arrowLine)
        {
            modelForward.y = 0f;
            arrowLine.y = 0f;
            if (modelForward.sqrMagnitude < 0.0001f || arrowLine.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(modelForward, arrowLine, Vector3.up);
        }

        /// <summary>
        /// 활을 겨눌 때 모델을 몸통 정면에서 몇 도(시계 방향 +) 돌릴지. 화살 선이 모델 정면에서 <paramref name="aimOffset"/>도
        /// 돌아가 있으므로, 대상 방향에서 그만큼 빼면 화살 선이 대상을 향한다. ±90도로 자르고, 수평 방향이 없으면 0.
        /// </summary>
        public static float BowModelYaw(Vector3 bodyForward, Vector3 toTarget, float aimOffset)
        {
            bodyForward.y = 0f;
            toTarget.y = 0f;
            if (bodyForward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f) return 0f;
            float toward = Vector3.SignedAngle(bodyForward, toTarget, Vector3.up);
            return Mathf.Clamp(Mathf.DeltaAngle(0f, toward - aimOffset), -90f, 90f);
        }

        /// <summary>고정 값에서 범위 안의 번호를 고른다(같은 값이면 같은 번호).</summary>
        public static int Variant(int seed, int count)
        {
            if (count <= 0) return -1;
            int value = seed % count;
            return value < 0 ? value + count : value;
        }
    }
}
