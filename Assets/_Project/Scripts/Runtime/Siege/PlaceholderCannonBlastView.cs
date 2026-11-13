using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 포탄 Visual 자식에 붙는 임시 폭발 표시. 터진 자리에 폭발 반경만 한 구를 잠깐 띄웠다가 줄여 없앤다.
    /// 피해 판정은 하지 않는다. 정식 효과가 들어오면 이 부품만 바꾼다.
    /// </summary>
    public sealed class PlaceholderCannonBlastView : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField, Min(0.05f)] float duration = 0.4f;
        [SerializeField, Min(0.1f)] float radius = 3f;
        CannonShell shell;

        public void Configure(Material blastMaterial, float blastRadius)
        {
            material = blastMaterial;
            radius = Mathf.Max(0.1f, blastRadius);
        }

        void Awake() => shell = GetComponentInParent<CannonShell>();
        void OnEnable() { if (shell != null) shell.Exploded += OnExploded; }
        void OnDisable() { if (shell != null) shell.Exploded -= OnExploded; }

        void OnExploded(CannonShell source, Vector3 point)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "CannonBlast";
            Destroy(flash.GetComponent<Collider>());
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(flash, gameObject.scene);
            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * (radius * 2f);
            var renderer = flash.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.AddComponent<PlaceholderBlastFade>().Begin(duration);
        }
    }
}
