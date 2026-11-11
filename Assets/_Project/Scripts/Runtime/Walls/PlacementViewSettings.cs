using UnityEngine;

namespace ProjectNeo.Walls
{
    [CreateAssetMenu(menuName = "ProjectNeo/Construction/Preview Settings")]
    public sealed class PlacementViewSettings : ScriptableObject
    {
        [SerializeField] Material ghostMaterial;
        [SerializeField] Color validColor = new Color(0.12f, 1f, 0.55f, 0.45f);
        [SerializeField] Color invalidColor = new Color(1f, 0.2f, 0.16f, 0.45f);
        public Material GhostMaterial => ghostMaterial;
        public Color ValidColor => validColor;
        public Color InvalidColor => invalidColor;
        public static PlacementViewSettings Create(Material ghostMaterial)
        {
            var value = CreateInstance<PlacementViewSettings>();
            value.ghostMaterial = ghostMaterial;
            return value;
        }
    }
}
