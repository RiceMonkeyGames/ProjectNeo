using ProjectNeo.Flow;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>타이틀 화면의 버튼 동작. 새 런을 시작해 게임 씬으로 가거나 게임을 끝낸다.</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] SceneCatalog catalog;

        public void Configure(SceneCatalog sceneCatalog) => catalog = sceneCatalog;

        void Start()
        {
            // 전투 중 잠근 커서가 남아 있지 않게 한다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void StartGame()
        {
            if (SceneSwitcher.IsLoading) return;
            if (GameSession.Flow.StartRun()) SceneSwitcher.Load(catalog.GameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
