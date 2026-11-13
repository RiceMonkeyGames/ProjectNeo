using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 순항 높이까지 오른 뒤 목적지로 곧게 날아가고, 목적지 가까이에서 그 높이로 내려간다.
    /// 성벽에 막히지 않는다. 처음 나온 비행 방식이다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Flyer Movement - Direct Flight")]
    public sealed class DirectFlightMovement : FlyerMovement
    {
        [SerializeField, Min(0.01f)] float verticalSpeed = 4f;
        // 목적지와의 수평 거리가 이 안이면 순항 높이를 버리고 목적지 높이로 내려간다.
        [SerializeField, Min(0f)] float descendRadius = 4f;
        // 순항 높이에서 이만큼 아래까지 올라오면 수평으로 나아가기 시작한다.
        [SerializeField, Min(0f)] float cruiseTolerance = 0.5f;
        public float VerticalSpeed => verticalSpeed;
        public float DescendRadius => descendRadius;
        public float CruiseTolerance => cruiseTolerance;

        public override Vector3 Step(Vector3 position, Vector3 destination, float cruiseHeight, float speed, float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime)) return position;
            var from = new Vector2(position.x, position.z);
            var to = new Vector2(destination.x, destination.z);
            bool near = (to - from).sqrMagnitude <= descendRadius * descendRadius;
            float targetHeight = near ? destination.y : Mathf.Max(cruiseHeight, destination.y);
            float y = Mathf.MoveTowards(position.y, targetHeight, verticalSpeed * deltaTime);
            // 멀리 있으면 성벽을 넘을 높이까지 오른 뒤에만 나아간다.
            bool travel = near || y >= Mathf.Max(cruiseHeight, destination.y) - cruiseTolerance;
            Vector2 flat = travel ? Vector2.MoveTowards(from, to, Mathf.Max(0f, speed) * deltaTime) : from;
            return new Vector3(flat.x, y, flat.y);
        }

        public static DirectFlightMovement Create(float verticalSpeed = 4f, float descendRadius = 4f, float cruiseTolerance = 0.5f)
        {
            var value = CreateInstance<DirectFlightMovement>();
            value.verticalSpeed = Mathf.Max(0.01f, verticalSpeed);
            value.descendRadius = Mathf.Max(0f, descendRadius);
            value.cruiseTolerance = Mathf.Max(0f, cruiseTolerance);
            return value;
        }
    }
}
