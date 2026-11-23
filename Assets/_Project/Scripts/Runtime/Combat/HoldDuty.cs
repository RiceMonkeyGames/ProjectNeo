using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 중지 중인 유닛이 자리 대신 잠시 맡는 임무(SIEGE-31: 성벽 위 사다리 밀기·출구 길목).
    /// 임무 중에는 <see cref="Point"/>에 서서 <see cref="FacePoint"/>를 바라보고,
    /// <see cref="Focus"/> 둘레 <see cref="TargetRadius"/> 안(높이 차 <see cref="HeightTolerance"/> 안)의 적만 상대한다.
    /// </summary>
    public readonly struct HoldDuty
    {
        public readonly Vector3 Point;
        public readonly Vector3 FacePoint;
        public readonly Vector3 Focus;
        public readonly float TargetRadius;
        public readonly float HeightTolerance;

        public HoldDuty(Vector3 point, Vector3 facePoint, Vector3 focus, float targetRadius, float heightTolerance)
        {
            Point = point;
            FacePoint = facePoint;
            Focus = focus;
            TargetRadius = Mathf.Max(0f, targetRadius);
            HeightTolerance = Mathf.Max(0f, heightTolerance);
        }

        /// <summary>이 임무 중에 상대해도 되는 위치인지(초점 둘레 수평 반경 안, 높이 차 허용치 안).</summary>
        public bool Covers(Vector3 position) => HoldDutyRules.IsNear(position, Focus, TargetRadius, HeightTolerance);

        /// <summary>같은 자리·같은 초점의 임무인지(새로 길을 찾을 필요가 없는지).</summary>
        public bool SamePlace(HoldDuty other, float tolerance) =>
            (Point - other.Point).sqrMagnitude <= tolerance * tolerance
            && (Focus - other.Focus).sqrMagnitude <= tolerance * tolerance;
    }
}
