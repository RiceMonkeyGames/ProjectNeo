using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 활성 카메라의 위치·방향·직교(탑다운) 여부를 프레임당 한 번만 캐시해
    /// 400개 유닛의 <see cref="AILodTier"/> 판정이 <see cref="Camera.main"/>을 매번 찾지 않게 한다(PERF-ai-lod).
    /// </summary>
    public static class AILodContext
    {
        static int cachedFrame = -1;
        static bool hasView;
        static Vector3 viewPosition;
        static Vector3 viewForward;
        static bool topDownMode;

        static bool hasOverride;
        static Vector3 overridePosition;
        static Vector3 overrideForward;
        static bool overrideTopDown;

        public static bool HasView { get { EnsureCurrent(); return hasView; } }
        public static Vector3 ViewPosition { get { EnsureCurrent(); return viewPosition; } }
        public static Vector3 ViewForward { get { EnsureCurrent(); return viewForward; } }
        public static bool IsTopDownMode { get { EnsureCurrent(); return topDownMode; } }

        /// <summary>테스트나 벤치마크에서 카메라 대신 명시적인 기준점과 시야 방향을 고정한다.</summary>
        public static void SetViewOverride(Vector3 position, Vector3 forward, bool topDown = false)
        {
            hasOverride = true;
            overridePosition = position;
            overrideForward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            overrideTopDown = topDown;
            cachedFrame = -1;
        }

        public static void ClearViewOverride()
        {
            hasOverride = false;
            cachedFrame = -1;
        }

        public static void EnsureCurrent()
        {
            if (hasOverride)
            {
                hasView = true;
                viewPosition = overridePosition;
                viewForward = overrideForward;
                topDownMode = overrideTopDown;
                return;
            }
            int frame = Time.frameCount;
            if (cachedFrame == frame) return;
            cachedFrame = frame;

            Camera cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled)
            {
                hasView = false;
                topDownMode = false;
                return;
            }
            hasView = true;
            Transform t = cam.transform;
            viewPosition = t.position;
            viewForward = t.forward;
            // 직교(Orthographic) 카메라이거나 아래를 거의 수직으로 내려다보는 카메라는 탑다운 배치·건설 모드로 본다.
            topDownMode = cam.orthographic || viewForward.y < -0.75f;
        }

        /// <summary>
        /// 유닛 위치 <paramref name="unitPosition"/>까지의 거리 제곱과 시야각(전방 반구 또는 탑다운) 안 여부를 계산한다.
        /// 활성 카메라가 없으면 false를 돌려준다(호출자는 LOD 0으로 처리한다).
        /// </summary>
        public static bool TrySampleView(Vector3 unitPosition, out float sqrDistance, out bool inViewCone, out bool topDown)
        {
            EnsureCurrent();
            if (!hasView)
            {
                sqrDistance = 0f;
                inViewCone = true;
                topDown = false;
                return false;
            }
            topDown = topDownMode;
            Vector3 offset = unitPosition - viewPosition;
            if (topDown)
            {
                // 탑다운 뷰에서는 수평(XZ) 거리로 재고 화면 안으로 본다.
                offset.y = 0f;
                sqrDistance = offset.sqrMagnitude;
                inViewCone = true;
                return true;
            }
            sqrDistance = offset.sqrMagnitude;
            if (sqrDistance < 4f)
            {
                inViewCone = true;
                return true;
            }
            // 카메라 전방 약 110도 원뿔(cos 55° ≈ 0.57 -> 여유 있게 dot >= -0.1f 반구) 안이면 시야 안으로 본다.
            inViewCone = Vector3.Dot(offset.normalized, viewForward) >= -0.1f;
            return true;
        }
    }
}
