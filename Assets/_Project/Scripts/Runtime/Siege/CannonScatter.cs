using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>자동 사격 탄착점 흩어짐. 수평 원 안에 고르게 퍼지고 같은 시드면 같은 결과다.</summary>
    public static class CannonScatter
    {
        public static Vector3 Apply(Vector3 point, float radius, IRandomSource random)
        {
            if (random == null || !(radius > 0f)) return point;
            float distance = radius * Mathf.Sqrt(random.NextFloat());
            float angle = random.NextFloat() * Mathf.PI * 2f;
            return point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        }
    }
}
