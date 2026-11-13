using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>임시 폭발 구를 정한 시간 동안 줄이며 없앤다. 외형 전용이다.</summary>
    public sealed class PlaceholderBlastFade : MonoBehaviour
    {
        float total, remaining;
        Vector3 start;

        public void Begin(float seconds)
        {
            total = remaining = Mathf.Max(0.01f, seconds);
            start = transform.localScale;
        }

        void Update()
        {
            if (total <= 0f) return;
            remaining -= Time.deltaTime;
            transform.localScale = start * Mathf.Clamp01(remaining / total);
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
