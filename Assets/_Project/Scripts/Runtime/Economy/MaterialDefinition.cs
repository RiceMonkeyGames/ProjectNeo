using UnityEngine;

namespace ProjectNeo.Economy
{
    /// <summary>몬스터 재료 한 종류.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Material", fileName = "Material_")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;

        public string Id => id;
        public string DisplayName => displayName;

        /// <summary>코드에서 재료를 만든다(테스트용).</summary>
        public static MaterialDefinition Create(string id)
        {
            var material = CreateInstance<MaterialDefinition>();
            material.id = id;
            material.displayName = id;
            return material;
        }
    }
}
