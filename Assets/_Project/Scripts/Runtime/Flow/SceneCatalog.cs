using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>흐름이 오가는 씬의 이름. 코드에 씬 이름을 적지 않고 이 에셋에서 바꾼다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Scene Catalog", fileName = "SceneCatalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [SerializeField] string titleScene = "Title";
        [SerializeField] string gameScene = "Game";

        public string TitleScene => titleScene;
        public string GameScene => gameScene;

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static SceneCatalog Create(string titleScene, string gameScene)
        {
            var catalog = CreateInstance<SceneCatalog>();
            catalog.titleScene = titleScene;
            catalog.gameScene = gameScene;
            return catalog;
        }
    }
}
