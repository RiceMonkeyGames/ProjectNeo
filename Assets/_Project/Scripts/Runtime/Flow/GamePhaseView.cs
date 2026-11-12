using ProjectNeo.Characters;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 준비는 1인칭 탐색을 기본으로 건설소에서 건설 시점을 열고, 전투는 1인칭 직접 조작과 부대 지휘를 쓴다.
    /// 보상·런 결과에서는 건설과 조작을 모두 멈춘다. 단계가 바뀌어도 지형·유닛·지시는 그대로 둔다.
    /// </summary>
    [DefaultExecutionOrder(-75)]
    public sealed class GamePhaseView : MonoBehaviour
    {
        [SerializeField] Camera constructionCamera;
        [SerializeField] FirstPersonCamera battleCamera;
        [SerializeField] PlacementController placement;
        [SerializeField] PlayerInputController playerInput;
        [SerializeField] SquadCommandInput commandInput;
        [SerializeField] GameObject constructionHud, commandHud;
        [SerializeField] GameObject[] actors;
        GameFlow flow;
        bool constructionOpen, panelOpen;

        public bool IsBuildView { get; private set; }
        public bool IsBattleView { get; private set; }
        public bool IsExplorationView { get; private set; }
        /// <summary>준비 중 시설 팝업 같은 화면이 열려 1인칭 이동·시점을 멈추고 커서를 푼 상태(D-0).</summary>
        public bool IsPanelView { get; private set; }

        public void Configure(Camera buildCamera, FirstPersonCamera firstPersonCamera, PlacementController placementController,
            PlayerInputController playerController, SquadCommandInput squadInput, GameObject buildHud, GameObject squadHud,
            GameObject[] sceneActors)
        {
            constructionCamera = buildCamera; battleCamera = firstPersonCamera; placement = placementController;
            playerInput = playerController; commandInput = squadInput; constructionHud = buildHud; commandHud = squadHud;
            actors = sceneActors;
        }

        void Awake()
        {
            // ConstructionWorld(-200)가 길을 만든 뒤, 지휘 센터(-50)가 편성을 읽기 전에 유닛을 활성화한다.
            foreach (GameObject actor in actors) actor.SetActive(true);
            flow = GameSession.Flow;
            Apply(flow.Phase);
        }

        void OnEnable() => flow.PhaseChanged += Apply;

        void OnDisable()
        {
            flow.PhaseChanged -= Apply;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Apply(GamePhase phase)
        {
            constructionOpen = false;
            panelOpen = false;
            placement.Cancel();
            Refresh(phase);
        }

        /// <summary>준비 중 1인칭 탐색에서 팝업 화면을 연다. 건설 중이거나 이미 열려 있으면 false.</summary>
        public bool EnterPanel()
        {
            if (flow.Phase != GamePhase.Preparation || constructionOpen || panelOpen) return false;
            panelOpen = true;
            Refresh(flow.Phase);
            return true;
        }

        public void ExitPanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            Refresh(flow.Phase);
        }

        public bool EnterConstruction()
        {
            if (flow.Phase != GamePhase.Preparation || constructionOpen || panelOpen) return false;
            constructionOpen = true;
            Refresh(flow.Phase);
            return true;
        }

        public void ExitConstruction()
        {
            if (!constructionOpen) return;
            constructionOpen = false;
            placement.Cancel();
            Refresh(flow.Phase);
        }

        void Refresh(GamePhase phase)
        {
            // 게임 씬만 열어 시작하면 첫 프레임은 아직 타이틀 단계이므로 준비와 같게 보여준다.
            bool preparation = phase == GamePhase.Preparation || phase == GamePhase.Title;
            bool build = preparation && constructionOpen;
            bool battle = phase == GamePhase.Battle;
            bool panel = preparation && panelOpen && !build;
            bool exploration = preparation && !build && !panel;
            placement.enabled = false; playerInput.enabled = false; commandInput.enabled = false;
            constructionCamera.gameObject.SetActive(build);
            battleCamera.gameObject.SetActive(!build);
            constructionHud.SetActive(build); commandHud.SetActive(battle);
            IsBuildView = build; IsBattleView = battle; IsExplorationView = exploration; IsPanelView = panel;
            placement.enabled = build;
            playerInput.enabled = battle || exploration;
            commandInput.enabled = battle;
            Cursor.lockState = battle || exploration ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !(battle || exploration);
        }
    }
}
