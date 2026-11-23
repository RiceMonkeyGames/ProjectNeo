using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대상 옆, 대상이 선 발판 위에 일정 높이로 떠서 공격한다(Q-43 기본형).
    /// 띄우는 높이를 낮게 두면 같은 발판의 아군 근접 공격이 닿고, 높게 두면 원거리 공격으로만 잡힌다.
    /// 같은 대상을 쫓는 비행 적끼리는 대상 둘레의 다른 자리를 잡고, 몸이 들어가지 않거나 대상이 가려지는 자리는 건너뛴다(SIEGE-16).
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Flyer Engagement - Hover Beside")]
    public sealed class HoverBesideEngagement : FlyerEngagement
    {
        // 대상 발 높이에서 비행 적 발까지의 높이.
        [SerializeField, Min(0f)] float hoverHeight = 0.5f;
        // 대상 몸통 가장자리에서 공격 사거리의 이 비율만큼 떨어진다.
        [SerializeField, Range(0.1f, 1f)] float standoffRatio = 0.7f;
        // 켜면 대상과 같은 높이의 발판 위만 자리로 고른다. 성벽 위 대상 옆이 성 밖 허공이 되지 않게 한다.
        [SerializeField] bool matchTargetSurface = true;
        [SerializeField, Min(0f)] float surfaceTolerance = 0.5f;
        // 한 바퀴에서 살펴볼 자리 수의 상한. 자리 간격이 넓으면 더 적게 본다.
        const int MaxAnglesPerRing = 16;
        // 첫 바퀴가 다 차면 자리 간격만큼 바깥 바퀴를 본다. 사거리를 넘지 않는 바퀴만 본다.
        const int MaxRings = 4;
        public float HoverHeight => hoverHeight;
        public float StandoffRatio => standoffRatio;
        public bool MatchTargetSurface => matchTargetSurface;
        public float SurfaceTolerance => surfaceTolerance;

        public override Vector3 HoverPoint(in FlyerHoverQuery query)
        {
            Vector3 targetFeet = query.TargetFeet;
            Vector3 toward = query.Flyer - targetFeet;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            float flyerRadius = Mathf.Max(0f, query.FlyerRadius), targetRadius = Mathf.Max(0f, query.TargetRadius);
            float range = Mathf.Max(0f, query.AttackRange);
            float distance = targetRadius + Mathf.Max(flyerRadius, range * standoffRatio);
            bool spread = query.Occupied != null && query.Occupied.Count > 0;
            float separation = Mathf.Max(Mathf.Max(0f, query.Separation), flyerRadius * 2f);
            // 사거리 끝(대상 가장자리 + 사거리)을 넘는 바퀴는 공격이 닿지 않으므로 보지 않는다.
            float outermost = targetRadius + Mathf.Max(flyerRadius, range);
            bool checkSurface = matchTargetSurface && query.SurfaceAt != null;

            // 차선 자리: 발판·시야는 맞지만 다른 비행 적과 겹치는 곳, 발판만 맞는 곳, 다가오던 쪽 첫 자리.
            Vector3 first = default, surfaceOnly = default, crowded = default;
            bool hasSurfaceOnly = false, hasCrowded = false;
            float ring = distance;
            for (int r = 0; r < MaxRings; r++, ring += separation)
            {
                if (r > 0 && (separation <= 0f || ring > outermost + 1e-4f)) break;
                int count = AngleCount(ring, spread ? separation : 0f);
                float step = 360f / count;
                for (int i = 0; i < count; i++)
                {
                    // 0, +간격, -간격, +2간격 … 순서로 다가오던 쪽에서 가까운 자리부터 본다.
                    float angle = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * step * ((i + 1) / 2);
                    Vector3 point = targetFeet + Quaternion.AngleAxis(angle, Vector3.up) * toward * ring;
                    point.y = targetFeet.y + hoverHeight;
                    if (r == 0 && i == 0) first = point;
                    if (checkSurface)
                    {
                        float? surface = query.SurfaceAt(new Vector3(point.x, targetFeet.y, point.z));
                        if (!surface.HasValue || Mathf.Abs(surface.Value - targetFeet.y) > surfaceTolerance) continue;
                    }
                    if (query.ClearAt != null && !query.ClearAt(point))
                    {
                        if (!hasSurfaceOnly) { surfaceOnly = point; hasSurfaceOnly = true; }
                        continue;
                    }
                    if (!spread || IsFree(point, query.Occupied, separation)) return point;
                    if (!hasCrowded) { crowded = point; hasCrowded = true; }
                }
            }
            // 맞는 자리가 없으면 덜 나쁜 자리를 쓴다. 아무 발판도 맞지 않으면 다가오던 쪽 옆에 뜬다.
            return hasCrowded ? crowded : hasSurfaceOnly ? surfaceOnly : first;
        }

        // 바퀴 둘레에 자리 간격을 지키며 놓을 수 있는 자리 수. 간격을 따지지 않으면 상한만큼 본다.
        static int AngleCount(float ring, float separation)
        {
            if (separation <= 0f || ring <= 0.0001f) return ring <= 0.0001f ? 1 : MaxAnglesPerRing;
            float half = Mathf.Clamp01(separation / (2f * ring));
            float needed = 2f * Mathf.Asin(half) * Mathf.Rad2Deg;
            if (needed <= 0.0001f) return MaxAnglesPerRing;
            return Mathf.Clamp(Mathf.FloorToInt(360f / needed + 1e-4f), 1, MaxAnglesPerRing);
        }

        static bool IsFree(Vector3 point, IReadOnlyList<Vector3> occupied, float separation)
        {
            float limit = separation * separation - 1e-4f;
            for (int i = 0; i < occupied.Count; i++)
            {
                Vector3 offset = occupied[i] - point;
                offset.y = 0f;
                if (offset.sqrMagnitude < limit) return false;
            }
            return true;
        }

        public static HoverBesideEngagement Create(float hoverHeight = 0.5f, float standoffRatio = 0.7f,
            bool matchTargetSurface = true, float surfaceTolerance = 0.5f)
        {
            var value = CreateInstance<HoverBesideEngagement>();
            value.hoverHeight = Mathf.Max(0f, hoverHeight);
            value.standoffRatio = Mathf.Clamp(standoffRatio, 0.1f, 1f);
            value.matchTargetSurface = matchTargetSurface;
            value.surfaceTolerance = Mathf.Max(0f, surfaceTolerance);
            return value;
        }
    }
}
