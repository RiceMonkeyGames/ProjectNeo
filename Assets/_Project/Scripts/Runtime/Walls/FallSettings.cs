using UnityEngine;

namespace ProjectNeo.Walls
{
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Fall Settings")]
    public sealed class FallSettings : ScriptableObject
    {
        [SerializeField, Min(0.01f)] float gravity = 25f;
        [SerializeField, Min(0f)] float safeHeight = 1f;
        [SerializeField, Min(0f)] float damagePerMeter = 12f;
        [SerializeField, Min(0.01f)] float supportProbe = 0.4f;
        [SerializeField, Min(0.01f)] float navMeshSnapDistance = 0.6f;
        public float Gravity => gravity;
        public float SafeHeight => safeHeight;
        public float DamagePerMeter => damagePerMeter;
        public float SupportProbe => supportProbe;
        public float NavMeshSnapDistance => navMeshSnapDistance;
        public static FallSettings Create() => CreateInstance<FallSettings>();
    }

    public static class FallDamageRules
    {
        public static float Calculate(float height, float safeHeight, float damagePerMeter) =>
            Mathf.Max(0f, height - Mathf.Max(0f, safeHeight)) * Mathf.Max(0f, damagePerMeter);
    }
}
