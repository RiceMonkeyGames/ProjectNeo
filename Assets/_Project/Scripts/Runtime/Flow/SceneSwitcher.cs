using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 씬 전환을 한 곳에서만 한다. 지금은 씬을 통째로 바꾸며, 불러오는 방식을 바꿀 때 이 클래스만 고친다.
    /// 전환이 끝나기 전의 중복 요청은 무시한다.
    /// </summary>
    public static class SceneSwitcher
    {
        public static bool IsLoading { get; private set; }

        public static bool Load(string sceneName)
        {
            if (IsLoading) return false;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) return false;
            IsLoading = true;
            operation.completed += _ => IsLoading = false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => IsLoading = false;
    }
}
