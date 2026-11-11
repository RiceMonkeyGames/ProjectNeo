using ProjectNeo.Characters;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 게임 씬에서 흐름 단계에 맞춰 시점·입력·화면을 바꾼다. 준비는 건설 직교 시점, 전투는 1인칭 직접 조작과 부대 지휘다.
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

        public bool IsBuildView { get; private set; }
        public bool IsBattleView { get; private set; }

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
            // 게임 씬만 열어 시작하면 첫 프레임은 아직 타이틀 단계이므로 준비와 같게 보여준다.
            bool build = phase == GamePhase.Preparation || phase == GamePhase.Title;
            bool battle = phase == GamePhase.Battle;
            placement.enabled = false; playerInput.enabled = false; commandInput.enabled = false;
            constructionCamera.gameObject.SetActive(build);
            battleCamera.gameObject.SetActive(!build);
            constructionHud.SetActive(build); commandHud.SetActive(battle);
            IsBuildView = build; IsBattleView = battle;
            placement.enabled = build;
            playerInput.enabled = battle;
            commandInput.enabled = battle;
            Cursor.lockState = battle ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !battle;
        }
    }
}
