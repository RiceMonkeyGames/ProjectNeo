using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 맞은 자리에 박혀 남는 화살의 수치(ART-05). 화살 프리팹의 StuckArrow가 참조한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stuck Arrow Settings")]
    public sealed class StuckArrowSettings : ScriptableObject
    {
        // 박힌 뒤 사라지기까지의 시간(초). 시체에 박힌 화살도 같다.
        [SerializeField, Min(0.1f)] float lifetime = 30f;
        // 장면에 동시에 박혀 있을 수 있는 화살 수. 넘으면 가장 먼저 박힌 것부터 없앤다.
        [SerializeField, Min(1)] int maxCount = 100;
        // 화살촉이 맞은 면 안으로 들어가는 깊이(m).
        [SerializeField, Min(0f)] float penetration = 0.12f;

        public float Lifetime => lifetime;
        public int MaxCount => maxCount;
        public float Penetration => penetration;

        public static StuckArrowSettings Create(float lifetime = 30f, int maxCount = 100, float penetration = 0.12f)
        {
            var settings = CreateInstance<StuckArrowSettings>();
            settings.lifetime = Mathf.Max(0.1f, lifetime);
            settings.maxCount = Mathf.Max(1, maxCount);
            settings.penetration = Mathf.Max(0f, penetration);
            return settings;
        }
    }
}
