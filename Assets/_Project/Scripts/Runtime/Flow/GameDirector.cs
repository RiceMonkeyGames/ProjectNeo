using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 게임 씬과 흐름을 잇는다. 게임 씬만 열고 Play해도 새 런으로 시작하고, 흐름이 타이틀로 돌아가면 타이틀 씬을 부른다.
    /// 준비·전투 기능은 이 컴포넌트를 고치지 않고 <see cref="GameSession.Flow"/>의 단계 변경을 구독해 붙인다.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        [SerializeField] SceneCatalog catalog;
        GameFlow flow;

        public void Configure(SceneCatalog sceneCatalog) => catalog = sceneCatalog;

        void OnEnable()
        {
            flow = GameSession.Flow;
            flow.PhaseChanged += OnPhaseChanged;
        }

        void Start()
        {
            // 타이틀을 거치지 않고 이 씬에서 바로 시작한 경우.
            if (flow.Phase == GamePhase.Title) flow.StartRun();
        }

        void OnDisable() => flow.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Title) SceneSwitcher.Load(catalog.TitleScene);
        }
    }
}
