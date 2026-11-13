using System;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대상 옆, 대상이 선 발판 위에 일정 높이로 떠서 공격한다(Q-43 기본형).
    /// 띄우는 높이를 낮게 두면 같은 발판의 아군 근접 공격이 닿고, 높게 두면 원거리 공격으로만 잡힌다.
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
        static readonly float[] Angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        public float HoverHeight => hoverHeight;
        public float StandoffRatio => standoffRatio;
        public bool MatchTargetSurface => matchTargetSurface;
        public float SurfaceTolerance => surfaceTolerance;

        public override Vector3 HoverPoint(Vector3 flyer, Vector3 targetFeet, float attackRange, float flyerRadius,
            float targetRadius, Func<Vector3, float?> surfaceAt)
        {
            Vector3 toward = flyer - targetFeet;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            float distance = Mathf.Max(0f, targetRadius) + Mathf.Max(Mathf.Max(0f, flyerRadius),
                Mathf.Max(0f, attackRange) * standoffRatio);
            Vector3 first = default;
            for (int i = 0; i < Angles.Length; i++)
            {
                Vector3 point = targetFeet + Quaternion.AngleAxis(Angles[i], Vector3.up) * toward * distance;
                point.y = targetFeet.y + hoverHeight;
                if (i == 0) first = point;
                if (!matchTargetSurface || surfaceAt == null) return point;
                float? surface = surfaceAt(new Vector3(point.x, targetFeet.y, point.z));
                if (surface.HasValue && Mathf.Abs(surface.Value - targetFeet.y) <= surfaceTolerance) return point;
            }
            // 맞는 발판이 없으면 다가오던 쪽 옆에 뜬다.
            return first;
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
