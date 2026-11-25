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
        [Tooltip("건설 격자선. 건설 화면에서만 켠다(SIEGE-36). 비어 있으면 건드리지 않는다.")]
        [SerializeField] GameObject buildGrid;
        GameFlow flow;
        bool constructionOpen, panelOpen, placementOpen;

        public bool IsBuildView { get; private set; }
        /// <summary>준비 중 위에서 내려다보며 부대를 배치하는 화면(PREP-05). 건설 시점 카메라를 쓰고 건설 도구는 끈다.</summary>
        public bool IsPlacementView { get; private set; }
        public bool IsBattleView { get; private set; }
        public bool IsExplorationView { get; private set; }
        /// <summary>준비 중 시설 팝업 같은 화면이 열려 1인칭 이동·시점을 멈추고 커서를 푼 상태(D-0).</summary>
        public bool IsPanelView { get; private set; }
        /// <summary>준비 단계 부대 배치(PREP-05). 지휘 입력과 건설 화면이 없으면 null.</summary>
        public GamePrepPlacement PrepPlacement { get; private set; }

        public void Configure(Camera buildCamera, FirstPersonCamera firstPersonCamera, PlacementController placementController,
            PlayerInputController playerController, SquadCommandInput squadInput, GameObject buildHud, GameObject squadHud,
            GameObject[] sceneActors)
        {
            constructionCamera = buildCamera; battleCamera = firstPersonCamera; placement = placementController;
            playerInput = playerController; commandInput = squadInput; constructionHud = buildHud; commandHud = squadHud;
            actors = sceneActors;
        }

        /// <summary>건설 화면에서만 보일 격자선(SIEGE-36).</summary>
        public void ConfigureBuildGrid(GameObject grid) => buildGrid = grid;

        void Awake()
        {
            // ConstructionWorld(-200)가 길을 만든 뒤, 지휘 센터(-50)가 편성을 읽기 전에 유닛을 활성화한다.
            foreach (GameObject actor in actors) actor.SetActive(true);
            flow = GameSession.Flow;
            // 준비 단계 부대 배치(PREP-05). 씬 파일을 바꾸지 않고 실행 중에 붙인다.
            if (commandInput != null && placement != null && PrepPlacement == null)
            {
                PrepPlacement = gameObject.AddComponent<GamePrepPlacement>();
                PrepPlacement.Configure(this, commandInput, placement, constructionCamera);
                // 부대 배치 화면의 지도 표식(UI-13). 같은 방식으로 실행 중에 붙인다.
                PrepPlacement.gameObject.AddComponent<ProjectNeo.UI.PlacementMapHud>().Configure(PrepPlacement, null);
            }
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
            placementOpen = false;
            placement.Cancel();
            Refresh(phase);
        }

        bool AnyOverlayOpen => constructionOpen || panelOpen || placementOpen;

        /// <summary>준비 중 1인칭 탐색에서 부대 배치 화면을 연다(PREP-05). 다른 화면이 열려 있으면 false.</summary>
        public bool EnterPlacement()
        {
            if (flow.Phase != GamePhase.Preparation || AnyOverlayOpen) return false;
            placementOpen = true;
            Refresh(flow.Phase);
            return true;
        }

        public void ExitPlacement()
        {
            if (!placementOpen) return;
            placementOpen = false;
            Refresh(flow.Phase);
        }

        /// <summary>준비 중 1인칭 탐색에서 팝업 화면을 연다. 건설 중이거나 이미 열려 있으면 false.</summary>
        public bool EnterPanel()
        {
            if (flow.Phase != GamePhase.Preparation || AnyOverlayOpen) return false;
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
            if (flow.Phase != GamePhase.Preparation || AnyOverlayOpen) return false;
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
            bool squadPlacement = preparation && placementOpen && !build && !panel;
            bool exploration = preparation && !build && !panel && !squadPlacement;
            bool topDown = build || squadPlacement;
            placement.enabled = false; playerInput.enabled = false; commandInput.enabled = false;
            constructionCamera.gameObject.SetActive(topDown);
            battleCamera.gameObject.SetActive(!topDown);
            // 준비 중 1인칭 탐색과 부대 배치 화면에서도 부대 상태를 보여준다(PREP-05). 키 안내 상자는 전투에서만 보인다.
            constructionHud.SetActive(build); commandHud.SetActive(battle || exploration || squadPlacement);
            // 격자선은 건설할 때만 필요하다. 1인칭 탐색·전투에서는 바닥 위 초록 선으로 거슬린다.
            if (buildGrid != null) buildGrid.SetActive(build);
            var commandView = commandHud.GetComponent<ProjectNeo.UI.SquadCommandHud>();
            if (commandView != null) commandView.SetHelpVisible(battle);
            IsBuildView = build; IsBattleView = battle; IsExplorationView = exploration; IsPanelView = panel;
            IsPlacementView = squadPlacement;
            placement.enabled = build;
            playerInput.enabled = battle || exploration;
            commandInput.enabled = battle || exploration;
            Cursor.lockState = battle || exploration ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !(battle || exploration);
        }
    }
}
