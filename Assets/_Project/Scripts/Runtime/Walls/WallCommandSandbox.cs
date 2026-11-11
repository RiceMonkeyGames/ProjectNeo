using ProjectNeo.Characters;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Walls
{
    /// <summary>건설과 지휘를 함께 검증하는 시점 전환. 정식 준비·전투 단계와는 별개다.</summary>
    [DefaultExecutionOrder(-75)]
    public sealed class WallCommandSandbox : MonoBehaviour
    {
        [SerializeField] Camera constructionCamera;
        [SerializeField] FirstPersonCamera commandCamera;
        [SerializeField] PlacementController placement;
        [SerializeField] PlayerInputController playerInput;
        [SerializeField] SquadCommandInput commandInput;
        [SerializeField] GameObject constructionHud, commandHud;
        [SerializeField] GameObject[] actors;
        [SerializeField] InputAction switchView = new InputAction("SwitchView", InputActionType.Button, "<Keyboard>/tab");
        InputAction toggle;
        public bool IsConstructionView { get; private set; } = true;

        public void Configure(Camera buildCamera, FirstPersonCamera firstPersonCamera, PlacementController placementController,
            PlayerInputController playerController, SquadCommandInput squadInput, GameObject buildHud, GameObject squadHud,
            GameObject[] sceneActors)
        {
            constructionCamera = buildCamera; commandCamera = firstPersonCamera; placement = placementController;
            playerInput = playerController; commandInput = squadInput; constructionHud = buildHud; commandHud = squadHud;
            actors = sceneActors;
        }

        void Awake()
        {
            // ConstructionWorld(-200)가 길을 만든 뒤, 지휘 센터(-50)가 편성을 읽기 전에 활성화한다.
            foreach (GameObject actor in actors) actor.SetActive(true);
            toggle = switchView.Clone();
            SetConstructionView(true);
        }
        void OnEnable() => toggle.Enable();
        void OnDisable() { if (toggle != null) toggle.Disable(); }
        void OnDestroy() { if (toggle != null) toggle.Dispose(); }
        void Update() { if (toggle.WasPressedThisFrame()) SetConstructionView(!IsConstructionView); }

        public void SetConstructionView(bool build)
        {
            placement.enabled = false; playerInput.enabled = false; commandInput.enabled = false;
            constructionCamera.gameObject.SetActive(build);
            commandCamera.gameObject.SetActive(!build);
            constructionHud.SetActive(build); commandHud.SetActive(!build);
            IsConstructionView = build;
            placement.enabled = build;
            playerInput.enabled = !build;
            commandInput.enabled = !build;
            Cursor.lockState = build ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = build;
        }
    }
}
