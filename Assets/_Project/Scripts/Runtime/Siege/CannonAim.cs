using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>대포 하나의 조준 조건. 위치·설치 방향과 사거리·회전 한계를 묶는다.</summary>
    public readonly struct CannonAimContext
    {
        public Vector3 Origin { get; }
        public Vector3 Facing { get; }
        public float MinimumRange { get; }
        public float MaximumRange { get; }
        public float HalfAngle { get; }

        public CannonAimContext(Vector3 origin, Vector3 facing, float minimumRange, float maximumRange, float halfAngle)
        {
            Origin = origin;
            facing.y = 0f;
            Facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
            MinimumRange = Mathf.Max(0f, minimumRange);
            MaximumRange = Mathf.Max(MinimumRange, maximumRange);
            HalfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
        }

        public static CannonAimContext From(CannonSettings settings, Vector3 origin, Vector3 facing) =>
            new CannonAimContext(origin, facing, settings.MinimumRange, settings.MaximumRange, settings.AimHalfAngle);

        /// <summary>사람이 탔을 때의 조준 조건. 사거리는 같고 회전 한계만 넓다(SIEGE-07).</summary>
        public static CannonAimContext Manual(CannonSettings settings, Vector3 origin, Vector3 facing) =>
            new CannonAimContext(origin, facing, settings.MinimumRange, settings.MaximumRange, settings.ManualAimHalfAngle);
    }

    /// <summary>회전 범위·사거리 판정. 거리와 각도는 수평면 기준이다.</summary>
    public static class CannonAim
    {
        public static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>설치 방향에서 목표까지의 수평 각도(도, 왼쪽 음수).</summary>
        public static float YawOffset(Vector3 facing, Vector3 origin, Vector3 point)
        {
            Vector3 offset = point - origin;
            offset.y = 0f;
            facing.y = 0f;
            if (offset.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(facing, offset, Vector3.up);
        }

        public static bool IsWithinArc(in CannonAimContext context, Vector3 point) =>
            Mathf.Abs(YawOffset(context.Facing, context.Origin, point)) <= context.HalfAngle + 0.001f;

        public static bool IsInRange(in CannonAimContext context, Vector3 point)
        {
            float distance = HorizontalDistance(context.Origin, point);
            return distance >= context.MinimumRange && distance <= context.MaximumRange;
        }

        public static bool CanTarget(in CannonAimContext context, Vector3 point) =>
            IsInRange(context, point) && IsWithinArc(context, point);

        /// <summary>포신 각도를 목표 각도 쪽으로 정해진 속도만큼 돌린다. 각도는 설치 방향 기준(도)이다.</summary>
        public static float StepSwivel(float current, float target, float degreesPerSecond, float deltaTime)
        {
            if (deltaTime <= 0f || degreesPerSecond <= 0f) return current;
            return Mathf.MoveTowards(current, target, degreesPerSecond * deltaTime);
        }

        /// <summary>직접 조준할 때 회전 한계 밖으로 나가지 않게 각도를 자른다.</summary>
        public static float ClampYawOffset(float yawOffset, float halfAngle)
        {
            float limit = Mathf.Clamp(halfAngle, 0f, 180f);
            return Mathf.Clamp(Mathf.DeltaAngle(0f, yawOffset), -limit, limit);
        }
    }
}
